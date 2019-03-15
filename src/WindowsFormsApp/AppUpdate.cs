using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Squirrel;

namespace WindowsFormsApp
{
    public class AppUpdate
    {
        public UpdateState State { get; private set; }
        public CancellationTokenSource Token { get; private set; }
        public bool FakeUpdate { get; set; }

        public async Task<bool> CheckForUpdatesOnLocalNetwordkAsync(string updatePath, Action<string> log, CancellationTokenSource token, bool restartOnSuccess)
        {
            State = UpdateState.init;

            var di = new DirectoryInfo(updatePath);

            if (!di.Exists)
            {
                State = UpdateState.InvalidUpdatePath;
                UpdateDone?.Invoke();
                log($"Directory not found: {di.FullName}");
                return false;
            }

            var fi = new FileInfo(Path.Combine(di.FullName, "RELEASES"));

            if (!fi.Exists)
            {
                State = UpdateState.InvalidUpdatePath;
                UpdateDone?.Invoke();
                log($"File not found: {fi.FullName}");
                return false;
            }

            using (var manager = new UpdateManager(di.FullName))
            {
                return await CheckForUpdatesAsync(manager, updatePath, log, token, restartOnSuccess).ConfigureAwait(false);
            }
        }

        public async Task<bool> CheckForUpdatesOnGitHubAsync(string updateUrl, Action<string> log, CancellationTokenSource token, bool restartOnSuccess)
        {
            State = UpdateState.init;

            if (await CheckForInternetConnection().ConfigureAwait(false) == false)
            {
                State = UpdateState.CantConecctServer;
                UpdateDone?.Invoke();
                log("Could not connect to server");
                return false;
            }

            //https://stackoverflow.com/questions/2859790/the-request-was-aborted-could-not-create-ssl-tls-secure-channel
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            using (var manager = await UpdateManager.GitHubUpdateManager(updateUrl).ConfigureAwait(false))
            {
                return await CheckForUpdatesAsync(manager, updateUrl, log, token, restartOnSuccess).ConfigureAwait(false);
            }
        }

        private async Task<bool> CheckForUpdatesAsync(IUpdateManager manager, string updatePath, Action<string> log, CancellationTokenSource token, bool restartOnSuccess)
        {
            Token = token;
            State = UpdateState.Checking;
            log("Contacting update server...");

            log("Checking for updates...");

            var updateInfo = await manager.CheckForUpdate().ConfigureAwait(false);

            if (FakeUpdate == false)
            {
                if (updateInfo.CurrentlyInstalledVersion == null)
                {
                    State = UpdateState.NotInstalledApp;
                    UpdateDone?.Invoke();
                    log("Current installation path not found, update canceled!");
                    return false;
                }
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

                log("Downloading update 0%");

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

                var cleanReleaseNotes = CleanReleaseNotes(updateInfo.FetchReleaseNotes());

                if (cleanReleaseNotes.Count <= 0)
                {
                    cleanReleaseNotes = CleanReleaseNotes(FetchReleaseNotes(updateInfo));
                }

                var releaseNotes = new StringBuilder();

                foreach (var note in cleanReleaseNotes)
                {
                    releaseNotes.Append($"Version {note.Version}: {note.ReleaseNotes}{Environment.NewLine}");
                }

                if (releaseNotes.Length <= 0)
                {
                    log("Release notes not found");
                }
                else
                {
                    GotReleaseNotes?.Invoke(new GotUpdateReleaseNotesEventArgs(cleanReleaseNotes));
                    log("Release notes:");
                    log(releaseNotes.ToString().TrimEnd(Environment.NewLine.ToCharArray()));
                }

                log("Performing update, please wait...");

                if (FakeUpdate)
                {
                    await Task.Delay(1000).ConfigureAwait(false);
                }
                else
                {
                    log("Updating 0%");
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
                }

                // I do not know if it's an error, but Squirrel always ends the update process with 99%
                log("Updating 100%");

                log("Update completed successfully");

                State = UpdateState.Done;
            }
            else
            {
                log($"No updates found on path: {updatePath}");
            }

            UpdateDone?.Invoke();

            if (restartOnSuccess && State == UpdateState.Done)
            {
                log("Restarting application...");

                RestartApp();
            }

            return State == UpdateState.Done;
        }

        private static Dictionary<ReleaseEntry, string> FetchReleaseNotes(UpdateInfo updateInfo)
        {
            var releaseNotes = new Dictionary<ReleaseEntry, string>(updateInfo.ReleasesToApply.Count);

            foreach (var entry in updateInfo.ReleasesToApply)
            {
                var notes = entry.GetReleaseNotes(updateInfo.PackageDirectory);
                releaseNotes.Add(entry, notes);
            }

            return releaseNotes;
        }

        private static List<GotUpdateReleaseNotesEventArgs.ReleaseEntry> CleanReleaseNotes(Dictionary<ReleaseEntry, string> releaseNotes)
        {
            var cleanReleaseNotes = new List<GotUpdateReleaseNotesEventArgs.ReleaseEntry>(releaseNotes.Count);

            foreach (var entry in releaseNotes)
            {
                var notes = entry.Value
                                .Replace("<![CDATA[\n", "")
                                .Replace("<p>", "")
                                .Replace("</p>", "")
                                .Replace("]]>", "")
                                .Replace("\n\n", "")
                                .Replace("\n", "; ")
                                .Trim() + ";";

                if (string.IsNullOrWhiteSpace(notes) == false)
                {
                    cleanReleaseNotes.Add(new GotUpdateReleaseNotesEventArgs.ReleaseEntry(entry.Key.Version.ToString(), notes));
                }
                else
                {
                    cleanReleaseNotes.Add(new GotUpdateReleaseNotesEventArgs.ReleaseEntry(entry.Key.Version.ToString(), "release notes not found"));
                }
            }

            return cleanReleaseNotes;
        }

        public static void RestartApp()
        {
            // Arguments only works if you enable the Squirrel events manually
            Task.Run(() => UpdateManager.RestartApp(null, "--myapp-firstrunafterupdate")).ConfigureAwait(false);
        }

        private static async Task<bool> CheckForInternetConnection()
        {
            try
            {
                using (var client = new WebClient() { Proxy = null })
                //using (client.OpenRead("http://clients3.google.com/generate_204"))
                using (await client.OpenReadTaskAsync(new Uri("http://172.217.12.206/generate_204")).ConfigureAwait(false))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
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
        NotInstalledApp,
        InvalidUpdatePath,
        CantConecctServer,
        init
    }
}