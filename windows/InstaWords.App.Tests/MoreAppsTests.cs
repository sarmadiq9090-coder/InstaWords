using System.IO;
using System.Linq;
using System.Text;
using InstaWords.App.Services;
using Xunit;

namespace InstaWords.App.Tests
{
    /// <summary>«المزيد من FastKiwi» / "More from FastKiwi": the embedded apps.json and its rules.</summary>
    public class MoreAppsTests
    {
        private static string VendoredFile => Path.Combine(TestStrings.AppDir, "Assets", "apps.json");

        private static Stream Json(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

        [Theory]
        [InlineData("ar")]
        [InlineData("en")]
        public void TheEmbeddedListHasTheOtherApps(string language)
        {
            var apps = MoreApps.LoadEmbedded(language);
            Assert.Equal(new[] { "instafind", "remember-me", "lemon", "instaconvert" }, apps.Select(a => a.Id));
            Assert.DoesNotContain(apps, a => a.Id == MoreApps.SelfId);
            foreach (var a in apps)
            {
                Assert.False(string.IsNullOrWhiteSpace(a.Name), a.Id);
                Assert.False(string.IsNullOrWhiteSpace(a.Tagline), a.Id);
                Assert.True(MoreApps.IsAllowedLink(a.PageUrl), a.PageUrl);
                Assert.StartsWith("https://fastkiwi.app/", a.PageUrl);
                if (a.StoreUri != null) Assert.True(MoreApps.IsAllowedLink(a.StoreUri), a.StoreUri);
            }
        }

        [Fact]
        public void TheEmbeddedCopyIsTheVendoredFile()
        {
            using (var f = File.OpenRead(VendoredFile))
            {
                var fromDisk = MoreApps.Parse(f, "en");
                var embedded = MoreApps.LoadEmbedded("en");
                Assert.Equal(fromDisk.Select(a => a.Id + a.PageUrl + a.StoreUri), embedded.Select(a => a.Id + a.PageUrl + a.StoreUri));
            }
        }

        [Fact]
        public void FollowsTheInterfaceLanguage()
        {
            var ar = MoreApps.LoadEmbedded("ar").Single(a => a.Id == "remember-me");
            var en = MoreApps.LoadEmbedded("en").Single(a => a.Id == "remember-me");
            Assert.Equal("ذكرني", ar.Name);
            Assert.Equal("Remember Me", en.Name);
            Assert.Equal("https://fastkiwi.app/Remember-Me/ar/", ar.PageUrl);
            Assert.Equal("https://fastkiwi.app/Remember-Me/", en.PageUrl);
        }

        [Fact]
        public void StoreLinkOnlyWhenLive()
        {
            var apps = MoreApps.LoadEmbedded("ar").ToDictionary(a => a.Id);
            Assert.Equal("ms-windows-store://pdp/?productid=9NGB1F7T7JGN", apps["remember-me"].StoreUri); // live
            Assert.Null(apps["instafind"].StoreUri); // reserved, not live yet
            Assert.Null(apps["lemon"].StoreUri);     // no Store entry
        }

        [Fact]
        public void ComingSoonHasNoStoreLink()
        {
            var soon = MoreApps.LoadEmbedded("en").Single(a => a.Id == "instaconvert");
            Assert.True(soon.ComingSoon);
            Assert.Null(soon.StoreUri);
            Assert.Equal("https://fastkiwi.app/downloads/#instaconvert", soon.PageUrl);

            // Even a coming-soon entry that claims a live Store listing gets no Store link.
            var apps = MoreApps.Parse(Json(@"{""schema"":1,""apps"":[{""id"":""x"",""status"":""coming-soon"",""name"":{""ar"":""س"",""en"":""X""},
                ""url"":{""ar"":""https://fastkiwi.app/x/"",""en"":""https://fastkiwi.app/x/""},""store"":{""id"":""9NGB1F7T7JGN"",""live"":true}}]}"), "en");
            Assert.True(apps.Single().ComingSoon);
            Assert.Null(apps.Single().StoreUri);
        }

        [Fact]
        public void LeavesOutSelfUnknownStatusAndForeignLinks()
        {
            const string json = @"{""schema"":1,""apps"":[
                {""id"":""instawords"",""status"":""released"",""name"":{""ar"":""a"",""en"":""a""},""url"":{""ar"":""https://fastkiwi.app/a/"",""en"":""https://fastkiwi.app/a/""},""store"":null},
                {""id"":""beta"",""status"":""beta"",""name"":{""ar"":""b"",""en"":""b""},""url"":{""ar"":""https://fastkiwi.app/b/"",""en"":""https://fastkiwi.app/b/""},""store"":null},
                {""id"":""evil"",""status"":""released"",""name"":{""ar"":""c"",""en"":""c""},""url"":{""ar"":""https://evil.example/"",""en"":""https://evil.example/""},""store"":null},
                {""id"":""http"",""status"":""released"",""name"":{""ar"":""d"",""en"":""d""},""url"":{""ar"":""http://fastkiwi.app/d/"",""en"":""http://fastkiwi.app/d/""},""store"":null},
                {""id"":""badstore"",""status"":""released"",""name"":{""ar"":""e"",""en"":""e""},""url"":{""ar"":""https://fastkiwi.app/e/"",""en"":""https://fastkiwi.app/e/""},""store"":{""id"":""9N&x=1"",""live"":true}},
                {""id"":""ok"",""status"":""released"",""name"":{""ar"":""f"",""en"":""f""},""url"":{""ar"":""https://fastkiwi.app/f/"",""en"":""https://fastkiwi.app/f/""},""store"":{""id"":""9MTK4KMFQ657"",""live"":false}}
            ]}";
            var apps = MoreApps.Parse(Json(json), "en");
            Assert.Equal(new[] { "badstore", "ok" }, apps.Select(a => a.Id));
            Assert.All(apps, a => Assert.Null(a.StoreUri));
        }

        [Fact]
        public void OtherSchemaVersionsShowNothing()
        {
            Assert.Empty(MoreApps.Parse(Json(@"{""schema"":2,""apps"":[]}"), "ar"));
        }

        [Theory]
        [InlineData("https://fastkiwi.app/", true)]
        [InlineData("https://fastkiwi.app/Remember-Me/ar/", true)]
        [InlineData("https://fastkiwi.app/downloads/#instaconvert", true)]
        [InlineData("ms-windows-store://pdp/?productid=9NGB1F7T7JGN", true)]
        [InlineData("http://fastkiwi.app/", false)]
        [InlineData("https://fastkiwi.app.evil.example/", false)]
        [InlineData("https://fastkiwi.app@evil.example/", false)]
        [InlineData("https://user@fastkiwi.app/", false)]
        [InlineData("https://fastkiwi.app:8443/", false)]
        [InlineData("https://FASTKIWI.APP/", false)]
        [InlineData("https://evil.example/https://fastkiwi.app/", false)]
        [InlineData("https://github.com/sarmadiq9090-coder/tafket", false)]
        [InlineData(" https://fastkiwi.app/", false)]
        [InlineData("file:///C:/Windows/System32/calc.exe", false)]
        [InlineData("ms-windows-store://pdp/?productid=9NGB1F7T7JGN&cid=x", false)]
        [InlineData("ms-windows-store://pdp/?productid=short", false)]
        [InlineData("ms-windows-store://search/?query=x", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void OnlyFastKiwiPagesAndStoreProductPagesAreAllowed(string url, bool allowed) =>
            Assert.Equal(allowed, MoreApps.IsAllowedLink(url));

        /// <summary>The app embeds the same file under the same name (so the Windows build shows the list).</summary>
        [Fact]
        public void TheAppEmbedsTheFile()
        {
            var csproj = File.ReadAllText(Path.Combine(TestStrings.AppDir, "InstaWords.App.csproj"));
            Assert.Contains("<EmbeddedResource Include=\"Assets\\apps.json\" LogicalName=\"" + MoreApps.ResourceName + "\" />", csproj);
        }
    }
}
