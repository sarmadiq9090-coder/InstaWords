using System;

// Pure logic (no WPF, no Windows API): compiled into the app and linked into InstaWords.App.Tests.
// The Windows side is AppPackage.cs (package detection) and StartupService.cs (the startup task).
namespace InstaWords.App.Services
{
    /// <summary>How this copy of InstaWords was installed (problem reports, updater, start with Windows).</summary>
    public enum InstallKind
    {
        /// <summary>The portable zip, or a build run from anywhere else.</summary>
        Portable,
        /// <summary>The Inno Setup installer (windows/installer/InstaWords.iss).</summary>
        Installed,
        /// <summary>The MSIX package (Microsoft Store, or a sideloaded test package).</summary>
        Store,
    }

    /// <summary>StartupTaskState (Windows.ApplicationModel), as numbers so this file needs no WinRT.</summary>
    public enum StartupTaskStatus
    {
        Disabled = 0,
        DisabledByUser = 1,
        Enabled = 2,
        DisabledByPolicy = 3,
        EnabledByPolicy = 4,
    }

    /// <summary>What the «تشغيل مع Windows» check box shows.</summary>
    public sealed class StartupView
    {
        public StartupView(bool isChecked, bool canChange, string noteKey)
        {
            IsChecked = isChecked;
            CanChange = canChange;
            NoteKey = noteKey;
        }

        public bool IsChecked { get; }
        public bool CanChange { get; }
        /// <summary>A string key explaining why the box cannot be changed, or null.</summary>
        public string NoteKey { get; }
    }

    public static class StoreLogic
    {
        /// <summary>GetCurrentPackageFullName: the process has no package identity.</summary>
        public const int AppModelErrorNoPackage = 15700;
        public const int ErrorInsufficientBuffer = 122;
        public const int ErrorSuccess = 0;

        /// <summary>The startup task's TaskId: must match uap5:StartupTask in windows/packaging/msix/AppxManifest.xml.</summary>
        public const string StartupTaskId = "InstaWordsStartup";

        /// <summary>
        /// Reads the result of GetCurrentPackageFullName(ref length, null): a packaged process gets
        /// ERROR_INSUFFICIENT_BUFFER and the needed length; an unpackaged one APPMODEL_ERROR_NO_PACKAGE.
        /// Anything else (an unexpected error) counts as not packaged, which keeps the classic behaviour.
        /// </summary>
        public static bool IsPackagedResult(int result, int length) =>
            (result == ErrorInsufficientBuffer || result == ErrorSuccess) && length > 0;

        public static InstallKind Kind(bool packaged, bool installedBySetup) =>
            packaged ? InstallKind.Store : installedBySetup ? InstallKind.Installed : InstallKind.Portable;

        /// <summary>The «نوع التثبيت» shown in problem reports.</summary>
        public static string TechKey(InstallKind kind) =>
            kind == InstallKind.Store ? "Tech.Store" : kind == InstallKind.Installed ? "Tech.Installed" : "Tech.Portable";

        /// <summary>Only the Microsoft Store updates a Store copy: no GitHub check, no download, no installer.</summary>
        public static bool SelfUpdates(InstallKind kind) => kind != InstallKind.Store;

        /// <summary>
        /// Where a packaged app's files under %APPDATA% really are on disk. MSIX redirects new files the app
        /// writes there to %LOCALAPPDATA%\Packages\&lt;family name&gt;\LocalCache\Roaming\…; the app itself still
        /// sees the classic path, but Explorer (outside the package) does not. Returns null when
        /// <paramref name="dataDir"/> is not under <paramref name="roamingAppData"/> or an input is missing.
        /// </summary>
        public static string PackagedDataDir(string dataDir, string roamingAppData, string localAppData, string packageFamilyName)
        {
            if (string.IsNullOrWhiteSpace(dataDir) || string.IsNullOrWhiteSpace(roamingAppData)
                || string.IsNullOrWhiteSpace(localAppData) || string.IsNullOrWhiteSpace(packageFamilyName))
                return null;
            var root = roamingAppData.TrimEnd('\\', '/');
            var dir = dataDir.TrimEnd('\\', '/');
            if (!dir.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return null;
            var rest = dir.Substring(root.Length);
            if (rest.Length > 0 && rest[0] != '\\' && rest[0] != '/') return null; // "...\RoamingX" is not under "...\Roaming"
            return localAppData.TrimEnd('\\', '/') + @"\Packages\" + packageFamilyName + @"\LocalCache\Roaming" + rest.Replace('/', '\\');
        }

        /// <summary>The check box for a startup task state.</summary>
        public static StartupView DescribeStartupTask(StartupTaskStatus state)
        {
            switch (state)
            {
                case StartupTaskStatus.Enabled: return new StartupView(true, true, null);
                case StartupTaskStatus.EnabledByPolicy: return new StartupView(true, false, "Settings.StartupByPolicy");
                case StartupTaskStatus.DisabledByPolicy: return new StartupView(false, false, "Settings.StartupByPolicy");
                // Turned off in Task Manager or Settings: only the user can turn it back on, there.
                case StartupTaskStatus.DisabledByUser: return new StartupView(false, false, "Settings.StartupDisabledByUser");
                default: return new StartupView(false, true, null);
            }
        }
    }
}
