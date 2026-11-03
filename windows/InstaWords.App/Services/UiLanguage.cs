using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace InstaWords.App.Services
{
    /// <summary>
    /// Loads Strings/Strings.{ar,en}.xaml into the application resources (so every
    /// {DynamicResource} updates at once) and into <see cref="Loc"/> for code.
    /// </summary>
    public static class UiLanguage
    {
        /// <summary>Where the installer writes the language chosen in its wizard (HKCU, per user).</summary>
        public const string InstallerKey = @"Software\FastKiwi\InstaWords";
        public const string InstallerValue = "InstallerLanguage";

        private static ResourceDictionary _current;

        /// <summary>Captured once at startup, before anything could change the thread culture.</summary>
        public static readonly string WindowsDisplayLanguage = CultureInfo.CurrentUICulture.Name;

        public static FlowDirection Flow => Loc.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        public static ResourceDictionary Load(string language)
        {
            var uri = new Uri("/InstaWords;component/Strings/Strings." + (language == Loc.English ? "en" : "ar") + ".xaml", UriKind.Relative);
            return (ResourceDictionary)Application.LoadComponent(uri);
        }

        /// <summary>Applies the interface language for a setting ("ar", "en" or "auto").</summary>
        public static void Apply(string setting)
        {
            // "auto": the Windows display language of this user (CurrentUICulture at startup).
            var language = Loc.Resolve(Loc.NormalizeSetting(setting), WindowsDisplayLanguage);
            if (_current != null && Loc.Language == language) return;

            var dict = Load(language);
            var merged = Application.Current.Resources.MergedDictionaries;
            var old = _current ?? merged.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.IndexOf("Strings.", StringComparison.OrdinalIgnoreCase) >= 0);
            if (old != null) merged.Remove(old);
            merged.Add(dict);
            _current = dict;

            var table = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (DictionaryEntry e in dict)
                if (e.Key is string k && e.Value is string v) table[k] = v;
            Loc.Set(language, table);
        }

        /// <summary>The language chosen in the installer's wizard, as a setting ("ar"/"en"), or null.</summary>
        public static string InstallerChoice()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(InstallerKey))
                    return Loc.FromInstallerLanguage(key?.GetValue(InstallerValue) as string);
            }
            catch
            {
                return null;
            }
        }
    }
}
