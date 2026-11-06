using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace InstaWords.App.Services
{
    /// <summary>Technical details for problem reports. Deliberately excludes amounts, clipboard and history.</summary>
    public static class SystemInfo
    {
        public static IList<KeyValuePair<string, string>> Collect()
        {
            var app = App.Current;
            var s = app.State;
            var list = new List<KeyValuePair<string, string>>
            {
                Pair(Loc.T("Tech.Version"), "InstaWords " + UpdateService.CurrentVersionText + " (" + Loc.T(StoreLogic.TechKey(AppPackage.Kind)) + ")"),
                Pair("Windows", WindowsVersion()),
                Pair(".NET Framework", DotNetRelease()),
                Pair(Loc.T("Tech.UiLanguage"), Loc.Language + " (" + app.Settings.UiLanguage + ") · Windows " + CultureInfo.CurrentUICulture.Name + " / " + CultureInfo.CurrentCulture.Name),
                Pair(Loc.T("Tech.Scale"), DisplayScale()),
                Pair(Loc.T("Tech.Options"), "language=" + s.Language + ", unit=" + s.Unit + ", hundreds=" + s.HundredsForm + ", decimals=" + s.Decimals),
                Pair(Loc.T("Tech.Hotkey"), app.Settings.HotkeyEnabled
                    ? app.CurrentHotkeyText + (app.Hotkey?.IsRegistered == true ? "" : " (" + Loc.T("Tech.NotRegistered") + ")")
                    : Loc.T("Tech.Off")),
            };
            if (AppPackage.IsPackaged) list.Insert(1, Pair("MSIX", AppPackage.FullName));
            if (!string.IsNullOrEmpty(app.LastErrorCode)) list.Add(Pair(Loc.T("Tech.LastError"), app.LastErrorCode));
            return list;
        }

        private static KeyValuePair<string, string> Pair(string k, string v) => new KeyValuePair<string, string>(k, v ?? "");

        public static string WindowsVersion()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    var product = key?.GetValue("ProductName") as string ?? "Windows";
                    var display = key?.GetValue("DisplayVersion") as string ?? key?.GetValue("ReleaseId") as string ?? "";
                    var build = key?.GetValue("CurrentBuild") as string ?? "";
                    var ubr = key?.GetValue("UBR");
                    // Windows 11 still reports "Windows 10" as ProductName.
                    if (int.TryParse(build, out var b) && b >= 22000) product = product.Replace("Windows 10", "Windows 11");
                    var arch = Environment.Is64BitOperatingSystem ? "x64" : "x86";
                    return (product + " " + display + " (build " + build + (ubr != null ? "." + ubr : "") + ", " + arch + ")").Replace("  ", " ");
                }
            }
            catch
            {
                return Environment.OSVersion.VersionString;
            }
        }

        public static string DotNetRelease()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
                {
                    if (!(key?.GetValue("Release") is int release)) return Environment.Version.ToString();
                    var name = release >= 533320 ? "4.8.1" : release >= 528040 ? "4.8" : release >= 461808 ? "4.7.2" : "< 4.7.2";
                    return name + " (release " + release + ")";
                }
            }
            catch
            {
                return Environment.Version.ToString();
            }
        }

        private static string DisplayScale()
        {
            try
            {
                var w = Application.Current?.MainWindow;
                if (w == null) return "";
                var dpi = VisualTreeHelper.GetDpi(w);
                return Math.Round(dpi.DpiScaleX * 100) + "%";
            }
            catch
            {
                return "";
            }
        }
    }
}
