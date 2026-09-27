using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace NocturneMemoryHelper
{
    internal sealed class MemorySnapshot
    {
        internal bool DetailedDataAvailable;
        internal ulong TotalBytes;
        internal ulong AvailableBytes;
        internal ulong ActiveBytes;
        internal ulong ModifiedBytes;
        internal ulong StandbyBytes;
        internal ulong PriorityZeroStandbyBytes;
        internal uint MemoryLoadPercent;
        internal string Error;
    }

    internal static class NativeMemory
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MemoryStatusEx
        {
            internal uint Length;
            internal uint MemoryLoad;
            internal ulong TotalPhysical;
            internal ulong AvailablePhysical;
            internal ulong TotalPageFile;
            internal ulong AvailablePageFile;
            internal ulong TotalVirtual;
            internal ulong AvailableVirtual;
            internal ulong AvailableExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

        [DllImport("ntdll.dll")]
        private static extern int NtQuerySystemInformation(
            int informationClass,
            IntPtr information,
            int length,
            out int returnLength);

        private static ulong ReadNativeUInt(IntPtr buffer, int index)
        {
            int offset = index * IntPtr.Size;
            if (IntPtr.Size == 8) return unchecked((ulong)Marshal.ReadInt64(buffer, offset));
            return unchecked((uint)Marshal.ReadInt32(buffer, offset));
        }

        internal static MemorySnapshot Read()
        {
            MemorySnapshot result = new MemorySnapshot();
            MemoryStatusEx status = new MemoryStatusEx();
            status.Length = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));

            if (!GlobalMemoryStatusEx(ref status))
            {
                result.Error = "GlobalMemoryStatusEx failed: " + Marshal.GetLastWin32Error();
                return result;
            }

            result.TotalBytes = status.TotalPhysical;
            result.AvailableBytes = status.AvailablePhysical;
            result.MemoryLoadPercent = status.MemoryLoad;

            IntPtr buffer = Marshal.AllocHGlobal(1024);
            try
            {
                int returned;
                int ntStatus = NtQuerySystemInformation(80, buffer, 1024, out returned);
                if (ntStatus < 0)
                {
                    result.ActiveBytes = status.TotalPhysical - status.AvailablePhysical;
                    result.Error = "Detailed page-list query failed: 0x" + ntStatus.ToString("X8");
                    return result;
                }

                ulong zeroPages = ReadNativeUInt(buffer, 0);
                ulong freePages = ReadNativeUInt(buffer, 1);
                ulong modifiedPages = ReadNativeUInt(buffer, 2);
                ulong modifiedNoWritePages = ReadNativeUInt(buffer, 3);
                ulong badPages = ReadNativeUInt(buffer, 4);
                ulong standbyPages = 0;
                for (int priority = 0; priority < 8; priority++)
                    standbyPages += ReadNativeUInt(buffer, 5 + priority);

                ulong pageSize = unchecked((ulong)Environment.SystemPageSize);
                ulong totalPages = status.TotalPhysical / pageSize;
                ulong accountedPages = zeroPages + freePages + modifiedPages + modifiedNoWritePages + badPages + standbyPages;
                ulong activePages = totalPages > accountedPages ? totalPages - accountedPages : 0;

                result.ActiveBytes = activePages * pageSize;
                result.ModifiedBytes = modifiedPages * pageSize;
                result.StandbyBytes = standbyPages * pageSize;
                result.PriorityZeroStandbyBytes = ReadNativeUInt(buffer, 5) * pageSize;
                result.DetailedDataAvailable = true;
            }
            catch (Exception ex)
            {
                result.ActiveBytes = status.TotalPhysical - status.AvailablePhysical;
                result.Error = ex.Message;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
            return result;
        }
    }

    internal static class NativeCleaner
    {
        private const uint TokenAdjustPrivileges = 0x0020;
        private const uint TokenQuery = 0x0008;
        private const uint PrivilegeEnabled = 0x00000002;
        private const int ErrorNotAllAssigned = 1300;
        private const int SystemMemoryListInformation = 80;

        [StructLayout(LayoutKind.Sequential)]
        private struct Luid
        {
            internal uint LowPart;
            internal int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LuidAndAttributes
        {
            internal Luid Luid;
            internal uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TokenPrivileges
        {
            internal uint PrivilegeCount;
            internal LuidAndAttributes Privileges;
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LookupPrivilegeValue(string systemName, string name, out Luid luid);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AdjustTokenPrivileges(
            IntPtr tokenHandle,
            [MarshalAs(UnmanagedType.Bool)] bool disableAllPrivileges,
            ref TokenPrivileges newState,
            uint bufferLength,
            IntPtr previousState,
            IntPtr returnLength);

        [DllImport("ntdll.dll")]
        private static extern int NtSetSystemInformation(int informationClass, ref int information, int informationLength);

        internal static void EnableMemoryManagementPrivilege()
        {
            IntPtr token;
            if (!OpenProcessToken(GetCurrentProcess(), TokenAdjustPrivileges | TokenQuery, out token))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open the worker process token.");

            try
            {
                Luid luid;
                if (!LookupPrivilegeValue(null, "SeProfileSingleProcessPrivilege", out luid))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not locate SeProfileSingleProcessPrivilege.");

                TokenPrivileges privileges = new TokenPrivileges();
                privileges.PrivilegeCount = 1;
                privileges.Privileges = new LuidAndAttributes();
                privileges.Privileges.Luid = luid;
                privileges.Privileges.Attributes = PrivilegeEnabled;

                if (!AdjustTokenPrivileges(token, false, ref privileges, 0, IntPtr.Zero, IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not enable SeProfileSingleProcessPrivilege.");

                int error = Marshal.GetLastWin32Error();
                if (error == ErrorNotAllAssigned)
                    throw new Win32Exception(error, "The elevated worker was not granted SeProfileSingleProcessPrivilege.");
            }
            finally
            {
                CloseHandle(token);
            }
        }

        internal static void RunMemoryListCommand(int command)
        {
            int value = command;
            int status = NtSetSystemInformation(SystemMemoryListInformation, ref value, sizeof(int));
            if (status < 0)
            {
                throw new InvalidOperationException(
                    "NtSetSystemInformation command " + command +
                    " failed with NTSTATUS 0x" + status.ToString("X8") + ".");
            }
        }
    }
}
