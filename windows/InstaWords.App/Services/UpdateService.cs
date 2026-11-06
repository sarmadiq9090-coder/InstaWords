using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using InstaWords.App.Views;
using Microsoft.Win32;

namespace InstaWords.App.Services
{
    /// <summary>
    /// Checks GitHub Releases for a newer InstaWords and installs it. Fully asynchronous and silent
    /// when offline: a failed check never shows anything unless the user asked for it.
    /// Test hook: INSTAWORDS_UPDATE_FEED = path or file:// URL of a release JSON (used only when set).
    /// The Store copy (MSIX) never checks or downloads anything: the Microsoft Store updates it.
    /// </summary>
    public sealed class UpdateService
    {
        /// <summary>The installer's AppId (windows/installer/InstaWords.iss).</summary>
        public const string InstallerAppId = "{8F3B6C2A-5D4E-4A7B-9C1F-2E6D8A0B4C71}";
        public const string SilentInstallArgs = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS /UPDATE";
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);
        private bool _checking;

        public static Version CurrentVersion
        {
            get
            {
                var info = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                if (UpdateLogic.TryParseVersion(info, out var v, out _)) return UpdateLogic.Normalize(v);
                return UpdateLogic.Normalize(Assembly.GetExecutingAssembly().GetName().Version);
            }
        }

        public static string CurrentVersionText => UpdateLogic.Display(CurrentVersion);

        /// <summary>A newer eligible release, once found.</summary>
        public GitHubRelease Available { get; private set; }
        public Version AvailableVersion =>
            Available != null && UpdateLogic.TryParseVersion(Available.TagName, out var v, out _) ? UpdateLogic.Normalize(v) : null;

        public string LastError { get; private set; }

        public DateTimeOffset? LastCheck
        {
            get
            {
                var s = App.Current.Settings.LastUpdateCheck;
                return DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : (DateTimeOffset?)null;
            }
        }

