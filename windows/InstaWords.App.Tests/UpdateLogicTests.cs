using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using InstaWords.App.Services;
using Xunit;

namespace InstaWords.App.Tests
{
    public class UpdateLogicTests
    {
        [Theory]
        [InlineData("v2.0.0", 2, 0, 0, false)]
        [InlineData("2.1", 2, 1, 0, false)]
        [InlineData("V10.20.30", 10, 20, 30, false)]
        [InlineData("v2.1.0-beta.1", 2, 1, 0, true)]
        [InlineData("2.0.0+build.7", 2, 0, 0, false)]
        [InlineData(" v3.0.1 ", 3, 0, 1, false)]
        public void ParsesVersions(string text, int major, int minor, int patch, bool pre)
        {
            Assert.True(UpdateLogic.TryParseVersion(text, out var v, out var isPre));
            Assert.Equal(new Version(major, minor, patch), UpdateLogic.Normalize(v));
            Assert.Equal(pre, isPre);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("latest")]
        [InlineData("v2.x")]
        [InlineData("v1..2")]
        [InlineData("1.2.3.4.5")]
        public void RejectsBadVersions(string text) => Assert.False(UpdateLogic.TryParseVersion(text, out _, out _));

        [Theory]
        [InlineData("v2.0.1", "2.0.0", true)]
        [InlineData("v2.1.0", "2.0.9", true)]
        [InlineData("v10.0.0", "9.9.9", true)]   // numeric, not lexical
        [InlineData("v2.0.0", "2.0.0", false)]
        [InlineData("v1.9.9", "2.0.0", false)]
        [InlineData("v2.0.0", "2.0.0.5", false)] // the 4th assembly part is ignored
        [InlineData("v3.0.0-rc.1", "2.0.0", false)] // pre-releases are never offered
        [InlineData("garbage", "2.0.0", false)]
        public void ComparesSemantically(string tag, string current, bool newer) =>
            Assert.Equal(newer, UpdateLogic.IsNewer(tag, Version.Parse(current)));

        [Fact]
        public void EligibilitySkipsDraftsAndPrereleases()
        {
            Assert.True(UpdateLogic.IsEligible(new GitHubRelease { TagName = "v2.0.0" }));
            Assert.False(UpdateLogic.IsEligible(new GitHubRelease { TagName = "v2.0.0", Prerelease = true }));
            Assert.False(UpdateLogic.IsEligible(new GitHubRelease { TagName = "v2.0.0", Draft = true }));
            Assert.False(UpdateLogic.IsEligible(new GitHubRelease { TagName = "v2.1.0-beta" }));
            Assert.False(UpdateLogic.IsEligible(null));
        }

        [Fact]
        public void SelectsOnlyTheSetupAsset()
        {
            var assets = new List<GitHubAsset>
            {
                new GitHubAsset { Name = "InstaWords_2.0.0_x64-portable.zip" },
                new GitHubAsset { Name = "InstaWords_2.0.0_x64-setup.exe.sig" },
                new GitHubAsset { Name = "Evil_InstaWords_2.0.0_x64-setup.exe" },
                new GitHubAsset { Name = "InstaWords_2.0.0_x64-setup.exe", BrowserDownloadUrl = "https://github.com/x" },
                new GitHubAsset { Name = null },
            };
            Assert.Equal("InstaWords_2.0.0_x64-setup.exe", UpdateLogic.SelectSetupAsset(assets).Name);
            Assert.Null(UpdateLogic.SelectSetupAsset(new[] { new GitHubAsset { Name = "InstaWords_2.0_x64-setup.exe" } }));
            Assert.Null(UpdateLogic.SelectSetupAsset(null));
        }

