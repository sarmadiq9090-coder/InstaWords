using System.IO;
using System.Text.RegularExpressions;
using InstaWords.App.Services;
using Xunit;

namespace InstaWords.App.Tests
{
    /// <summary>Store mode (MSIX): package detection, install kind, data folder, start with Windows.</summary>
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
    }
}