        public string LastCheckText => LastCheck.HasValue
            ? LastCheck.Value.ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture)
            : Loc.T("Update.NeverChecked");

        /// <summary>False for the Store copy: no GitHub check, no download, no installer.</summary>
        public static bool IsEnabled => StoreLogic.SelfUpdates(AppPackage.Kind);

        public string StatusText
        {
            get
            {
                if (!IsEnabled) return Loc.T("Update.FromStore");
                if (_checking) return Loc.T("Update.Checking");
                if (Available != null) return Loc.F("Update.StatusAvailable", UpdateLogic.Display(AvailableVersion), LastCheckText);
                if (LastError != null) return Loc.F("Update.StatusFailed", LastError);
                return LastCheck.HasValue ? Loc.F("Update.StatusLatest", LastCheckText) : Loc.T("Update.StatusNever");
            }
        }

        /// <summary>Portable copies (no matching installer registration, or a portable marker) update through the browser.</summary>
        public static bool IsInstalled
        {
            get
            {
                try
                {
                    var exeDir = Path.GetDirectoryName(AppPaths.ExePath) ?? "";
                    if (File.Exists(Path.Combine(exeDir, "portable.txt"))) return false;
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + InstallerAppId + "_is1"))
                    {
                        var location = key?.GetValue("InstallLocation") as string;
                        if (string.IsNullOrEmpty(location)) return false;
                        return string.Equals(Path.GetFullPath(location).TrimEnd('\\'), Path.GetFullPath(exeDir).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
                    }
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>At startup: at most once a day, a few seconds after the window appears.</summary>
        public async void CheckOnStartup()
        {
            if (!IsEnabled) return;
            var s = App.Current.Settings;
            var feed = Environment.GetEnvironmentVariable("INSTAWORDS_UPDATE_FEED");
            if (string.IsNullOrEmpty(feed))
            {
                if (!s.AutoCheckUpdates) return;
                if (LastCheck.HasValue && DateTimeOffset.Now - LastCheck.Value < CheckInterval) return;
                await Task.Delay(TimeSpan.FromSeconds(8));
            }
            else await Task.Delay(TimeSpan.FromSeconds(1.5)); // test feed: let the window settle first
            await CheckAsync(interactive: false, owner: null);
        }

        /// <summary>Checks now. Interactive checks report every outcome; silent ones only announce a new version.</summary>
        public async Task CheckAsync(bool interactive, Window owner)
        {
            if (_checking || !IsEnabled) return;
            _checking = true;
            try
            {
                var release = await FetchLatestAsync().ConfigureAwait(true);
                LastError = null;
                App.Current.Settings.LastUpdateCheck = DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture);
                App.Current.SaveSettingsSoon();

                if (UpdateLogic.IsEligible(release) && UpdateLogic.IsNewer(release.TagName, CurrentVersion))
                {
                    Available = release;
                    var version = UpdateLogic.Display(AvailableVersion);
                    App.Current.AppWindow?.SetUpdateBadge(version);
                    var skipped = string.Equals(App.Current.Settings.SkippedVersion, version, StringComparison.Ordinal);
                    if (interactive) ShowAvailableUpdate(owner);
                    else if (!skipped && App.Current.Settings.AnnouncedVersion != version)
                    {
                        App.Current.Settings.AnnouncedVersion = version;
                        App.Current.SaveSettingsSoon();
                        var main = App.Current.AppWindow;
                        if (main != null && main.IsVisible) ShowAvailableUpdate(main);
                        else ToastWindow.ShowMessage(true, Loc.F("Update.ToastTitle", version), Loc.T("Update.ToastBody"));
                    }
                }
                else
                {
                    Available = null;
                    App.Current.AppWindow?.SetUpdateBadge(null);
                    if (interactive) ToastWindow.ShowMessage(true, Loc.T("Update.LatestTitle"), Loc.F("Update.LatestBody", CurrentVersionText));
                }
            }
            catch (Exception ex)
            {
                LastError = Describe(ex);
                if (interactive) ToastWindow.ShowMessage(false, Loc.T("Update.CheckFailedTitle"), LastError);
            }
            finally
            {
                _checking = false;
            }
        }

        public void ShowAvailableUpdate(Window owner)
        {
            if (Available == null || !IsEnabled) return;
            var w = new UpdateWindow(this, Available) { Owner = owner != null && owner.IsVisible ? owner : null };
            if (w.Owner == null) w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            w.ShowDialog();
        }

        public void Skip(GitHubRelease release)
        {
            if (!UpdateLogic.TryParseVersion(release?.TagName, out var v, out _)) return;
            App.Current.Settings.SkippedVersion = UpdateLogic.Display(v);
            App.Current.SaveSettingsSoon();
        }

        public static void OpenReleasePage(GitHubRelease release)
        {
            var url = release?.HtmlUrl;
            if (string.IsNullOrEmpty(url) || !url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)) url = UpdateLogic.ReleasesPage;
            Links.Open(url);
        }

        // ---------- Network ----------

        private static HttpClient CreateClient()
        {
            try
            {
                // .NET Framework defaults may exclude TLS 1.2 on older systems; GitHub requires it.
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | (SecurityProtocolType)12288 /* TLS 1.3 */;
            }
            catch (NotSupportedException)
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            var handler = new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate };
            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("InstaWords/" + CurrentVersionText);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            return client;
        }

        private static async Task<GitHubRelease> FetchLatestAsync()
        {
            var feed = Environment.GetEnvironmentVariable("INSTAWORDS_UPDATE_FEED");
            if (!string.IsNullOrEmpty(feed))
            {
                var path = feed.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ? new Uri(feed).LocalPath : feed;
                using (var fs = File.OpenRead(path)) return UpdateLogic.ParseRelease(fs);
            }
            using (var client = CreateClient())
            {
                var url = UpdateLogic.LatestReleaseApi;
                for (var hop = 0; ; hop++)
                {
                    using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseContentRead).ConfigureAwait(false))
                    {
                        var code = (int)response.StatusCode;
                        // A renamed repository answers 301: follow it, but only within api.github.com.
                        if (code >= 300 && code < 400 && response.Headers.Location != null && hop < 3)
                        {
                            var next = new Uri(new Uri(url), response.Headers.Location);
                            if (next.Scheme != Uri.UriSchemeHttps || !next.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase))
                                throw new UpdateException(Loc.T("Update.BadRedirect"));
                            url = next.AbsoluteUri;
                            continue;
                        }
                        if (response.StatusCode == HttpStatusCode.NotFound) return null; // no release yet
                        response.EnsureSuccessStatusCode();
                        using (var s = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            return UpdateLogic.ParseRelease(s);
                    }
                }
            }
        }

        /// <summary>
        /// Downloads the setup asset to %TEMP%, following redirects only to allowed GitHub hosts,
        /// then checks its size and SHA-256. Returns the verified file path.
        /// </summary>
        public static async Task<string> DownloadInstallerAsync(GitHubAsset asset, IProgress<double> progress, CancellationToken ct)
        {
            if (!IsEnabled) throw new UpdateException(Loc.T("Update.FromStore"));
            if (asset == null) throw new UpdateException(Loc.T("Update.NoInstaller"));
            if (!UpdateLogic.TryParseSha256Digest(asset.Digest, out _))
                throw new UpdateException(Loc.T("Update.NoDigest"));
            var url = asset.BrowserDownloadUrl;
            if (!UpdateLogic.IsAllowedDownloadUrl(url)) throw new UpdateException(Loc.T("Update.NotGitHub"));

            var dir = Path.Combine(Path.GetTempPath(), "InstaWords-update");
            Directory.CreateDirectory(dir);
            var target = Path.Combine(dir, Path.GetFileName(asset.Name));
            var partial = target + ".part";

            using (var client = CreateClient())
            {
                client.Timeout = TimeSpan.FromMinutes(10);
                HttpResponseMessage response = null;
                try
                {
                    for (var hop = 0; ; hop++)
                    {
                        response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                        var code = (int)response.StatusCode;
                        if (code >= 300 && code < 400 && response.Headers.Location != null)
                        {
                            var next = new Uri(new Uri(url), response.Headers.Location).AbsoluteUri;
                            response.Dispose();
                            response = null;
                            if (hop >= 5 || !UpdateLogic.IsAllowedDownloadUrl(next)) throw new UpdateException(Loc.T("Update.UntrustedRedirect"));
                            url = next;
                            continue;
                        }
                        response.EnsureSuccessStatusCode();
                        break;
                    }

                    var total = response.Content.Headers.ContentLength ?? asset.Size;
                    using (var src = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var dst = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                    {
                        var buffer = new byte[81920];
                        long done = 0;
                        int read;
                        while ((read = await src.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                        {
                            await dst.WriteAsync(buffer, 0, read, ct).ConfigureAwait(false);
                            done += read;
                            if (asset.Size > 0 && done > asset.Size) throw new UpdateException(Loc.T("Update.TooLarge"));
                            if (total > 0) progress?.Report(Math.Min(1.0, (double)done / total));
                        }
                    }
                }
                finally
                {
                    response?.Dispose();
                }
            }

            if (!UpdateLogic.VerifyDownload(partial, asset, out var error))
            {
                TryDelete(partial);
                throw new UpdateException(error);
            }
            TryDelete(target);
            File.Move(partial, target);
            return target;
        }

        /// <summary>Starts the verified installer silently (it relaunches InstaWords when done) and exits.</summary>
        public static void RunInstallerAndExit(string installer)
        {
            if (!IsEnabled) return;
            Process.Start(new ProcessStartInfo(installer, SilentInstallArgs) { UseShellExecute = true });
            App.Current.ExitApp();
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
        }

        public static string Describe(Exception ex)
        {
            while (ex is AggregateException && ex.InnerException != null) ex = ex.InnerException;
            if (ex is UpdateException) return ex.Message;
            if (ex is TaskCanceledException || ex is OperationCanceledException) return Loc.T("Update.Timeout");
            if (ex is HttpRequestException || ex is WebException) return Loc.T("Update.Offline");
            return ex.Message;
        }
    }

    public sealed class UpdateException : Exception
    {
        public UpdateException(string message) : base(message) { }
    }

    public static class Links
    {
        public const string Website = "https://fastkiwi.app/instawords/";
        public const string GitHub = "https://github.com/" + UpdateLogic.Owner + "/" + UpdateLogic.Repo;
        public const string License = GitHub + "/blob/HEAD/LICENSE";

        /// <summary>Opens an https link in the default browser.</summary>
        public static void Open(string url)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return;
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ToastWindow.ShowMessage(false, Loc.T("Common.OpenLinkFailed"), ex.Message);
            }
        }

        /// <summary>Opens a mailto: link in the user's mail app; false when Windows has none to open it with.</summary>
        public static bool TryOpenMail(string url)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) return false;
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
