using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Squirrel;

namespace WindowsFormsApp
{
    public partial class MainForm : Form
    {
        private bool _updateInProgress;

        public MainForm()
        {
            InitializeComponent();
            ShowAppVersion();
            Shown += MainForm_Shown;
            FormClosing += MainForm_FormClosing;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_updateInProgress)
            {
                var dr = MessageBox.Show(
                    "The updade process has not yet finished, do you want to close anyway?\nIt is strongly recommended to wait for the updade to complete.",
                    "Update in progress...", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

                if (dr != DialogResult.Yes)
                {
                    e.Cancel = true;
                }
            }
        }

        private async void MainForm_Shown(object sender, EventArgs e)
        {
            BringToFront();
            await CheckForUpdates();
        }

        private async Task CheckForUpdates()
        {
            const string updateUrl = "https://github.com/Lukkian/Cake.StartUp";

            var activity = new Activity<bool>();
            var token = new CancellationTokenSource();
            var appUpdate = new AppUpdate();

            void MessageLogs(string msg)
            {
                TouchGui(() =>
                {
                    string RemoveDuplicate(string duplicate)
                    {
                        var duplicateIndex = updateLogTextBox.Text.IndexOf(duplicate, StringComparison.InvariantCultureIgnoreCase);
                        if (duplicateIndex > 0 && msg.Contains(duplicate))
                        {
                            var output = updateLogTextBox.Text;
                            var substring = output.Substring(0, duplicateIndex);

                            output = output.Remove(0, substring.Length);
                            var newLineIndex = output.IndexOf(Environment.NewLine, StringComparison.InvariantCultureIgnoreCase);
                            string remaining = null;
                            if (newLineIndex > 0)
                            {
                                remaining = output.Substring(newLineIndex);
                            }

                            msg = $"{substring}{msg}{remaining}".Trim();
                            updateLogTextBox.Clear();
                        }

                        return msg;
                    }

                    const string downloadingMsg = "Downloading update";
                    msg = RemoveDuplicate(downloadingMsg);

                    const string updatingMsg = "Updating";
                    msg = RemoveDuplicate(updatingMsg);

                    updateLogTextBox.AppendLine(msg);

                    switch (appUpdate.State)
                    {
                        case UpdateState.Timeout:
                            Text = "Windows Forms App - MainForm [timeout]";
                            break;
                        case UpdateState.Checking:
                            Text = "Windows Forms App - MainForm [checking-update]";
                            break;
                        case UpdateState.Downloading:
                            Text = "Windows Forms App - MainForm [downloading]";
                            break;
                    }
                });
            }

            _updateInProgress = true;
            var task = appUpdate.CheckForUpdatesOnGitHubAsync(updateUrl, MessageLogs, token, false);
            appUpdate.UpdateDone += () =>
            {
                _updateInProgress = false;
                TouchGui(() =>
                {
                    Text = "Windows Forms App - MainForm [done]";

                    if (appUpdate.State == UpdateState.Done)
                    {
                        var dr = MessageBox.Show("Update applied, do you want to restart the app?", "Restart needed", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                        if (dr == DialogResult.Yes)
                        {
                            WindowState = FormWindowState.Minimized;
                            Visible = false;
                            appUpdate.RestartApp();
                        }
                    }
                    else
                    {
                        MessageBox.Show("No update found.");
                    }
                });
            };
            appUpdate.GotReleaseNotes += args =>
            {
                TouchGui(() =>
                {
                    var notes = new StringBuilder();
                    notes.AppendLine("New version available, those are the release notes:");

                    foreach (var entry in args.ReleaseNotes)
                    {
                        notes.AppendLine($"Version {entry.Version}: {entry.ReleaseNotes}");
                    }

                    MessageBox.Show(notes.ToString());
                });
            };

            try
            {
                var timeout = TimeSpan.FromSeconds(120);
                updateLogTextBox.AppendLine($"Time limite: {timeout.TotalSeconds} seconds");
                await activity.ForTask(task).WithToken(token).Wait(timeout).Run();
            }
            catch (OperationCanceledException)
            {
                TouchGui(() =>
                {
                    Text = "Windows Forms App - MainForm [backgroung]";
                    updateLogTextBox.AppendLine("Update task put into background");
                    const string msg = "Ok, at this point the update process may have ended or not, but it seems to be taking a long time to respond, so let's leave it in the background and allow the app to continue working on other things.";
                    MessageBox.Show(msg);
                });
            }
            catch (Exception ex)
            {
                TouchGui(() =>
                {
                    _updateInProgress = false;
                    Text = "Windows Forms App - MainForm [error]";
                    updateLogTextBox.AppendLine("Update error");
                    MessageBox.Show(ex.Message);
                });
            }
        }

        private void TouchGui(Action action)
        {
            BeginInvoke(action);
        }

        // ReSharper disable once UnusedMember.Local
        private async Task CheckForUpdatesOnNetWorkOrLocalDriveAsync(Form form)
        {
            updateLogTextBox.Text += "\nChecking for updates...";

            var di = new DirectoryInfo(@"C:\source\repos\Cake\Cake.StartUp\Releases");

            if (!di.Exists)
            {
                updateLogTextBox.Text += $"\nDirectory not found: {di.FullName}";
                return;
            }

            var fi = new FileInfo(Path.Combine(di.FullName, "RELEASES"));

            if (!fi.Exists)
            {
                updateLogTextBox.Text += $"\nFile not found: {fi.FullName}";
                return;
            }

            using (var manager = new UpdateManager(di.FullName))
            {
                var updateInfo = await manager.CheckForUpdate();

                // Check for Squirrel application update
                if (updateInfo.ReleasesToApply.Any()) // Check if we have any update
                {
                    updateLogTextBox.Text += "\nPerforming app update, please wait...";

                    var assembly = Assembly.GetExecutingAssembly();
                    var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);

                    var releaseNotes = new StringBuilder();

                    foreach (var releaseEntry in updateInfo.ReleasesToApply)
                    {
                        releaseNotes.AppendLine($"\n\nVersion: {releaseEntry.Version}");
                        releaseNotes.AppendLine(releaseEntry.GetReleaseNotes(di.FullName)
                            .Replace("<![CDATA[", "")
                            .Replace("<p>", "")
                            .Replace("</p>", "")
                            .Replace("]]>", "")
                            .Trim()
                        );
                    }

                    var msg = "New version available!" +
                              "\n\nCurrent version: " + updateInfo.CurrentlyInstalledVersion.Version +
                               "\nNew version: " + updateInfo.FutureReleaseEntry.Version +
                              "\n\nThe application will update and restart." +
                              "\n\nRelease notes: " + releaseNotes;

                    form.BeginInvoke(new Action(() => MessageBox.Show(msg, fvi.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information)));

                    // Do the update
                    await manager.UpdateApp();

                    updateLogTextBox.Text += "\nApp update done, restarting...";

                    // Restart the app
                    UpdateManager.RestartApp();
                }
                else
                {
                    updateLogTextBox.Text += "\nNo update found.";
                }
            }
        }

        private void ShowAppVersion()
        {
            updateLogTextBox.Text = $"Version: {Application.ProductVersion}";
        }
    }
}
