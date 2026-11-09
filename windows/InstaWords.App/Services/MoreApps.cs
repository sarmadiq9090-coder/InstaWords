using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;

// Pure logic (no WPF): compiled into the app and linked into InstaWords.App.Tests.
namespace InstaWords.App.Services
{
    /// <summary>One entry of «المزيد من FastKiwi» / "More from FastKiwi", in the interface language.</summary>
    public sealed class MoreAppItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Tagline { get; set; }
        public string Description { get; set; }
        /// <summary>The app's page on fastkiwi.app (always passes <see cref="MoreApps.IsAllowedLink"/>).</summary>
        public string PageUrl { get; set; }
        /// <summary>ms-windows-store://pdp/?productid=… when the Store listing is live, else null.</summary>
        public string StoreUri { get; set; }
        /// <summary>No download yet: shown as «قريباً» / "Coming soon", with the page link only.</summary>
        public bool ComingSoon { get; set; }
    }

    /// <summary>
    /// The other FastKiwi apps, read from Assets/apps.json: a copy of the site's data/apps.json that is
    /// embedded in the exe at build time (see windows/README.md). Never fetched at run time.
    /// Leaves this app out, shows coming-soon apps without a Store link, and keeps only links to
    /// https://fastkiwi.app/ pages and Microsoft Store product pages.
    /// </summary>
    public static class MoreApps
    {
        public const string SelfId = "instawords";
        public const string ResourceName = "InstaWords.App.apps.json";

        private const string Released = "released";
        private const string ComingSoon = "coming-soon";
        private const string StorePrefix = "ms-windows-store://pdp/?productid=";
        private static readonly Regex StoreId = new Regex("^[0-9A-Z]{12}$", RegexOptions.CultureInvariant);
        private static readonly Regex StoreLink = new Regex(@"^ms-windows-store://pdp/\?productid=[0-9A-Z]{12}$", RegexOptions.CultureInvariant);

        /// <summary>The list from the copy embedded in this assembly; empty if it is missing or unreadable.</summary>
        public static IReadOnlyList<MoreAppItem> LoadEmbedded(string language)
        {
            try
            {
                using (var s = typeof(MoreApps).Assembly.GetManifestResourceStream(ResourceName))
                    return s == null ? new List<MoreAppItem>() : Parse(s, language);
            }
            catch (Exception)
            {
                return new List<MoreAppItem>(); // a broken list must never break the window that shows it
            }
        }

        /// <summary>Parses apps.json (schema 1) for <paramref name="language"/> ("en", anything else = Arabic).</summary>
        public static IReadOnlyList<MoreAppItem> Parse(Stream json, string language, string selfId = SelfId)
        {
            var file = (AppsFile)new DataContractJsonSerializer(typeof(AppsFile)).ReadObject(json);
            var list = new List<MoreAppItem>();
            if (file == null || file.Schema != 1 || file.Apps == null) return list;
            var en = language == Loc.English;
            foreach (var a in file.Apps)
            {
                if (a == null || string.IsNullOrEmpty(a.Id) || a.Id == selfId) continue;
                if (a.Status != Released && a.Status != ComingSoon) continue;
                var name = Pick(a.Name, en);
                var page = Pick(a.Url, en);
                if (string.IsNullOrWhiteSpace(name) || !IsHttps(page)) continue;
                var soon = a.Status == ComingSoon;
                string store = null;
                if (!soon && a.Store != null && a.Store.Live && a.Store.Id != null && StoreId.IsMatch(a.Store.Id))
                    store = StorePrefix + a.Store.Id;
                list.Add(new MoreAppItem
                {
                    Id = a.Id,
                    Name = name.Trim(),
                    Tagline = (Pick(a.Tagline, en) ?? "").Trim(),
                    Description = (Pick(a.Description, en) ?? "").Trim(),
                    PageUrl = page,
                    StoreUri = store,
                    ComingSoon = soon,
                });
            }
            return list;
        }

        /// <summary>
        /// The only links the list may open: https://fastkiwi.app/… pages (no other host, port or user
        /// info) and ms-windows-store://pdp/?productid=&lt;12-character Store ID&gt;.
        /// </summary>
        public static bool IsAllowedLink(string url)
        {
            if (string.IsNullOrEmpty(url) || url.Trim() != url) return false;
            if (StoreLink.IsMatch(url)) return true;
            return IsHttps(url);
        }

        private static bool IsHttps(string url)
        {
            if (url == null || !url.StartsWith("https://fastkiwi.app/", StringComparison.Ordinal)) return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
            return u.Scheme == Uri.UriSchemeHttps && u.Host == "fastkiwi.app" && u.IsDefaultPort && string.IsNullOrEmpty(u.UserInfo);
        }

        private static string Pick(LocalText t, bool en) => t == null ? null : en ? (t.En ?? t.Ar) : (t.Ar ?? t.En);

        [DataContract]
        internal sealed class AppsFile
        {
            [DataMember(Name = "schema")] public int Schema { get; set; }
            [DataMember(Name = "apps")] public List<AppEntry> Apps { get; set; }
        }

        [DataContract]
        internal sealed class AppEntry
        {
            [DataMember(Name = "id")] public string Id { get; set; }
            [DataMember(Name = "status")] public string Status { get; set; }
            [DataMember(Name = "name")] public LocalText Name { get; set; }
            [DataMember(Name = "tagline")] public LocalText Tagline { get; set; }
            [DataMember(Name = "description")] public LocalText Description { get; set; }
            [DataMember(Name = "url")] public LocalText Url { get; set; }
            [DataMember(Name = "store")] public StoreEntry Store { get; set; }
        }

        [DataContract]
        internal sealed class LocalText
        {
            [DataMember(Name = "ar")] public string Ar { get; set; }
            [DataMember(Name = "en")] public string En { get; set; }
        }

        [DataContract]
        internal sealed class StoreEntry
        {
            [DataMember(Name = "id")] public string Id { get; set; }
            [DataMember(Name = "live")] public bool Live { get; set; }
        }
    }
}
