using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

// Pure logic (no WPF, no network): compiled into the app and linked into InstaWords.App.Tests.
namespace InstaWords.App.Services
{
    /// <summary>The fields InstaWords reads from GET /repos/{owner}/{repo}/releases/latest.</summary>
    [DataContract]
    public sealed class GitHubRelease
    {
        [DataMember(Name = "tag_name")] public string TagName { get; set; }
        [DataMember(Name = "name")] public string Name { get; set; }
        [DataMember(Name = "body")] public string Body { get; set; }
        [DataMember(Name = "html_url")] public string HtmlUrl { get; set; }
        [DataMember(Name = "draft")] public bool Draft { get; set; }
        [DataMember(Name = "prerelease")] public bool Prerelease { get; set; }
        [DataMember(Name = "assets")] public List<GitHubAsset> Assets { get; set; }
    }

    [DataContract]
    public sealed class GitHubAsset
    {
        [DataMember(Name = "name")] public string Name { get; set; }
        [DataMember(Name = "browser_download_url")] public string BrowserDownloadUrl { get; set; }
        [DataMember(Name = "size")] public long Size { get; set; }
        /// <summary>"sha256:&lt;hex&gt;" (GitHub computes it for every uploaded asset).</summary>
        [DataMember(Name = "digest")] public string Digest { get; set; }
    }

    public static class UpdateLogic
    {
        public const string Owner = "sarmadiq9090-coder";
        public const string Repo = "InstaWords";
        public const string LatestReleaseApi = "https://api.github.com/repos/" + Owner + "/" + Repo + "/releases/latest";
        public const string ReleasesPage = "https://github.com/" + Owner + "/" + Repo + "/releases/latest";

        /// <summary>The installer asset name produced by .github/workflows/windows.yml.</summary>
        public static readonly Regex SetupAssetName = new Regex(@"^InstaWords_\d+\.\d+\.\d+_x64-setup\.exe$", RegexOptions.CultureInvariant);

        private static readonly HashSet<string> AllowedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "github.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com",
        };

        public static GitHubRelease ParseRelease(Stream json) =>
            (GitHubRelease)new DataContractJsonSerializer(typeof(GitHubRelease)).ReadObject(json);

