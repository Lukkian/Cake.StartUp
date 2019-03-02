using Squirrel;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            ShowAppVersion();

            CheckForUpdates();
        }

        private void CheckForUpdates()
        {
#if !DEBUG
            var activity = new Activity<bool>();
            var task = CheckForUpdatesOnGitHubAsync(this);
            
            try
            {
                activity.ForTask(task).Wait(100).Run().GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("The update server took too long to respond, the operation was canceled.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
#endif
        }

        // ReSharper disable once UnusedMember.Local
        private async Task<bool> CheckForUpdatesOnGitHubAsync(Form form)
        {
            versionLabel.Text += "\nContacting update server...";

            //var di = new DirectoryInfo(@"C:\source\repos\Cake\Cake.StartUp\Releases");

            //if (!di.Exists)
            //{
            //    versionLabel.Text += $"\nDirectory not found: {di.FullName}";
            //    return;
            //}

            //var fi = new FileInfo(Path.Combine(di.FullName, "RELEASES"));

            //if (!fi.Exists)
            //{
            //    versionLabel.Text += $"\nFile not found: {fi.FullName}";
            //    return;
            //}

            const string updateUrl = "https://github.com/Lukkian/Cake.StartUp";
            using (var manager = await UpdateManager.GitHubUpdateManager(updateUrl))
            {
                versionLabel.Text += "\nChecking for updates...";
                var updateInfo = await manager.CheckForUpdate();

                // Check for Squirrel application update
                if (updateInfo.ReleasesToApply.Any()) // Check if we have any update
                {
                    versionLabel.Text += "\nPerforming app update, please wait...";

                    var assembly = Assembly.GetExecutingAssembly();
                    var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);

                    var releaseNotes = new StringBuilder();

                    foreach (var releaseEntry in updateInfo.ReleasesToApply)
                    {
                        releaseNotes.AppendLine($"\n\nVersion: {releaseEntry.Version}");
                        releaseNotes.AppendLine(releaseEntry.GetReleaseNotes(updateUrl)
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

                    versionLabel.Text += "\nApp update done, restarting...";

                    // Restart the app
                    UpdateManager.RestartApp();
                }
                else
                {
                    versionLabel.Text += "\nNo update found.";
                }

                return true;
            }
        }

        // ReSharper disable once UnusedMember.Local
        private async Task CheckForUpdatesOnNetWorkOrLocalDriveAsync(Form form)
        {
            versionLabel.Text += "\nChecking for updates...";

            var di = new DirectoryInfo(@"C:\source\repos\Cake\Cake.StartUp\Releases");

            if (!di.Exists)
            {
                versionLabel.Text += $"\nDirectory not found: {di.FullName}";
                return;
            }

            var fi = new FileInfo(Path.Combine(di.FullName, "RELEASES"));

            if (!fi.Exists)
            {
                versionLabel.Text += $"\nFile not found: {fi.FullName}";
                return;
            }

            using (var manager = new UpdateManager(di.FullName))
            {
                var updateInfo = await manager.CheckForUpdate();

                // Check for Squirrel application update
                if (updateInfo.ReleasesToApply.Any()) // Check if we have any update
                {
                    versionLabel.Text += "\nPerforming app update, please wait...";

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

                    versionLabel.Text += "\nApp update done, restarting...";

                    // Restart the app
                    UpdateManager.RestartApp();
                }
                else
                {
                    versionLabel.Text += "\nNo update found.";
                }
            }
        }

        private void ShowAppVersion()
        {
            versionLabel.Text = $"Version: {Application.ProductVersion}";
        }
    }
}
