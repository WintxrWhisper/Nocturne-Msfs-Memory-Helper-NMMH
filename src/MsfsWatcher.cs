using System;
using System.Diagnostics;
using System.Threading;

namespace NocturneMemoryHelper
{
    internal static class MsfsWatcher
    {
        internal static int Run()
        {
            try
            {
                while (StartWithMsfsEnabled())
                {
                    if (Program.IsSimulatorRunning())
                    {
                        if (!Program.IsTrayApplicationRunning()) StartTrayApplication();

                        while (StartWithMsfsEnabled() && Program.IsSimulatorRunning())
                            Thread.Sleep(3000);
                    }
                    Thread.Sleep(3000);
                }
                return 0;
            }
            catch (Exception ex)
            {
                Logger.Write("MSFS watcher failed: " + ex.Message);
                return 1;
            }
        }

        private static bool StartWithMsfsEnabled()
        {
            AppSettings settings = JsonFiles.Read<AppSettings>(Paths.SettingsPath);
            return settings != null && settings.StartWithMSFS;
        }

        private static void StartTrayApplication()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = Paths.ExecutablePath;
            startInfo.Arguments = "--started-by-watcher";
            startInfo.WorkingDirectory = Paths.ApplicationDirectory;
            startInfo.UseShellExecute = true;
            Process.Start(startInfo);
        }
    }
}
