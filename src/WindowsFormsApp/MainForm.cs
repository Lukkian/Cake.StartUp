using System;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

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
            var appUpdate = new AppUpdate();

#if DEBUG
            appUpdate.FakeUpdate = true;
#endif

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
                            var substring = output.Substring(0, duplicateIndex - 12).TrimEnd(Environment.NewLine.ToCharArray());

                            output = output.Remove(0, substring.Length);
                            var newLineIndex = output.IndexOf(Environment.NewLine, StringComparison.InvariantCultureIgnoreCase);
                            string remaining = null;
                            if (newLineIndex > 0)
                            {
                                remaining = output.Substring(newLineIndex);
                            }

                            updateLogTextBox.Text = substring;
                            msg = $"{msg}{remaining}".Trim();
                            //msg = $"{substring}{msg}{remaining}".Trim();
                            //updateLogTextBox.Clear();
                        }

                        return msg;
                    }

                    const string downloadingMsg = "Downloading update";
                    msg = RemoveDuplicate(downloadingMsg);

                    const string updatingMsg = "Updating";
                    msg = RemoveDuplicate(updatingMsg);

                    updateLogTextBox.AppendLine(msg);
                    //updateLogTextBox.Text += msg;

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

            appUpdate.GotReleaseNotes += args =>
            {
                TouchGui(() =>
                {
                    var notes = new StringBuilder();
                    notes.AppendLine("New version available, those are the release notes:\n");

                    foreach (var entry in args.ReleaseNotes)
                    {
                        notes.AppendLine($"Version {entry.Version}: {entry.ReleaseNotes}");
                    }

                    MessageBox.Show(notes.ToString(), "Release Notes", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
            };

            appUpdate.UpdateDone += () =>
            {
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
                            AppUpdate.RestartApp();
                        }
                    }
                    else
                    {
                        //MessageBox.Show("No update found.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                });
            };

            try
            {
                updateLogTextBox.AppendLine("Starting Check for Updates");
                _updateInProgress = true;
                var timeout = TimeSpan.FromSeconds(5);
                updateLogTextBox.AppendLine($"Time limite: {timeout.TotalSeconds} seconds");

                const string updatePath = @"C:\MyAppUpdates";
                updateLogTextBox.AppendLine($"Checking for local updates on path: {updatePath}");
                var checkForUpdatesOnLocalNetwordkAsync = appUpdate.CheckForUpdatesOnLocalNetwordkAsync(updatePath, MessageLogs, new CancellationTokenSource(), false);
                await new Activity<bool>().ForTask(checkForUpdatesOnLocalNetwordkAsync).WithToken(appUpdate.Token).Wait(timeout)
                    .Run(t =>
                    {
                        if (t.Exception != null)
                        {
                            var ex = t.Exception.GetBaseException();
                            TouchGui(() => updateLogTextBox.AppendLine($"{ex.GetType().Name}: {ex.Message}\nStackTrace: {ex.StackTrace}"));
                        }
                        TouchGui(() => updateLogTextBox.AppendLine("[DoneLocalUpdate]"));
                        _updateInProgress = false;
                    }).ConfigureAwait(true);

                // Wait for all messages to be logged
                do
                {
                    await Task.Delay(1000);
                } while (_updateInProgress);

                _updateInProgress = true;
                const string updateUrl = "https://github.com/Lukkian/Cake.StartUp";
                updateLogTextBox.AppendLine($"Checking for remote updates on server: {updateUrl}");
                var checkForUpdatesOnGitHubAsync = appUpdate.CheckForUpdatesOnGitHubAsync(updateUrl, MessageLogs, new CancellationTokenSource(), false);
                await new Activity<bool>().ForTask(checkForUpdatesOnGitHubAsync).WithToken(appUpdate.Token).Wait(timeout)
                    .Run(t =>
                    {
                        if (t.Exception != null)
                        {
                            var ex = t.Exception.GetBaseException();
                            TouchGui(() => updateLogTextBox.AppendLine($"{ex.GetType().Name}: {ex.Message}\nStackTrace: {ex.StackTrace}"));
                        }
                        TouchGui(() => updateLogTextBox.AppendLine("[DoneRemoteUpdate]"));
                        _updateInProgress = false;
                    }).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                TouchGui(() =>
                {
                    Text = "Windows Forms App - MainForm [backgroung]";
                    updateLogTextBox.AppendLine("[Timeout!] Update task put into background");
                    const string msg = "Ok, at this point the update process may have ended or not, but it seems to be taking a long time to respond, so let's leave it in the background and allow the app to continue working on other things.";
                    MessageBox.Show(msg, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                });
            }
            catch (TimeoutException ex)
            {
                TouchGui(() =>
                {
                    updateLogTextBox.AppendLine($"TimeoutException: {ex.Message}\n{ex.StackTrace}");
                });
            }
            catch (Exception ex)
            {
                var innerEx = ex.InnerException;

                while (innerEx?.InnerException != null)
                {
                    innerEx = innerEx.InnerException;
                }

                var msg = $"{ex.Message}\n{innerEx?.Message}\n{ex.StackTrace}".Replace("\n\n", "\n");

                TouchGui(() =>
                {
                    Text = "Windows Forms App - MainForm [error]";
                    updateLogTextBox.AppendLine("Update error");
                    updateLogTextBox.AppendLine(msg);
                    MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            }
        }

        private void TouchGui(Action action)
        {
            BeginInvoke(action);
        }

        private void ShowAppVersion()
        {
            var version = Assembly.GetEntryAssembly().GetName().Version;

            // remove revision number
            version = new Version(version.Major, version.Minor, version.Build);

#if DEBUG
            updateLogTextBox.Text = $"Version: {version}-debug";
#else
            updateLogTextBox.Text = $"Version: {version}";
#endif
        }
    }
}
