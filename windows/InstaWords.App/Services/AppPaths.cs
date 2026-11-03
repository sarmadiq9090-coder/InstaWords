using System;
using System.IO;

namespace InstaWords.App.Services
{
    public static class AppPaths
    {
        /// <summary>
        /// %APPDATA%\FastKiwi\InstaWords, or the folder in INSTAWORDS_DATA_DIR
        /// (used by the UI tests so they never touch the user's real settings).
        /// </summary>
        public static string DataDir
        {
            get
            {
                var overridden = Environment.GetEnvironmentVariable("INSTAWORDS_DATA_DIR");
                if (!string.IsNullOrWhiteSpace(overridden)) return Path.GetFullPath(overridden);
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FastKiwi", "InstaWords");
            }
        }

        public static string SettingsFile => Path.Combine(DataDir, "settings.json");
        public static string HistoryFile => Path.Combine(DataDir, "history.json");

        public static bool IsIsolated => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("INSTAWORDS_DATA_DIR"));

        public static string ExePath => System.Reflection.Assembly.GetEntryAssembly()?.Location
                                        ?? System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
    }
}
