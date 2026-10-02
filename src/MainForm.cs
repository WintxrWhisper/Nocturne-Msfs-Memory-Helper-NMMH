using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace NocturneMemoryHelper
{
    internal sealed class MainForm : Form
    {
        private readonly AppSettings settings;
        private readonly SimConnectProbe simConnect = new SimConnectProbe();
        private readonly Timer uiTimer = new Timer();
        private readonly NotifyIcon trayIcon = new NotifyIcon();
        private readonly ToolStripMenuItem trayStatusItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem cleanItem = new ToolStripMenuItem("Clean now");
        private readonly ToolStripMenuItem automaticItem = new ToolStripMenuItem("Automatic cleaning enabled");
        private readonly ToolStripMenuItem intervalMenu = new ToolStripMenuItem("Cleaning interval");
        private readonly ToolStripMenuItem startWithMsfsItem = new ToolStripMenuItem("Start with MSFS");
        private readonly ToolStripMenuItem closeWithMsfsItem = new ToolStripMenuItem("Close with MSFS");

        private readonly Label connectionLabel = new Label();
        private readonly Label activeValue = new Label();
        private readonly Label modifiedValue = new Label();
        private readonly Label standbyValue = new Label();
        private readonly Label priorityZeroValue = new Label();
        private readonly Label availableValue = new Label();
        private readonly Label countdownLabel = new Label();
        private readonly Label progressLabel = new Label();
        private readonly CheckBox startWithMsfsCheck = new CheckBox();
        private readonly CheckBox closeWithMsfsCheck = new CheckBox();
        private readonly Button cleanButton = new Button();
        private readonly Button startStopButton = new Button();

        private PerformanceCounter modifiedCounter;
        private PerformanceCounter standbyCoreCounter;
        private PerformanceCounter standbyNormalCounter;
        private PerformanceCounter standbyReserveCounter;

        private bool allowExit;
        private bool isCleaning;
        private string currentRunId = String.Empty;
        private string lastCompletedRunId = String.Empty;
        private DateTime? cleanupStartedAt;
        private int wipesThisSession;
        private DateTime? nextCleanupAt;
        private bool simConnected;
        private string lastSimConnectError = String.Empty;
        private bool hasSeenSimulator;
        private DateTime? simulatorAbsentSince;
        private Icon applicationIcon;
        private Image brandImage;

        internal MainForm()
        {
            settings = AppSettings.Load();
            settings.Save();

            InitializeWindow();
            InitializeMemoryCounters();
            SetAutomaticCleaning(false);
            ResetCountdown();

            uiTimer.Interval = 1000;
            uiTimer.Tick += delegate { UpdateDashboard(); };
            uiTimer.Start();

            UpdateDashboard();
            ShowDashboard();
        }

        private void InitializeWindow()
        {
            Text = Program.AppName;
            ClientSize = new Size(560, 468);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(25, 27, 34);
            ForeColor = Color.White;
            ShowInTaskbar = true;
            ShowIcon = true;

            try
            {
                applicationIcon = Icon.ExtractAssociatedIcon(Paths.ExecutablePath);
                if (applicationIcon != null)
                {
                    Icon = applicationIcon;
                    brandImage = applicationIcon.ToBitmap();
                }
            }
            catch (Exception ex)
            {
                Logger.Write("Could not load the embedded application icon: " + ex.Message);
            }

            PictureBox brand = new PictureBox();
            brand.Location = new Point(20, 14);
            brand.Size = new Size(56, 56);
            brand.SizeMode = PictureBoxSizeMode.Zoom;
            brand.Image = brandImage;
            Controls.Add(brand);

            Label title = new Label();
            title.Text = "NOCTURNE MSFS MEMORY HELPER";
            title.Location = new Point(88, 18);
            title.Size = new Size(445, 28);
            title.Font = new Font("Segoe UI Semibold", 14);
            title.ForeColor = Color.FromArgb(235, 65, 72);
            Controls.Add(title);

            connectionLabel.Location = new Point(90, 53);
            connectionLabel.Size = new Size(443, 24);
            connectionLabel.Font = new Font("Segoe UI Semibold", 10);
            Controls.Add(connectionLabel);

            GroupBox memoryGroup = new GroupBox();
            memoryGroup.Text = "Live physical memory";
            memoryGroup.Location = new Point(20, 84);
            memoryGroup.Size = new Size(520, 190);
            memoryGroup.ForeColor = Color.FromArgb(200, 205, 215);
            Controls.Add(memoryGroup);

            TableLayoutPanel memoryTable = new TableLayoutPanel();
            memoryTable.Location = new Point(16, 24);
            memoryTable.Size = new Size(485, 152);
            memoryTable.ColumnCount = 2;
            memoryTable.RowCount = 5;
            memoryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            memoryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            memoryGroup.Controls.Add(memoryTable);

            AddValueRow(memoryTable, 0, "Active / in use", activeValue);
            AddValueRow(memoryTable, 1, "Modified page list", modifiedValue);
            AddValueRow(memoryTable, 2, "Standby list", standbyValue);
            AddValueRow(memoryTable, 3, "Priority-0 standby", priorityZeroValue);
            AddValueRow(memoryTable, 4, "Immediately available", availableValue);

            countdownLabel.Location = new Point(23, 290);
            countdownLabel.Size = new Size(315, 30);
            countdownLabel.Font = new Font("Segoe UI Semibold", 12);
            Controls.Add(countdownLabel);

            progressLabel.Location = new Point(332, 290);
            progressLabel.Size = new Size(205, 30);
            progressLabel.TextAlign = ContentAlignment.MiddleRight;
            progressLabel.ForeColor = Color.FromArgb(205, 210, 220);
            Controls.Add(progressLabel);

            startWithMsfsCheck.Text = "Start with MSFS";
            startWithMsfsCheck.Location = new Point(24, 330);
            startWithMsfsCheck.Size = new Size(180, 26);
            startWithMsfsCheck.Checked = settings.StartWithMSFS;
            startWithMsfsCheck.Click += delegate { SetStartWithMsfs(startWithMsfsCheck.Checked); };
            Controls.Add(startWithMsfsCheck);

            closeWithMsfsCheck.Text = "Close with MSFS";
            closeWithMsfsCheck.Location = new Point(218, 330);
            closeWithMsfsCheck.Size = new Size(190, 26);
            closeWithMsfsCheck.Checked = settings.CloseWithMSFS;
            closeWithMsfsCheck.Click += delegate { SetCloseWithMsfs(closeWithMsfsCheck.Checked); };
            Controls.Add(closeWithMsfsCheck);

            cleanButton.Text = "Clean now";
            cleanButton.Location = new Point(22, 400);
            cleanButton.Size = new Size(130, 38);
            cleanButton.FlatStyle = FlatStyle.Flat;
            cleanButton.Click += delegate { InvokeCleanup(false); };
            Controls.Add(cleanButton);

            startStopButton.Location = new Point(162, 400);
            startStopButton.Size = new Size(220, 38);
            startStopButton.FlatStyle = FlatStyle.Flat;
            startStopButton.Click += delegate { SetAutomaticCleaning(!settings.AutomaticCleaningEnabled); };
            Controls.Add(startStopButton);

            Button hideButton = new Button();
            hideButton.Text = "Hide";
            hideButton.Location = new Point(392, 400);
            hideButton.Size = new Size(145, 38);
            hideButton.FlatStyle = FlatStyle.Flat;
            hideButton.Click += delegate { Hide(); };
            Controls.Add(hideButton);

            InitializeTrayMenu();
        }

        private void InitializeTrayMenu()
        {
            trayIcon.Icon = applicationIcon ?? SystemIcons.Shield;
            trayIcon.Visible = true;
            trayIcon.Text = "Nocturne MSFS Memory Helper";

            ContextMenuStrip menu = new ContextMenuStrip();
            trayStatusItem.Enabled = false;
            menu.Items.Add(trayStatusItem);
            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem openItem = new ToolStripMenuItem("Open status window");
            openItem.Click += delegate { ShowDashboard(); };
            menu.Items.Add(openItem);

            cleanItem.Click += delegate { InvokeCleanup(false); };
            menu.Items.Add(cleanItem);

            automaticItem.Checked = settings.AutomaticCleaningEnabled;
            automaticItem.Click += delegate { SetAutomaticCleaning(!settings.AutomaticCleaningEnabled); };
            menu.Items.Add(automaticItem);

            int[] intervals = { 5, 10, 15, 30, 45, 60, 90, 120 };
            for (int i = 0; i < intervals.Length; i++)
            {
                ToolStripMenuItem item = new ToolStripMenuItem(intervals[i] + " minutes");
                item.Tag = intervals[i];
                item.Checked = settings.IntervalMinutes == intervals[i];
                item.Click += delegate(object sender, EventArgs args)
                {
                    ToolStripMenuItem clicked = (ToolStripMenuItem)sender;
                    SetCleaningInterval((int)clicked.Tag);
                };
                intervalMenu.DropDownItems.Add(item);
            }
            intervalMenu.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem customItem = new ToolStripMenuItem("Custom...");
            customItem.Click += delegate
            {
                string answer = PromptDialog.Show(
                    this,
                    "How many minutes between cleanups? (1-1440)",
                    Program.AppName,
                    settings.IntervalMinutes.ToString());
                int minutes;
                if (Int32.TryParse(answer, out minutes)) SetCleaningInterval(minutes);
            };
            intervalMenu.DropDownItems.Add(customItem);
            menu.Items.Add(intervalMenu);

            startWithMsfsItem.Checked = settings.StartWithMSFS;
            startWithMsfsItem.Click += delegate { SetStartWithMsfs(!settings.StartWithMSFS); };
            menu.Items.Add(startWithMsfsItem);

            closeWithMsfsItem.Checked = settings.CloseWithMSFS;
            closeWithMsfsItem.Click += delegate { SetCloseWithMsfs(!settings.CloseWithMSFS); };
            menu.Items.Add(closeWithMsfsItem);
            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem logItem = new ToolStripMenuItem("Open cleanup log");
            logItem.Click += delegate { OpenCleanupLog(); };
            menu.Items.Add(logItem);

            ToolStripMenuItem exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += delegate { ExitNmmh(); };
            menu.Items.Add(exitItem);

            trayIcon.ContextMenuStrip = menu;
            trayIcon.DoubleClick += delegate { ShowDashboard(); };
        }

        private static void AddValueRow(TableLayoutPanel panel, int row, string name, Label valueLabel)
        {
            Label nameLabel = new Label();
            nameLabel.Text = name;
            nameLabel.AutoSize = true;
            nameLabel.ForeColor = Color.FromArgb(180, 185, 196);
            nameLabel.Margin = new Padding(0, 6, 8, 6);
            panel.Controls.Add(nameLabel, 0, row);

            valueLabel.Text = "-";
            valueLabel.AutoSize = true;
            valueLabel.ForeColor = Color.White;
            valueLabel.Font = new Font("Segoe UI Semibold", 10);
            valueLabel.Margin = new Padding(0, 5, 0, 6);
            panel.Controls.Add(valueLabel, 1, row);
        }

        private void InitializeMemoryCounters()
        {
            try
            {
                modifiedCounter = new PerformanceCounter("Memory", "Modified Page List Bytes", true);
                standbyCoreCounter = new PerformanceCounter("Memory", "Standby Cache Core Bytes", true);
                standbyNormalCounter = new PerformanceCounter("Memory", "Standby Cache Normal Priority Bytes", true);
                standbyReserveCounter = new PerformanceCounter("Memory", "Standby Cache Reserve Bytes", true);
            }
            catch (Exception ex)
            {
                DisposeMemoryCounters();
                Logger.Write("Detailed memory-counter fallback unavailable: " + ex.Message);
            }
        }

        private void UpdateDashboard()
        {
            UpdateSimConnectState();
            bool simulatorRunning = Program.IsSimulatorRunning();

            if (simulatorRunning)
            {
                hasSeenSimulator = true;
                simulatorAbsentSince = null;
            }
            else if (hasSeenSimulator)
            {
                if (!simulatorAbsentSince.HasValue) simulatorAbsentSince = DateTime.Now;
                else if (settings.CloseWithMSFS && (DateTime.Now - simulatorAbsentSince.Value).TotalSeconds >= 5)
                {
                    Logger.Write("MSFS closed; Close with MSFS is enabled.");
                    ExitNmmh();
                    return;
                }
            }

            MemorySnapshot snapshot = NativeMemory.Read();
            activeValue.Text = FormatBytes(snapshot.ActiveBytes);

            if (snapshot.DetailedDataAvailable)
            {
                modifiedValue.Text = FormatBytes(snapshot.ModifiedBytes);
                standbyValue.Text = FormatBytes(snapshot.StandbyBytes);
                priorityZeroValue.Text = FormatBytes(snapshot.PriorityZeroStandbyBytes);
            }
            else if (modifiedCounter != null)
            {
                try
                {
                    ulong modified = unchecked((ulong)modifiedCounter.NextSample().RawValue);
                    ulong core = unchecked((ulong)standbyCoreCounter.NextSample().RawValue);
                    ulong normal = unchecked((ulong)standbyNormalCounter.NextSample().RawValue);
                    ulong reserve = unchecked((ulong)standbyReserveCounter.NextSample().RawValue);
                    modifiedValue.Text = FormatBytes(modified);
                    standbyValue.Text = FormatBytes(core + normal + reserve);
                    priorityZeroValue.Text = FormatBytes(reserve) + " approx.";
                }
                catch
                {
                    SetDetailedMemoryUnavailable();
                }
            }
            else SetDetailedMemoryUnavailable();

            availableValue.Text = FormatBytes(snapshot.AvailableBytes) + "  (" + snapshot.MemoryLoadPercent + "% used)";

            if (simConnected)
            {
                connectionLabel.Text = "[+] SimConnect connected";
                connectionLabel.ForeColor = Color.FromArgb(100, 220, 145);
            }
            else if (simulatorRunning)
            {
                connectionLabel.Text = String.IsNullOrEmpty(simConnect.LastError)
                    ? "[!] MSFS detected - waiting for SimConnect"
                    : "[!] MSFS detected - " + simConnect.LastError;
                connectionLabel.ForeColor = Color.FromArgb(240, 190, 85);
            }
            else
            {
                connectionLabel.Text = "[-] Waiting for Microsoft Flight Simulator";
                connectionLabel.ForeColor = Color.FromArgb(145, 150, 165);
            }

            bool cleaningAllowed = simConnected && !isCleaning;
            cleanButton.Enabled = cleaningAllowed;
            cleanItem.Enabled = cleaningAllowed;
            UpdateWorkerProgress();
            UpdateAutomaticCountdown();
            UpdateTrayState();
        }

        private void UpdateSimConnectState()
        {
            bool previouslyConnected = simConnected;
            using (Process simulator = Program.FindSimulatorProcess())
                simConnect.Update(simulator == null ? 0 : simulator.Id);
            simConnected = simConnect.IsConnected;
            if (simConnected != previouslyConnected)
                Logger.Write(simConnected
                    ? "SimConnect connected directly (no external runtime)."
                    : "SimConnect disconnected; cleaning locked.");
            string error = simConnect.LastError;
            if (!String.Equals(error, lastSimConnectError, StringComparison.Ordinal))
            {
                lastSimConnectError = error;
                if (!String.IsNullOrEmpty(error)) Logger.Write(error);
            }
        }

        private void InvokeCleanup(bool scheduled)
        {
            if (isCleaning || (scheduled && !settings.AutomaticCleaningEnabled)) return;
            UpdateSimConnectState();
            if (!simConnected)
            {
                nextCleanupAt = null;
                Logger.Write("Cleanup blocked: SimConnect is not connected.");
                if (!scheduled) ShowTrayMessage("Cleaning is locked until SimConnect is connected.", ToolTipIcon.Warning);
                return;
            }

            try
            {
                if (!TaskSchedulerManager.EnsureAuthorized(false, this))
                    throw new InvalidOperationException("The elevated cleanup worker is not authorized.");

                // UAC can keep the dialog open while MSFS exits. Recheck the
                // actual connection after authorization, before approving work.
                UpdateSimConnectState();
                if (!simConnected)
                    throw new InvalidOperationException("SimConnect disconnected; cleanup was not started.");

                currentRunId = Guid.NewGuid().ToString("N");
                cleanupStartedAt = DateTime.Now;
                isCleaning = true;
                nextCleanupAt = null;

                WorkerStatus initialStatus = new WorkerStatus();
                initialStatus.RunId = currentRunId;
                initialStatus.Running = true;
                initialStatus.Step = 0;
                initialStatus.Total = 9;
                initialStatus.Action = "Starting worker";
                initialStatus.Succeeded = null;
                initialStatus.Message = String.Empty;
                initialStatus.UpdatedAt = DateTime.UtcNow;
                initialStatus.GateApproved = true;
                JsonFiles.Write(Paths.WorkerStatusPath, initialStatus);

                TaskSchedulerManager.RunWorkerTask();
                Logger.Write("Requested elevated cleanup worker. RunId=" + currentRunId);
            }
            catch (Exception ex)
            {
                isCleaning = false;
                WorkerStatus failedStatus = JsonFiles.Read<WorkerStatus>(Paths.WorkerStatusPath);
                if (failedStatus != null && String.Equals(failedStatus.RunId, currentRunId, StringComparison.Ordinal))
                {
                    failedStatus.Running = false;
                    failedStatus.Succeeded = false;
                    failedStatus.Action = "Failed";
                    failedStatus.Message = ex.Message;
                    failedStatus.GateApproved = false;
                    failedStatus.UpdatedAt = DateTime.UtcNow;
                    JsonFiles.Write(Paths.WorkerStatusPath, failedStatus);
                }
                Logger.Write("Cleanup failed: " + ex.Message);
                ShowTrayMessage("Cleanup failed: " + ex.Message, ToolTipIcon.Error);
                ResetCountdown();
            }
        }

        private void UpdateWorkerProgress()
        {
            if (isCleaning)
            {
                WorkerStatus status = JsonFiles.Read<WorkerStatus>(Paths.WorkerStatusPath);
                if (status != null && String.Equals(status.RunId, currentRunId, StringComparison.Ordinal))
                {
                    if (status.Running)
                    {
                        progressLabel.Text = "Wipe " + status.Step + "/" + status.Total + ": " + status.Action;
                    }
                    else
                    {
                        if (!String.Equals(lastCompletedRunId, currentRunId, StringComparison.Ordinal))
                        {
                            lastCompletedRunId = currentRunId;
                            if (status.Succeeded == true) wipesThisSession++;
                            else ShowTrayMessage("Cleanup failed: " + status.Message, ToolTipIcon.Error);
                        }
                        isCleaning = false;
                        ResetCountdown();
                    }
                }
                else if (cleanupStartedAt.HasValue && (DateTime.Now - cleanupStartedAt.Value).TotalMinutes > 5)
                {
                    isCleaning = false;
                    Logger.Write("Cleanup worker timed out without reporting completion.");
                    ResetCountdown();
                }
            }

            if (!isCleaning) progressLabel.Text = "Wipes this session: " + wipesThisSession;
        }

        private void UpdateAutomaticCountdown()
        {
            bool canSchedule = settings.AutomaticCleaningEnabled && simConnected;
            if (!settings.AutomaticCleaningEnabled)
            {
                countdownLabel.Text = "Automatic cleaning stopped";
                nextCleanupAt = null;
            }
            else if (!canSchedule)
            {
                countdownLabel.Text = "Next wipe: waiting for SimConnect";
                nextCleanupAt = null;
            }
            else if (isCleaning)
            {
                countdownLabel.Text = "Cleaning now...";
            }
            else
            {
                if (!nextCleanupAt.HasValue) ResetCountdown();
                TimeSpan remaining = nextCleanupAt.Value - DateTime.Now;
                if (remaining.TotalSeconds <= 0) InvokeCleanup(true);
                else
                {
                    int hours = (int)Math.Floor(remaining.TotalHours);
                    countdownLabel.Text = "Next wipe: " + hours.ToString("00") + ":" +
                                          remaining.Minutes.ToString("00") + ":" +
                                          remaining.Seconds.ToString("00");
                }
            }
        }

        private void UpdateTrayState()
        {
            string state = simConnected ? "SimConnect connected" : "Waiting for SimConnect";
            if (!settings.AutomaticCleaningEnabled) state = "Automatic cleaning stopped";
            if (isCleaning) state = "Cleaning memory";
            trayStatusItem.Text = state;
            string tip = "NMMH - " + state;
            trayIcon.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
        }

        private void SetCleaningInterval(int minutes)
        {
            if (minutes < 1 || minutes > 1440)
            {
                MessageBox.Show(
                    this,
                    "Choose an interval from 1 to 1440 minutes.",
                    Program.AppName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            settings.IntervalMinutes = minutes;
            settings.Save();
            ResetCountdown();

            foreach (ToolStripItem item in intervalMenu.DropDownItems)
            {
                ToolStripMenuItem menuItem = item as ToolStripMenuItem;
                if (menuItem != null && menuItem.Tag is int)
                    menuItem.Checked = (int)menuItem.Tag == minutes;
            }
        }

        private void SetAutomaticCleaning(bool enabled)
        {
            if (enabled && !TaskSchedulerManager.EnsureAuthorized(false, this))
            {
                enabled = false;
                ShowTrayMessage("Automatic cleaning remains off until authorization succeeds.", ToolTipIcon.Warning);
            }

            settings.AutomaticCleaningEnabled = enabled;
            automaticItem.Checked = enabled;
            startStopButton.Text = enabled ? "Stop automatic cleaning" : "Start automatic cleaning";
            settings.Save();
            ResetCountdown();
        }

        private void SetStartWithMsfs(bool enabled)
        {
            if (enabled && !TaskSchedulerManager.EnsureAuthorized(true, this))
            {
                startWithMsfsCheck.Checked = false;
                startWithMsfsItem.Checked = false;
                return;
            }

            settings.StartWithMSFS = enabled;
            startWithMsfsCheck.Checked = enabled;
            startWithMsfsItem.Checked = enabled;
            settings.Save();

            try
            {
                if (enabled) TaskSchedulerManager.StartWatcherTask();
                else TaskSchedulerManager.StopWatcherTask();
            }
            catch (Exception ex)
            {
                Logger.Write("Could not update MSFS watcher: " + ex.Message);
            }
        }

        private void SetCloseWithMsfs(bool enabled)
        {
            settings.CloseWithMSFS = enabled;
            closeWithMsfsCheck.Checked = enabled;
            closeWithMsfsItem.Checked = enabled;
            settings.Save();
        }

        private void ResetCountdown()
        {
            nextCleanupAt = settings.AutomaticCleaningEnabled
                ? DateTime.Now.AddMinutes(settings.IntervalMinutes)
                : (DateTime?)null;
        }

        private void OpenCleanupLog()
        {
            if (!File.Exists(Paths.LogPath)) Logger.Write("Log created.");
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "notepad.exe";
            startInfo.Arguments = "\"" + Paths.LogPath + "\"";
            startInfo.UseShellExecute = true;
            Process.Start(startInfo);
        }

        private void ShowTrayMessage(string text, ToolTipIcon icon)
        {
            trayIcon.BalloonTipTitle = Program.AppName;
            trayIcon.BalloonTipText = text;
            trayIcon.BalloonTipIcon = icon;
            trayIcon.ShowBalloonTip(4000);
        }

        private void ShowDashboard()
        {
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
        }

        private void ExitNmmh()
        {
            allowExit = true;
            uiTimer.Stop();
            simConnect.Disconnect();
            trayIcon.Visible = false;
            Close();
        }

        private void SetDetailedMemoryUnavailable()
        {
            modifiedValue.Text = "Unavailable";
            standbyValue.Text = "Unavailable";
            priorityZeroValue.Text = "Unavailable";
        }

        private static string FormatBytes(ulong bytes)
        {
            const double kilobyte = 1024.0;
            const double megabyte = kilobyte * 1024.0;
            const double gigabyte = megabyte * 1024.0;
            if (bytes >= gigabyte) return (bytes / gigabyte).ToString("N2") + " GB";
            if (bytes >= megabyte) return (bytes / megabyte).ToString("N0") + " MB";
            return (bytes / kilobyte).ToString("N0") + " KB";
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!allowExit)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                uiTimer.Dispose();
                trayIcon.Dispose();
                simConnect.Dispose();
                DisposeMemoryCounters();
                if (brandImage != null) brandImage.Dispose();
                if (applicationIcon != null) applicationIcon.Dispose();
            }
            base.Dispose(disposing);
        }

        private void DisposeMemoryCounters()
        {
            if (modifiedCounter != null) { modifiedCounter.Dispose(); modifiedCounter = null; }
            if (standbyCoreCounter != null) { standbyCoreCounter.Dispose(); standbyCoreCounter = null; }
            if (standbyNormalCounter != null) { standbyNormalCounter.Dispose(); standbyNormalCounter = null; }
            if (standbyReserveCounter != null) { standbyReserveCounter.Dispose(); standbyReserveCounter = null; }
        }
    }

    internal static class PromptDialog
    {
        internal static string Show(IWin32Window owner, string prompt, string title, string defaultValue)
        {
            using (Form dialog = new Form())
            using (Label label = new Label())
            using (TextBox input = new TextBox())
            using (Button ok = new Button())
            using (Button cancel = new Button())
            {
                dialog.Text = title;
                dialog.ClientSize = new Size(420, 130);
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowInTaskbar = false;

                label.Text = prompt;
                label.Location = new Point(12, 12);
                label.Size = new Size(396, 24);
                input.Text = defaultValue;
                input.Location = new Point(15, 42);
                input.Size = new Size(390, 23);

                ok.Text = "OK";
                ok.DialogResult = DialogResult.OK;
                ok.Location = new Point(249, 83);
                ok.Size = new Size(75, 28);
                cancel.Text = "Cancel";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.Location = new Point(330, 83);
                cancel.Size = new Size(75, 28);

                dialog.Controls.Add(label);
                dialog.Controls.Add(input);
                dialog.Controls.Add(ok);
                dialog.Controls.Add(cancel);
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;

                return dialog.ShowDialog(owner) == DialogResult.OK ? input.Text : String.Empty;
            }
        }
    }
}
