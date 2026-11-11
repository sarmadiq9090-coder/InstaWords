using System.IO;
using System.Text.RegularExpressions;
using InstaWords.App.Services;
using Xunit;

namespace InstaWords.App.Tests
{
    /// <summary>Store mode (MSIX): package detection, install kind, data folder, start with Windows, the rating request.</summary>
    public class StoreLogicTests
    {
        [Theory]
        [InlineData(StoreLogic.AppModelErrorNoPackage, 0, false)] // classic exe: no package identity
        [InlineData(StoreLogic.ErrorInsufficientBuffer, 62, true)] // packaged: asks for a buffer
        [InlineData(StoreLogic.ErrorSuccess, 62, true)]
        [InlineData(StoreLogic.ErrorInsufficientBuffer, 0, false)]
        [InlineData(87, 0, false)] // ERROR_INVALID_PARAMETER: unexpected, keep the classic behaviour
        public void DetectsThePackage(int result, int length, bool packaged) =>
            Assert.Equal(packaged, StoreLogic.IsPackagedResult(result, length));

        [Theory]
        [InlineData(true, true, InstallKind.Store)]
        [InlineData(true, false, InstallKind.Store)]
        [InlineData(false, true, InstallKind.Installed)]
        [InlineData(false, false, InstallKind.Portable)]
        public void ResolvesTheInstallKind(bool packaged, bool setup, InstallKind expected) =>
            Assert.Equal(expected, StoreLogic.Kind(packaged, setup));

        [Fact]
        public void OnlyTheStoreCopySkipsTheSelfUpdater()
        {
            Assert.False(StoreLogic.SelfUpdates(InstallKind.Store));
            Assert.True(StoreLogic.SelfUpdates(InstallKind.Installed));
            Assert.True(StoreLogic.SelfUpdates(InstallKind.Portable));
        }

        [Fact]
        public void ProblemReportsNameTheInstallKind()
        {
            Assert.Equal("Tech.Store", StoreLogic.TechKey(InstallKind.Store));
            Assert.Equal("Tech.Installed", StoreLogic.TechKey(InstallKind.Installed));
            Assert.Equal("Tech.Portable", StoreLogic.TechKey(InstallKind.Portable));
            foreach (var lang in new[] { "ar", "en" })
                Assert.True(TestStrings.Table(lang).ContainsKey("Tech.Store"), lang);
            TestStrings.With("en", () => Assert.Equal("Updates come from Microsoft Store", Loc.T("Update.FromStore")));
            Assert.Equal("التحديثات تأتي من Microsoft Store", Loc.T("Update.FromStore"));
        }

        [Theory]
        [InlineData(@"C:\Users\amal\AppData\Roaming\FastKiwi\InstaWords", @"C:\Users\amal\AppData\Roaming", @"C:\Users\amal\AppData\Local",
                    @"C:\Users\amal\AppData\Local\Packages\12345FastKiwi.InstaWords_8wekyb3d8bbwe\LocalCache\Roaming\FastKiwi\InstaWords")]
        [InlineData(@"C:\Users\amal\AppData\Roaming\FastKiwi\InstaWords\", @"c:\users\amal\appdata\roaming\", @"C:\Users\amal\AppData\Local\",
                    @"C:\Users\amal\AppData\Local\Packages\12345FastKiwi.InstaWords_8wekyb3d8bbwe\LocalCache\Roaming\FastKiwi\InstaWords")]
        [InlineData(@"D:\Elsewhere\InstaWords", @"C:\Users\amal\AppData\Roaming", @"C:\Users\amal\AppData\Local", null)]
        [InlineData(@"C:\Users\amal\AppData\RoamingX\InstaWords", @"C:\Users\amal\AppData\Roaming", @"C:\Users\amal\AppData\Local", null)]
        public void MapsTheDataFolderToTheMsixLocation(string dataDir, string roaming, string local, string expected) =>
            Assert.Equal(expected, StoreLogic.PackagedDataDir(dataDir, roaming, local, "12345FastKiwi.InstaWords_8wekyb3d8bbwe"));

        [Fact]
        public void DataFolderNeedsEveryInput()
        {
            Assert.Null(StoreLogic.PackagedDataDir(@"C:\R\FastKiwi\InstaWords", @"C:\R", @"C:\L", null));
            Assert.Null(StoreLogic.PackagedDataDir(@"C:\R\FastKiwi\InstaWords", @"C:\R", "", "fam_x"));
            Assert.Null(StoreLogic.PackagedDataDir(null, @"C:\R", @"C:\L", "fam_x"));
        }

        [Theory]
        [InlineData(StartupTaskStatus.Disabled, false, true, null)]
        [InlineData(StartupTaskStatus.Enabled, true, true, null)]
        [InlineData(StartupTaskStatus.DisabledByUser, false, false, "Settings.StartupDisabledByUser")]
        [InlineData(StartupTaskStatus.DisabledByPolicy, false, false, "Settings.StartupByPolicy")]
        [InlineData(StartupTaskStatus.EnabledByPolicy, true, false, "Settings.StartupByPolicy")]
        public void DescribesTheStartupTask(StartupTaskStatus state, bool isChecked, bool canChange, string note)
        {
            var view = StoreLogic.DescribeStartupTask(state);
            Assert.Equal(isChecked, view.IsChecked);
            Assert.Equal(canChange, view.CanChange);
            Assert.Equal(note, view.NoteKey);
            if (note != null) Assert.True(TestStrings.Table("en").ContainsKey(note), note);
        }

