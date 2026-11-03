using System;
using System.Collections.Generic;
using System.Globalization;

// Pure logic (no WPF): compiled into the app and linked into InstaWords.App.Tests.
namespace InstaWords.App.Services
{
    /// <summary>
    /// The interface language («لغة الواجهة»): Arabic or English. The strings live in
    /// Strings/Strings.ar.xaml and Strings/Strings.en.xaml (one key each, same keys in both).
    /// XAML reads them with {DynamicResource Key}, so a switch applies live; code reads them here.
    /// The tafqit OUTPUT language (ar/en/fr) is a separate option and never follows this one.
    /// </summary>
    public static class Loc
    {
        public const string Arabic = "ar";
        public const string English = "en";
        public const string Auto = "auto";

        private static IReadOnlyDictionary<string, string> _table = new Dictionary<string, string>();

        /// <summary>"ar" or "en": the language the strings are currently in.</summary>
        public static string Language { get; private set; } = Arabic;

        public static bool IsEnglish => Language == English;
        public static bool IsRightToLeft => !IsEnglish;

        /// <summary>Raised after the language changed, for text that code (not XAML) has set.</summary>
        public static event EventHandler Changed;

        /// <summary>Installs the strings of <paramref name="language"/> (called by the app, or by tests).</summary>
        public static void Set(string language, IReadOnlyDictionary<string, string> table)
        {
            var changed = Language != language || !ReferenceEquals(_table, table);
            Language = language == English ? English : Arabic;
            _table = table ?? new Dictionary<string, string>();
            if (changed) Changed?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>The string for <paramref name="key"/>; the key itself when missing (visible, never a crash).</summary>
        public static string T(string key) => key != null && _table.TryGetValue(key, out var v) ? v : key;

        public static bool Has(string key) => key != null && _table.ContainsKey(key);

        /// <summary>A string with {0}, {1}… placeholders filled in.</summary>
        public static string F(string key, params object[] args)
        {
            var format = T(key);
            try
            {
                return string.Format(CultureInfo.InvariantCulture, format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }

        /// <summary>
        /// The language to use for a setting: "ar", "en", or "auto" (Windows display language:
        /// Arabic for any Arabic locale, English for everything else).
        /// </summary>
        public static string Resolve(string setting, string windowsUiCulture)
        {
            if (setting == Arabic || setting == English) return setting;
            var name = (windowsUiCulture ?? "").Trim();
            return name.Equals("ar", StringComparison.OrdinalIgnoreCase) || name.StartsWith("ar-", StringComparison.OrdinalIgnoreCase)
                ? Arabic : English;
        }

        /// <summary>A stored setting made valid: "ar", "en" or "auto".</summary>
        public static string NormalizeSetting(string setting) =>
            setting == Arabic || setting == English || setting == Auto ? setting : Auto;

        /// <summary>The installer's language name (Inno Setup «{language}»: arabic / english) as a setting, or null.</summary>
        public static string FromInstallerLanguage(string innoLanguage)
        {
            switch ((innoLanguage ?? "").Trim().ToLowerInvariant())
            {
                case "arabic": return Arabic;
                case "english": return English;
                default: return null;
            }
        }

        /// <summary>
        /// The message for a tafqit error code in the interface language. In Arabic the engine's own
        /// message is used as is (identical to the website and Excel); in English it comes from «Err.CODE».
        /// </summary>
        public static string Error(string code, string engineMessage)
        {
            if (!IsEnglish && !string.IsNullOrEmpty(engineMessage)) return engineMessage;
            var key = "Err." + (code ?? "");
            if (Has(key)) return T(key);
            return string.IsNullOrEmpty(engineMessage) ? T("Err.Generic") : engineMessage;
        }
    }
}