        [Theory]
        [InlineData("https://github.com/sarmadiq9090-coder/tafket/releases/download/v2.0.0/InstaWords_2.0.0_x64-setup.exe", true)]
        [InlineData("https://objects.githubusercontent.com/github-production-release-asset/123?X-Amz=1", true)]
        [InlineData("https://release-assets.githubusercontent.com/github-production-release-asset/1/2?sp=r", true)]
        [InlineData("https://GITHUB.com/a", true)]
        [InlineData("http://github.com/a", false)]                  // not https
        [InlineData("https://github.com.evil.example/a", false)]    // look-alike host
        [InlineData("https://evil.example/github.com/a", false)]
        [InlineData("https://user:pw@github.com/a", false)]         // credentials
        [InlineData("https://github.com:8443/a", false)]            // non-default port
        [InlineData("https://raw.githubusercontent.com/a", false)]  // not a release host
        [InlineData("file:///C:/setup.exe", false)]
        [InlineData("not a url", false)]
        [InlineData(null, false)]
        public void AllowsOnlyGitHubReleaseHosts(string url, bool allowed) => Assert.Equal(allowed, UpdateLogic.IsAllowedDownloadUrl(url));

        [Fact]
        public void ParsesDigest()
        {
            var hex = new string('a', 62) + "0F";
            Assert.True(UpdateLogic.TryParseSha256Digest("sha256:" + hex, out var hash));
            Assert.Equal(32, hash.Length);
            Assert.Equal(0x0F, hash[31]);
            Assert.False(UpdateLogic.TryParseSha256Digest(null, out _));
            Assert.False(UpdateLogic.TryParseSha256Digest("sha512:" + hex, out _));
            Assert.False(UpdateLogic.TryParseSha256Digest("sha256:" + hex.Substring(2), out _));
            Assert.False(UpdateLogic.TryParseSha256Digest("sha256:" + new string('g', 64), out _));
        }

        [Fact]
        public void VerifiesDownloadedFile()
        {
            var path = Path.GetTempFileName();
            try
            {
                var bytes = new byte[5000];
                new Random(42).NextBytes(bytes);
                File.WriteAllBytes(path, bytes);
                var digest = "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

                Assert.True(UpdateLogic.VerifyDownload(path, new GitHubAsset { Size = bytes.Length, Digest = digest }, out var err), err);
                Assert.True(UpdateLogic.VerifyDownload(path, new GitHubAsset { Size = bytes.Length, Digest = digest.ToUpperInvariant().Replace("SHA256", "sha256") }, out _));
                Assert.False(UpdateLogic.VerifyDownload(path, new GitHubAsset { Size = bytes.Length + 1, Digest = digest }, out err));
                Assert.Contains("حجم", err);
                var wrong = "sha256:" + new string('0', 64);
                Assert.False(UpdateLogic.VerifyDownload(path, new GitHubAsset { Size = bytes.Length, Digest = wrong }, out err));
                Assert.Contains("SHA-256", err);
                Assert.False(UpdateLogic.VerifyDownload(path, new GitHubAsset { Size = bytes.Length, Digest = null }, out err));
                Assert.Contains("بصمة", err);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void ParsesGitHubReleaseJson()
        {
            const string json = @"{
              ""tag_name"": ""v2.1.0"", ""name"": ""InstaWords v2.1.0"", ""draft"": false, ""prerelease"": false,
              ""html_url"": ""https://github.com/sarmadiq9090-coder/tafket/releases/tag/v2.1.0"",
              ""body"": ""## الجديد\n- **أسرع**\n- [رابط](https://x.y)"",
              ""unknown_field"": {""nested"": [1,2,3]},
              ""assets"": [{""name"": ""InstaWords_2.1.0_x64-setup.exe"", ""size"": 123,
                ""browser_download_url"": ""https://github.com/a/b"", ""digest"": ""sha256:abc"", ""uploader"": {""login"": ""x""}}]
            }";
            var r = UpdateLogic.ParseRelease(json);
            Assert.Equal("v2.1.0", r.TagName);
            Assert.Single(r.Assets);
            Assert.Equal(123, r.Assets[0].Size);
            Assert.Equal("sha256:abc", r.Assets[0].Digest);
            Assert.Equal("الجديد\n• أسرع\n• رابط", UpdateLogic.PlainNotes(r.Body));
        }

        [Fact]
        public void PlainNotesStripsMarkdown()
        {
            var md = "<div dir=\"rtl\">\n\n### Title\n\n**Bold** and *italic* and `code`\n\n---\n\n> quote\n![img](x.png)\n</div>";
            Assert.Equal("Title\n\nBold and italic and code\n\nquote", UpdateLogic.PlainNotes(md));
            Assert.Equal("", UpdateLogic.PlainNotes(null));
        }
    }
}
