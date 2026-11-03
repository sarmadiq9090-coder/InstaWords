using System;
using Microsoft.Win32;

namespace InstaWords.App.Services
{
    /// <summary>«تشغيل مع Windows»: the per-user Run key (no admin rights needed).</summary>
    public static class StartupService
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string ValueName = "InstaWords";
        public const string TrayArgument = "--tray";

        private static string Command => "\"" + AppPaths.ExePath + "\" " + TrayArgument;

        public static bool IsEnabled
        {
            get
            {
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                        return key?.GetValue(ValueName) is string v && v.IndexOf(AppPaths.ExePath, StringComparison.OrdinalIgnoreCase) >= 0;
                }
                catch { return false; }
            }
        }

        public static bool SetEnabled(bool enabled)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RunKey, true))
                {
                    if (key == null) return false;
                    if (enabled) key.SetValue(ValueName, Command, RegistryValueKind.String);
                    else if (key.GetValue(ValueName) != null) key.DeleteValue(ValueName, false);
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("Run key: " + ex.Message);
                return false;
            }
        }
    }
}
