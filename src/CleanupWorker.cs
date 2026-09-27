using System;
using System.Threading;

namespace NocturneMemoryHelper
{
    internal static class CleanupWorker
    {
        private sealed class Operation
        {
            internal readonly int Command;
            internal readonly string Name;

            internal Operation(int command, string name)
            {
                Command = command;
                Name = name;
            }
        }

        private static readonly Operation[] Operations =
        {
            new Operation(2, "Working sets"),
            new Operation(3, "Modified list"),
            new Operation(4, "Standby list"),
            new Operation(5, "Priority-0 standby")
        };

        internal static int Run()
        {
            Paths.EnsureDataDirectory();
            WorkerStatus status = JsonFiles.Read<WorkerStatus>(Paths.WorkerStatusPath);

            if (!IsApprovedRequest(status))
            {
                Logger.Write("Cleanup worker refused a missing, stale, or unapproved request.");
                return 2;
            }

            int step = 0;
            try
            {
                NativeCleaner.EnableMemoryManagementPrivilege();

                for (int pass = 1; pass <= 2; pass++)
                {
                    for (int i = 0; i < Operations.Length; i++)
                    {
                        step++;
                        status.Running = true;
                        status.Step = step;
                        status.Action = "Pass " + pass + "/2 - " + Operations[i].Name;
                        status.Succeeded = null;
                        status.Message = String.Empty;
                        status.UpdatedAt = DateTime.UtcNow;
                        JsonFiles.Write(Paths.WorkerStatusPath, status);
                        NativeCleaner.RunMemoryListCommand(Operations[i].Command);
                    }

                    Logger.Write("Completed native full-system cleanup pass " + pass + "/2.");

                    if (pass == 1)
                    {
                        step++;
                        status.Running = true;
                        status.Step = step;
                        status.Action = "Settling memory";
                        status.UpdatedAt = DateTime.UtcNow;
                        JsonFiles.Write(Paths.WorkerStatusPath, status);
                        Thread.Sleep(2000);
                    }
                }

                string message = "Completed two native full-system cleanup passes.";
                Logger.Write(message);
                status.Running = false;
                status.Step = 9;
                status.Action = "Complete";
                status.Succeeded = true;
                status.Message = message;
                status.GateApproved = false;
                status.UpdatedAt = DateTime.UtcNow;
                JsonFiles.Write(Paths.WorkerStatusPath, status);
                return 0;
            }
            catch (Exception ex)
            {
                Logger.Write("Cleanup failed: " + ex.Message);
                status.Running = false;
                status.Step = step;
                status.Action = "Failed";
                status.Succeeded = false;
                status.Message = ex.Message;
                status.GateApproved = false;
                status.UpdatedAt = DateTime.UtcNow;
                JsonFiles.Write(Paths.WorkerStatusPath, status);
                return 1;
            }
        }

        private static bool IsApprovedRequest(WorkerStatus status)
        {
            if (status == null || !status.Running || !status.GateApproved) return false;
            if (String.IsNullOrWhiteSpace(status.RunId)) return false;
            TimeSpan age = DateTime.UtcNow - status.UpdatedAt.ToUniversalTime();
            return age.TotalSeconds >= -5 && age.TotalMinutes <= 2;
        }
    }
}