        public static GitHubRelease ParseRelease(string json)
        {
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json ?? "")))
                return ParseRelease(ms);
        }

        /// <summary>
        /// Parses "v2.0.0", "2.0", "2.0.1-beta.1" or "2.0.0+build". Missing parts are 0.
        /// <paramref name="prerelease"/> is true when a "-suffix" is present.
        /// </summary>
        public static bool TryParseVersion(string text, out Version version, out bool prerelease)
        {
            version = null;
            prerelease = false;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var s = text.Trim();
            if (s[0] == 'v' || s[0] == 'V') s = s.Substring(1);
            var plus = s.IndexOf('+');
            if (plus >= 0) s = s.Substring(0, plus);
            var dash = s.IndexOf('-');
            if (dash >= 0)
            {
                prerelease = dash < s.Length - 1;
                s = s.Substring(0, dash);
            }
            var parts = s.Split('.');
            if (parts.Length < 1 || parts.Length > 4) return false;
            var nums = new int[4];
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0 || parts[i].Length > 9 || !parts[i].All(c => c >= '0' && c <= '9')) return false;
                nums[i] = int.Parse(parts[i], System.Globalization.CultureInfo.InvariantCulture);
            }
            version = new Version(nums[0], nums[1], nums[2], nums[3]);
            return true;
        }

        /// <summary>Normalizes to Major.Minor.Patch (assembly versions have a 4th part that releases never use).</summary>
        public static Version Normalize(Version v) => v == null ? null : new Version(v.Major, Math.Max(0, v.Minor), Math.Max(0, v.Build));

        /// <summary>True when the release tag is a stable version newer than <paramref name="current"/>.</summary>
        public static bool IsNewer(string tag, Version current)
        {
            if (!TryParseVersion(tag, out var v, out var pre) || pre) return false;
            return Normalize(v).CompareTo(Normalize(current ?? new Version(0, 0, 0))) > 0;
        }

        /// <summary>
        /// GitHub's rate limit: 429, or 403 with no requests left (X-RateLimit-Remaining: 0) or a Retry-After.
        /// Any other 403 (a proxy, a blocked network) is a different problem and keeps its own message.
        /// </summary>
        public static bool IsRateLimited(int statusCode, string rateLimitRemaining, bool hasRetryAfter) =>
            statusCode == 429 || (statusCode == 403 && (hasRetryAfter || (rateLimitRemaining ?? "").Trim() == "0"));

        public static string Display(Version v) => v == null ? "" : Normalize(v).ToString(3);

        /// <summary>A release InstaWords may offer: published, not a pre-release, tag is a stable version.</summary>
        public static bool IsEligible(GitHubRelease r) =>
            r != null && !r.Draft && !r.Prerelease && TryParseVersion(r.TagName, out _, out var pre) && !pre;

        public static GitHubAsset SelectSetupAsset(IEnumerable<GitHubAsset> assets) =>
            assets?.Where(a => a != null && a.Name != null && SetupAssetName.IsMatch(a.Name))
                .OrderByDescending(a => a.Name, StringComparer.Ordinal)
                .FirstOrDefault();

        /// <summary>Downloads are accepted only over https from GitHub's own release hosts.</summary>
        public static bool IsAllowedDownloadUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
            return uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo) && AllowedHosts.Contains(uri.Host);
        }

        /// <summary>Parses "sha256:&lt;64 hex&gt;"; anything else (missing, other algorithm, bad hex) is rejected.</summary>
        public static bool TryParseSha256Digest(string digest, out byte[] hash)
        {
            hash = null;
            if (string.IsNullOrWhiteSpace(digest)) return false;
            var d = digest.Trim();
            const string prefix = "sha256:";
            if (!d.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
            var hex = d.Substring(prefix.Length);
            if (hex.Length != 64) return false;
            var bytes = new byte[32];
            for (var i = 0; i < 32; i++)
            {
                if (!TryHex(hex[2 * i], out var hi) || !TryHex(hex[2 * i + 1], out var lo)) return false;
                bytes[i] = (byte)(hi << 4 | lo);
            }
            hash = bytes;
            return true;
        }

        private static bool TryHex(char c, out int v)
        {
            if (c >= '0' && c <= '9') { v = c - '0'; return true; }
            if (c >= 'a' && c <= 'f') { v = c - 'a' + 10; return true; }
            if (c >= 'A' && c <= 'F') { v = c - 'A' + 10; return true; }
            v = 0;
            return false;
        }

        /// <summary>Checks a downloaded file against the size and SHA-256 published by the GitHub API. Error text is Arabic.</summary>
        public static bool VerifyDownload(string path, GitHubAsset asset, out string error)
        {
            error = null;
            if (asset == null) { error = Loc.T("Update.NoAssetInfo"); return false; }
            if (!TryParseSha256Digest(asset.Digest, out var expected))
            {
                error = Loc.T("Update.NoDigest");
                return false;
            }
            var info = new FileInfo(path);
            if (!info.Exists) { error = Loc.T("Update.FileMissing"); return false; }
            if (asset.Size > 0 && info.Length != asset.Size)
            {
                error = Loc.T("Update.SizeMismatch");
                return false;
            }
            byte[] actual;
            using (var fs = File.OpenRead(path))
            using (var sha = SHA256.Create())
                actual = sha.ComputeHash(fs);
            if (!actual.SequenceEqual(expected))
            {
                error = Loc.T("Update.DigestMismatch");
                return false;
            }
            return true;
        }

        /// <summary>Release notes (Markdown) as readable plain text.</summary>
        public static string PlainNotes(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown)) return "";
            var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var sb = new StringBuilder();
            var blank = false;
            foreach (var raw in lines)
            {
                var line = raw.TrimEnd();
                if (Regex.IsMatch(line, @"^\s*(```|~~~)")) continue;
                if (Regex.IsMatch(line, @"^\s*</?div[^>]*>\s*$", RegexOptions.IgnoreCase)) continue;
                line = Regex.Replace(line, @"!\[[^\]]*\]\([^)]*\)", "");                 // images
                line = Regex.Replace(line, @"\[([^\]]+)\]\([^)]*\)", "$1");              // links → text
                line = Regex.Replace(line, @"<[^>]+>", "");                               // inline html
                line = Regex.Replace(line, @"^\s{0,3}#{1,6}\s*", "");                      // headings
                line = Regex.Replace(line, @"^\s*>\s?", "");                               // quotes
                line = Regex.Replace(line, @"^(\s*)[-*+]\s+", "$1• ");                     // bullets
                line = Regex.Replace(line, @"(\*\*|__)(.+?)\1", "$2");                     // bold
                line = Regex.Replace(line, @"(?<![\w*])\*(?!\s)(.+?)(?<!\s)\*(?![\w*])", "$1"); // italic
                line = line.Replace("`", "");
                if (Regex.IsMatch(line, @"^\s*([-*_]\s*){3,}$")) line = "";                // rules
                if (line.Trim().Length == 0)
                {
                    if (!blank && sb.Length > 0) sb.Append('\n');
                    blank = true;
                    continue;
                }
                blank = false;
                sb.Append(line).Append('\n');
            }
            return sb.ToString().Trim();
        }
    }
}
