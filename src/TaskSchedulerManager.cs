using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows.Forms;

namespace NocturneMemoryHelper
{
    internal static class TaskSchedulerManager
    {
        internal const string WorkerTaskName = "Nocturne Memory Cleaner - Elevated Native Worker";
        internal const string WatcherTaskName = "Nocturne MSFS Memory Helper - MSFS Watcher";
        private const string LegacyTaskName = "Nocturne Memory Cleaner - Elevated RAMMap Worker";

        private const int TaskActionExec = 0;
        private const int TaskTriggerLogon = 9;
        private const int TaskCreateOrUpdate = 6;
        private const int TaskLogonInteractiveToken = 3;
        private const int TaskRunLevelHighest = 1;
        private const int TaskInstancesIgnoreNew = 2;

        internal static bool IsAuthorized(bool requireWatcher)
        {
            try
            {
                if (!TaskMatches(WorkerTaskName, "--clean")) return false;
                return !requireWatcher || TaskMatches(WatcherTaskName, "--watch");
            }
            catch { return false; }
        }

        internal static bool EnsureAuthorized(bool requireWatcher, IWin32Window owner)
        {
            if (IsAuthorized(requireWatcher)) return true;

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = Paths.ExecutablePath;
                startInfo.Arguments = "--authorize --quiet";
                startInfo.WorkingDirectory = Paths.ApplicationDirectory;
                startInfo.UseShellExecute = true;
                startInfo.Verb = "runas";

                using (Process process = Process.Start(startInfo))
                {
                    process.WaitForExit();
                    if (process.ExitCode != 0 || !IsAuthorized(requireWatcher))
                        throw new InvalidOperationException("Authorization was not completed.");
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Write("Self-authorization failed: " + ex.Message);
                MessageBox.Show(
                    owner,
                    "Authorization failed:\r\n\r\n" + ex.Message,
                    Program.AppName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }
        }

        internal static void RegisterTasks()
        {
            string userId = WindowsIdentity.GetCurrent().Name;
            dynamic service = CreateService();
            dynamic folder = service.GetFolder("\\");

            dynamic workerDefinition = CreateBaseDefinition(service, userId, "PT5M");
            workerDefinition.RegistrationInfo.Description = "Runs NMMH's two-pass native memory cleanup after the SimConnect safety gate approves it.";
            dynamic workerAction = workerDefinition.Actions.Create(TaskActionExec);
            workerAction.Path = Paths.ExecutablePath;
            workerAction.Arguments = "--clean";
            workerAction.WorkingDirectory = Paths.ApplicationDirectory;
            folder.RegisterTaskDefinition(
                WorkerTaskName,
                workerDefinition,
                TaskCreateOrUpdate,
                userId,
                null,
                TaskLogonInteractiveToken,
                null);

            dynamic watcherDefinition = CreateBaseDefinition(service, userId, "PT0S");
            watcherDefinition.RegistrationInfo.Description = "Starts NMMH when Microsoft Flight Simulator starts, when enabled in NMMH settings.";
            dynamic watcherTrigger = watcherDefinition.Triggers.Create(TaskTriggerLogon);
            watcherTrigger.UserId = userId;
            dynamic watcherAction = watcherDefinition.Actions.Create(TaskActionExec);
            watcherAction.Path = Paths.ExecutablePath;
            watcherAction.Arguments = "--watch";
            watcherAction.WorkingDirectory = Paths.ApplicationDirectory;
            folder.RegisterTaskDefinition(
                WatcherTaskName,
                watcherDefinition,
                TaskCreateOrUpdate,
                userId,
                null,
                TaskLogonInteractiveToken,
                null);

            try { folder.DeleteTask(LegacyTaskName, 0); }
            catch { }
        }

        internal static void RunWorkerTask()
        {
            dynamic service = CreateService();
            dynamic task = service.GetFolder("\\").GetTask(WorkerTaskName);
            task.Run(null);
        }

        internal static void StartWatcherTask()
        {
            dynamic service = CreateService();
            dynamic task = service.GetFolder("\\").GetTask(WatcherTaskName);
            task.Run(null);
        }

        internal static void StopWatcherTask()
        {
            try
            {
                dynamic service = CreateService();
                dynamic task = service.GetFolder("\\").GetTask(WatcherTaskName);
                task.Stop(0);
            }
            catch { }
        }

        private static dynamic CreateBaseDefinition(dynamic service, string userId, string executionTimeLimit)
        {
            dynamic definition = service.NewTask(0);
            definition.Settings.Enabled = true;
            definition.Settings.AllowDemandStart = true;
            definition.Settings.DisallowStartIfOnBatteries = false;
            definition.Settings.StopIfGoingOnBatteries = false;
            definition.Settings.ExecutionTimeLimit = executionTimeLimit;
            definition.Settings.MultipleInstances = TaskInstancesIgnoreNew;
            definition.Principal.UserId = userId;
            definition.Principal.LogonType = TaskLogonInteractiveToken;
            definition.Principal.RunLevel = TaskRunLevelHighest;
            return definition;
        }

        private static dynamic CreateService()
        {
            Type serviceType = Type.GetTypeFromProgID("Schedule.Service");
            if (serviceType == null) throw new InvalidOperationException("Windows Task Scheduler is unavailable.");
            dynamic service = Activator.CreateInstance(serviceType);
            service.Connect();
            return service;
        }

        private static bool TaskMatches(string taskName, string expectedArguments)
        {
            dynamic service = CreateService();
            dynamic task = service.GetFolder("\\").GetTask(taskName);
            dynamic actions = task.Definition.Actions;

            for (int i = 1; i <= actions.Count; i++)
            {
                dynamic action = actions.Item(i);
                string path = Convert.ToString(action.Path);
                string arguments = Convert.ToString(action.Arguments).Trim();
                if (String.Equals(path, Paths.ExecutablePath, StringComparison.OrdinalIgnoreCase) &&
                    String.Equals(arguments, expectedArguments, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }

    internal static class AuthorizationMode
    {
        internal static int Run(bool quiet)
        {
            try
            {
                WindowsPrincipal principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
                if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                    throw new InvalidOperationException("Administrator approval is required to authorize the cleanup worker.");

                TaskSchedulerManager.RegisterTasks();
                Logger.Write("Authorization complete for the native worker and MSFS watcher.");

                if (!quiet)
                {
                    MessageBox.Show(
                        "Authorization complete.\r\n\r\nNMMH can now clean memory and start with MSFS without recurring UAC prompts.",
                        Program.AppName,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                return 0;
            }
            catch (Exception ex)
            {
                Logger.Write("Authorization failed: " + ex.Message);
                if (!quiet)
                {
                    MessageBox.Show(
                        "Authorization failed:\r\n\r\n" + ex.Message,
                        Program.AppName,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                return 1;
            }
        }
    }
}