        /// <summary>The numbers mirror Windows.ApplicationModel.StartupTaskState (cast directly in StartupService).</summary>
        [Fact]
        public void StartupStatesMatchWindows()
        {
            Assert.Equal(0, (int)StartupTaskStatus.Disabled);
            Assert.Equal(1, (int)StartupTaskStatus.DisabledByUser);
            Assert.Equal(2, (int)StartupTaskStatus.Enabled);
            Assert.Equal(3, (int)StartupTaskStatus.DisabledByPolicy);
            Assert.Equal(4, (int)StartupTaskStatus.EnabledByPolicy);
        }

        /// <summary>The app asks for the startup task by the TaskId the package declares.</summary>
        [Fact]
        public void StartupTaskIdMatchesTheManifest()
        {
            var manifest = File.ReadAllText(Path.Combine(TestStrings.WindowsDir, "packaging", "msix", "AppxManifest.xml"));
            var m = Regex.Match(manifest, "<uap5:StartupTask[^>]*TaskId=\"([^\"]+)\"");
            Assert.True(m.Success, "uap5:StartupTask in AppxManifest.xml");
            Assert.Equal(StoreLogic.StartupTaskId, m.Groups[1].Value);
            Assert.Contains("Executable=\"InstaWords.exe\"", manifest);
            Assert.Contains("EntryPoint=\"Windows.FullTrustApplication\"", manifest);
        }

        // ---------- «قيّم InstaWords»: the one-time rating request ----------

        [Fact]
        public void OnlyTheStoreCopyAsksForARating()
        {
            Assert.True(StoreLogic.CanRate(InstallKind.Store));
            Assert.False(StoreLogic.CanRate(InstallKind.Installed));
            Assert.False(StoreLogic.CanRate(InstallKind.Portable));
            foreach (var kind in new[] { InstallKind.Installed, InstallKind.Portable })
            {
                Assert.False(StoreLogic.RatingDue(kind, false, 100, true));
                Assert.Equal(0, StoreLogic.CountCopy(kind, false, 0)); // nothing counted outside the Store copy
            }
        }

        [Fact]
        public void AsksOnceAfterTheFifthCopy()
        {
            Assert.Equal(5, StoreLogic.RatingAfterCopies);
            var copies = 0;
            for (var i = 1; i < StoreLogic.RatingAfterCopies; i++)
            {
                copies = StoreLogic.CountCopy(InstallKind.Store, false, copies);
                Assert.Equal(i, copies);
                Assert.False(StoreLogic.RatingDue(InstallKind.Store, false, copies, true), "copy " + i);
            }
            copies = StoreLogic.CountCopy(InstallKind.Store, false, copies);
            Assert.True(StoreLogic.RatingDue(InstallKind.Store, false, copies, true));
            // Asked (either answer, or the dialog closed): never again, and the counter stops.
            Assert.False(StoreLogic.RatingDue(InstallKind.Store, true, copies, true));
            Assert.Equal(copies, StoreLogic.CountCopy(InstallKind.Store, true, copies));
        }

        [Fact]
        public void NeverAsksOverAnotherProgram()
        {
            // Five hotkey copies from other programs: not now, but the next copy in the window asks.
            Assert.False(StoreLogic.RatingDue(InstallKind.Store, false, 5, inActiveWindow: false));
            var copies = StoreLogic.CountCopy(InstallKind.Store, false, 5);
            Assert.True(StoreLogic.RatingDue(InstallKind.Store, false, copies, inActiveWindow: true));
        }

        [Theory]
        [InlineData(5, 5)] // stops at the threshold: settings.json holds a tiny number, not a usage history
        [InlineData(1000, 5)]
        [InlineData(-3, 1)] // a hand-edited settings.json
        public void TheCounterStaysSmall(int before, int after) =>
            Assert.Equal(after, StoreLogic.CountCopy(InstallKind.Store, false, before));

        /// <summary>The numbers mirror Windows.Services.Store.StoreRateAndReviewStatus (cast directly in StoreRating).</summary>
        [Fact]
        public void RatingStatesMatchWindows()
        {
            Assert.Equal(0, (int)RateReviewStatus.Succeeded);
            Assert.Equal(1, (int)RateReviewStatus.CanceledByUser);
            Assert.Equal(2, (int)RateReviewStatus.NetworkError);
            Assert.Equal(3, (int)RateReviewStatus.Error);
        }

        [Fact]
        public void OpensTheReviewPageOnlyWhenTheStoreDialogCannot()
        {
            Assert.False(StoreLogic.OpenReviewPageInstead(RateReviewStatus.Succeeded));
            Assert.False(StoreLogic.OpenReviewPageInstead(RateReviewStatus.CanceledByUser)); // the user said no: respect it
            Assert.True(StoreLogic.OpenReviewPageInstead(RateReviewStatus.NetworkError));
            Assert.True(StoreLogic.OpenReviewPageInstead(RateReviewStatus.Error));
        }

        /// <summary>The review page is this app's: the Store ID from Partner Center (identity.json).</summary>
        [Fact]
        public void ReviewPageUsesTheStoreId()
        {
            var identity = File.ReadAllText(Path.Combine(TestStrings.WindowsDir, "packaging", "msix", "identity.json"));
            var m = Regex.Match(identity, "\"StoreId\"\\s*:\\s*\"([^\"]+)\"");
            Assert.True(m.Success, "StoreId in identity.json");
            Assert.Equal(StoreLogic.StoreProductId, m.Groups[1].Value);
            Assert.Equal(StoreLogic.ReviewUri, "ms-windows-store://review/?ProductId=" + m.Groups[1].Value);
        }
    }
}
