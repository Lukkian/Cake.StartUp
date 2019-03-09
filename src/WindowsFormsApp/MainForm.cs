﻿using System;
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
            var token = new CancellationTokenSource();
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

                const string updatePath = @"C:\MyAppUpdates";
                updateLogTextBox.AppendLine($"Checking for local updates on path: {updatePath}");
                var checkForUpdatesOnLocalNetwordkAsync = appUpdate.CheckForUpdatesOnLocalNetwordkAsync(updatePath, MessageLogs, token, false);
                var success = await new Activity<bool>().ForTask(checkForUpdatesOnLocalNetwordkAsync).WithToken(token).Wait(timeout).Run().ConfigureAwait(true);

                // Wait for all messages to be written to the log
                await Task.Delay(1000, token.Token);

                if (success == false)
                {
                    const string updateUrl = "https://github.com/Lukkian/Cake.StartUp";
                    updateLogTextBox.AppendLine($"Checking for local updates on server: {updateUrl}");
                    var checkForUpdatesOnGitHubAsync = appUpdate.CheckForUpdatesOnGitHubAsync(updateUrl, MessageLogs, token, false);
                    await new Activity<bool>().ForTask(checkForUpdatesOnGitHubAsync).WithToken(token).Wait(timeout).Run();
                }
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

        private void ShowAppVersion()
        {
            updateLogTextBox.Text = $"Version: {Application.ProductVersion}";
        }
    }
}
