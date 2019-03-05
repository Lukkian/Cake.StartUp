using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Squirrel;

namespace WindowsFormsApp
{
    public class AppUpdate
    {
        public UpdateState State { get; private set; }

        public async Task<bool> CheckForUpdatesOnGitHubAsync(string updateUrl, Action<string> log, CancellationTokenSource token, bool restartOnSuccess)
        {
            State = UpdateState.Checking;
            log("Contacting update server...");

            using (var manager = await UpdateManager.GitHubUpdateManager(updateUrl).ConfigureAwait(false))
            {
                log("Checking for updates...");

                var updateInfo = await manager.CheckForUpdate().ConfigureAwait(false);

                if (updateInfo.CurrentlyInstalledVersion == null)
                {
                    State = UpdateState.NoNeed;
                    UpdateDone?.Invoke();
                    log("Current installation location not found, update canceled!");
                    return false;
                }

                // Check if we have any update
                if (updateInfo.ReleasesToApply.Any())
                {
                    State = UpdateState.Downloading;
                    log("New version available!");

                    var currentVersion = updateInfo.CurrentlyInstalledVersion?.Version.ToString();
                    var futureVersion = updateInfo.FutureReleaseEntry.Version;

                    log($"Current installed version: {currentVersion}");
                    log($"New version: {futureVersion}");

                    log("The application will now download and apply the update...");

                    await manager.DownloadReleases(updateInfo.ReleasesToApply, i =>
                    {
                        if (token == null || token.IsCancellationRequested == false)
                        {
                            log($"Downloading update {i}%");
                        }
                        else
                        {
                            State = UpdateState.Timeout;
                            log($"Downloading update {i}% [Timeout!]");
                        }
                    }).ConfigureAwait(false);

                    var releaseNotes = new StringBuilder();
                    var releaseEntries = new List<GotUpdateReleaseNotesEventArgs.ReleaseEntry>();

                    foreach (var entry in updateInfo.FetchReleaseNotes())
                    {
                        var notes = entry.Value
                            .Replace("<![CDATA[\n", "")
                            .Replace("<p>", "")
                            .Replace("</p>", "")
                            .Replace("]]>", "")
                            .Replace("\n\n", "")
                            .Replace("\n", "; ")
                            .Trim() + ";";

                        releaseNotes.Append($"Version {entry.Key.Version}: {notes}{Environment.NewLine}");

                        releaseEntries.Add(new GotUpdateReleaseNotesEventArgs.ReleaseEntry(entry.Key.Version.ToString(), notes));
                    }

                    GotReleaseNotes?.Invoke(new GotUpdateReleaseNotesEventArgs(releaseEntries));

                    if (string.IsNullOrWhiteSpace(releaseNotes.ToString().Trim()))
                    {
                        log("Release notes not found.");
                    }
                    else
                    {
                        log("Release notes:");
                        log(releaseNotes.ToString().TrimEnd(Environment.NewLine.ToCharArray()));
                    }

                    log("Performing update, please wait...");

                    // Do the update
                    await manager.UpdateApp(i =>
                    {
                        if (token == null || token.IsCancellationRequested == false)
                        {
                            log($"Updating {i}%");
                        }
                        else
                        {
                            State = UpdateState.Timeout;
                            log($"Updating {i}% [Timeout!]");
                        }
                    }).ConfigureAwait(false);

                    // I do not know if it's an error, but Squirrel always ends the update process with 99%
                    log("Updating 100%");

                    log("Update completed successfully");

                    State = UpdateState.Done;
                }
                else
                {
                    log("No update found.");
                }

                UpdateDone?.Invoke();

                if (restartOnSuccess && State == UpdateState.Done)
                {
                    log("Restarting application...");

                    RestartApp();
                }

                return true;
            }
        }

        public void RestartApp()
        {
            // Arguments only works if you enable the Squirrel events manually
            Task.Run(() => UpdateManager.RestartApp(null, "--myapp-firstrunafterupdate")).ConfigureAwait(false);
        }

        public event GotUpdateReleaseNotes GotReleaseNotes;
        public delegate void GotUpdateReleaseNotes(GotUpdateReleaseNotesEventArgs args);

        public event Done UpdateDone;
        public delegate void Done();
    }

    public class GotUpdateReleaseNotesEventArgs
    {
        public GotUpdateReleaseNotesEventArgs(IList<ReleaseEntry> releaseNotes)
        {
            ReleaseNotes = releaseNotes;
        }

        public IList<ReleaseEntry> ReleaseNotes { get; }

        public class ReleaseEntry
        {
            public ReleaseEntry(string version, string releaseNotes)
            {
                Version = version;
                ReleaseNotes = releaseNotes;
            }

            public string Version { get; }
            public string ReleaseNotes { get; }
        }
    }

    public enum UpdateState
    {
        None,
        Timeout,
        Checking,
        Downloading,
        Done,
        NoNeed
    }
}