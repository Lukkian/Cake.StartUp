using System;
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
                e.Cancel = true;
                MessageBoxQueue.Add(() =>
                {
                    TouchGui(() =>
                    {
                        var dr = MessageBox.Show(
                            $"The updade process has not yet finished, do you want to close anyway?{Environment.NewLine}It is strongly recommended to wait for the updade to complete.",
                            "Update in progress...", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                        MessageBoxQueue.SetFree();

                        if (dr == DialogResult.Yes)
                        {
                            _updateInProgress = false;
                            Close();
                        }
                    });
                });
            }
        }

        private async void MainForm_Shown(object sender, EventArgs e)
        {
            BringToFront();
            await CheckForUpdates();
        }

        private async Task CheckForUpdates()
        {
            var appUpdate = new AppUpdate { AllowUnstable = true, FakeUpdate = false };

#if DEBUG
            appUpdate.FakeUpdate = true;
#endif

            void MessageLogs(string msg)
            {
                TouchGui(() =>
                {
                    void RemoveDuplicate(string duplicate)
                    {
                        var lastLine = updateLogTextBox.Text.TakeLastLine() ?? string.Empty;

                        if (lastLine.Contains(duplicate) && msg.Contains(duplicate))
                        {
                            updateLogTextBox.Text = updateLogTextBox.Text.Replace(lastLine, string.Empty).TrimEnd();
                        }
                    }

                    RemoveDuplicate("Downloading update");
                    RemoveDuplicate("Updating");

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

            appUpdate.GotReleaseNotes += args =>
            {
                var notes = new StringBuilder();
                notes.AppendLine($"New version available, those are the release notes:{Environment.NewLine}");

                foreach (var entry in args.ReleaseNotes)
                {
                    notes.AppendLine($"Version {entry.Version}: {entry.ReleaseNotes}");
                }

                MessageBoxQueue.Add(() =>
                {
                    TouchGui(() =>
                    {
                        MessageBox.Show(notes.ToString(), "Release Notes", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        MessageBoxQueue.SetFree();
                    });
                });
            };

            appUpdate.UpdateDone += () =>
            {
                TouchGui(() => { Text = "Windows Forms App - MainForm [done]"; });
            };

            try
            {
                updateLogTextBox.AppendLine("Starting Check for Updates");
                _updateInProgress = true;
                var timeout = TimeSpan.FromSeconds(5);

                var localUpdateState = UpdateState.None;

                const string updatePath = @"C:\MyAppUpdates";
                updateLogTextBox.AppendLine($"Starting update task with {timeout.TotalSeconds} seconds timeout");
                var checkForUpdatesOnLocalNetwordkAsync = appUpdate.CheckForUpdatesOnLocalNetwordkAsync(updatePath, MessageLogs, new CancellationTokenSource(), false);
                await new Activity<bool>().ForTask(checkForUpdatesOnLocalNetwordkAsync).WithToken(appUpdate.Token).Wait(timeout)
                    .Run(t =>
                    {
                        if (t.Exception != null)
                        {
                            var ex = t.Exception.GetBaseException();
                            TouchGui(() => updateLogTextBox.AppendLine($"{ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}"));
                        }

                        TouchGui(() => updateLogTextBox.AppendLine("[DoneLocalUpdate]"));

                        _updateInProgress = false;

                        localUpdateState = appUpdate.State;
                    }).ConfigureAwait(true);

                // Wait for all messages to be logged
                do
                {
                    await Task.Delay(1000);
                } while (_updateInProgress);

                UpdateState remoteUpdateState;

                _updateInProgress = true;
                const string updateUrl = "https://github.com/Lukkian/Cake.StartUp";
                updateLogTextBox.AppendLine($"Starting update task with {timeout.TotalSeconds} seconds timeout");
                var checkForUpdatesOnGitHubAsync = appUpdate.CheckForUpdatesOnGitHubAsync(updateUrl, MessageLogs, new CancellationTokenSource(), false);
                await new Activity<bool>().ForTask(checkForUpdatesOnGitHubAsync).WithToken(appUpdate.Token).Wait(timeout)
                    .Run(t =>
                    {
                        if (t.Exception != null)
                        {
                            var ex = t.Exception.GetBaseException();
                            TouchGui(() => updateLogTextBox.AppendLine($"{ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}"));
                        }

                        TouchGui(() => updateLogTextBox.AppendLine("[DoneRemoteUpdate]"));

                        _updateInProgress = false;

                        remoteUpdateState = appUpdate.State;

                        if (localUpdateState == UpdateState.Done || remoteUpdateState == UpdateState.Done)
                        {
                            MessageBoxQueue.Add(() =>
                            {
                                TouchGui(() =>
                                {
                                    var dr = MessageBox.Show("Update applied, do you want to restart the app?", "Restart needed", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                                    MessageBoxQueue.SetFree();

                                    if (dr == DialogResult.Yes)
                                    {
                                        WindowState = FormWindowState.Minimized;
                                        Visible = false;
                                        AppUpdate.RestartApp();
                                    }
                                });
                            });
                        }

                    }).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                TouchGui(() =>
                {
                    Text = "Windows Forms App - MainForm [backgroung]";
                    updateLogTextBox.AppendLine("[Timeout!] Update task put into background");
                });

                const string msg = "Ok, at this point the update process may have ended or not, but it seems to be taking a long time to respond, so let's leave it in the background and allow the app to continue working on other things.";
                MessageBoxQueue.Add(() =>
                {
                    TouchGui(() =>
                    {
                        MessageBox.Show(msg, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        MessageBoxQueue.SetFree();
                    });
                });
            }
            catch (TimeoutException ex)
            {
                TouchGui(() =>
                {
                    updateLogTextBox.AppendLine($"TimeoutException: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                });
            }
            catch (Exception ex)
            {
                var innerEx = ex.InnerException;

                while (innerEx?.InnerException != null)
                {
                    innerEx = innerEx.InnerException;
                }

                var log = $"{ex.GetType().Name}: {ex.Message} {innerEx?.Message}{Environment.NewLine}{ex.StackTrace}";
                var msg = $"{ex.Message} {innerEx?.Message}";

                TouchGui(() =>
                {
                    Text = "Windows Forms App - MainForm [error]";
                    updateLogTextBox.AppendLine(log);
                });

                MessageBoxQueue.Add(() =>
                {
                    TouchGui(() =>
                    {
                        MessageBox.Show(msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        MessageBoxQueue.SetFree();
                    });
                });
            }
        }

        private void TouchGui(Action action)
        {
            BeginInvoke(action);
        }

        private void ShowAppVersion()
        {
            var version = Application.ProductVersion;

            if (version.IndexOf('+') > 0)
            {
                version = version.Remove(version.IndexOf('+'));
            }
            
#if DEBUG
            updateLogTextBox.Text = $"Version: {version}-debug";
#else
            updateLogTextBox.Text = $"Version: {version}";
#endif
        }
    }
}
