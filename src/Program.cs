using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace NocturneMemoryHelper
{
    internal static class Program
    {
        internal const string AppName = "Nocturne MSFS Memory Helper 1.5.3 RC";
        internal const string MutexName = "Local\\NocturneMSFSMemoryHelper.UI";

        [STAThread]
        private static int Main(string[] args)
        {
            string mode = args.Length == 0 ? String.Empty : args[0].ToLowerInvariant();

            try
            {
                if (mode == "--clean") return CleanupWorker.Run();
                if (mode == "--watch") return MsfsWatcher.Run();
                if (mode == "--authorize")
                {
                    bool quiet = HasArgument(args, "--quiet");
                    return AuthorizationMode.Run(quiet);
                }

                return RunTrayApplication();
            }
            catch (Exception ex)
            {
                Logger.Write("Fatal error: " + ex);
                if (mode != "--watch" && mode != "--clean")
                {
                    MessageBox.Show(
                        "NMMH could not start:\r\n\r\n" + ex.Message,
                        AppName,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                return 1;
            }
        }

        private static int RunTrayApplication()
        {
            bool createdNew;
            using (Mutex instanceMutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew) return 0;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Paths.EnsureDataDirectory();

                using (MainForm form = new MainForm())
                {
                    Application.Run(form);
                }

                try { instanceMutex.ReleaseMutex(); }
                catch (ApplicationException) { }
            }
            return 0;
        }

        internal static bool IsTrayApplicationRunning()
        {
            try
            {
                using (Mutex mutex = Mutex.OpenExisting(MutexName))
                {
                    return mutex != null;
                }
            }
            catch (WaitHandleCannotBeOpenedException) { return false; }
            catch (UnauthorizedAccessException) { return true; }
        }

        internal static bool IsSimulatorRunning()
        {
            using (Process simulator = FindSimulatorProcess())
            {
                return simulator != null;
            }
        }

        internal static Process FindSimulatorProcess()
        {
            string[] processNames = { "FlightSimulator2024", "FlightSimulator" };
            for (int n = 0; n < processNames.Length; n++)
            {
                Process[] processes = Process.GetProcessesByName(processNames[n]);
                if (processes.Length > 0)
                {
                    Process selected = processes[0];
                    for (int i = 1; i < processes.Length; i++) processes[i].Dispose();
                    return selected;
                }
            }
            return null;
        }

        internal static bool HasArgument(string[] args, string argument)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (String.Equals(args[i], argument, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }

    internal static class Paths
    {
        internal static readonly string ExecutablePath = Process.GetCurrentProcess().MainModule.FileName;
        internal static readonly string ApplicationDirectory = Path.GetDirectoryName(ExecutablePath);
        internal static readonly string DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NocturneMemoryCleaner");
        internal static readonly string SettingsPath = Path.Combine(DataDirectory, "settings.json");
        internal static readonly string LogPath = Path.Combine(DataDirectory, "cleaner.log");
        internal static readonly string WorkerStatusPath = Path.Combine(DataDirectory, "worker-status.json");

        internal static void EnsureDataDirectory()
        {
            Directory.CreateDirectory(DataDirectory);
        }
    }
}
