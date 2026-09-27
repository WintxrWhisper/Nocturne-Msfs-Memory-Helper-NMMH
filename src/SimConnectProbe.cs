using System;
using System.Runtime.InteropServices;

namespace NocturneMemoryHelper
{
    internal sealed class SimConnectProbe : IDisposable
    {
        private IntPtr runtime = IntPtr.Zero;
        private IntPtr connection = IntPtr.Zero;
        private string lastError = String.Empty;
        private string runtimePath = String.Empty;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string fileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string procedureName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr module);

        [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Ansi)]
        private delegate int OpenDelegate(
            out IntPtr handle,
            string name,
            IntPtr window,
            uint eventId,
            IntPtr eventHandle,
            uint configIndex);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CloseDelegate(IntPtr handle);

        private OpenDelegate open;
        private CloseDelegate close;

        internal string LastError { get { return lastError; } }
        internal string RuntimePath { get { return runtimePath; } }

        internal bool LoadRuntime(string path)
        {
            if (runtime != IntPtr.Zero && String.Equals(runtimePath, path, StringComparison.OrdinalIgnoreCase))
                return true;

            UnloadRuntime();
            IntPtr candidate = LoadLibrary(path);
            if (candidate == IntPtr.Zero)
            {
                lastError = "Could not load SimConnect runtime (Win32 " + Marshal.GetLastWin32Error() + ").";
                return false;
            }

            IntPtr openAddress = GetProcAddress(candidate, "SimConnect_Open");
            IntPtr closeAddress = GetProcAddress(candidate, "SimConnect_Close");
            if (openAddress == IntPtr.Zero || closeAddress == IntPtr.Zero)
            {
                FreeLibrary(candidate);
                lastError = "A SimConnect DLL was found, but it is not the native runtime.";
                return false;
            }

            runtime = candidate;
            runtimePath = path;
            open = (OpenDelegate)Marshal.GetDelegateForFunctionPointer(openAddress, typeof(OpenDelegate));
            close = (CloseDelegate)Marshal.GetDelegateForFunctionPointer(closeAddress, typeof(CloseDelegate));
            lastError = String.Empty;
            return true;
        }

        internal bool Connect()
        {
            if (connection != IntPtr.Zero) return true;
            if (runtime == IntPtr.Zero || open == null)
            {
                lastError = "SimConnect runtime not found.";
                return false;
            }

            try
            {
                IntPtr handle;
                int result = open(out handle, "Nocturne MSFS Memory Helper", IntPtr.Zero, 0, IntPtr.Zero, 0);
                if (result >= 0 && handle != IntPtr.Zero)
                {
                    connection = handle;
                    lastError = String.Empty;
                    return true;
                }
                lastError = "SimConnect_Open returned 0x" + result.ToString("X8");
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }
            return false;
        }

        internal void Disconnect()
        {
            if (connection == IntPtr.Zero) return;
            try
            {
                if (close != null) close(connection);
            }
            catch { }
            connection = IntPtr.Zero;
        }

        internal void UnloadRuntime()
        {
            Disconnect();
            open = null;
            close = null;
            runtimePath = String.Empty;
            if (runtime != IntPtr.Zero)
            {
                try { FreeLibrary(runtime); }
                catch { }
            }
            runtime = IntPtr.Zero;
        }

        public void Dispose()
        {
            UnloadRuntime();
        }
    }
}
