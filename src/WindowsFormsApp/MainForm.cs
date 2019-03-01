using System;
using System.Diagnostics;
using Squirrel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
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
            var context = SynchronizationContext.Current;
            Task.Run(async () => await CheckForUpdatesAsync(this));
#endif
        }

        private async Task CheckForUpdatesAsync(Form form)
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
