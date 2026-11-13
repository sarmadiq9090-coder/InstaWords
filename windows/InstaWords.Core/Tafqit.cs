using System.Collections.Generic;
using System.Collections.ObjectModel;
using InstaWords.Core.Internal;

namespace InstaWords.Core
{
    /// <summary>
    /// Amounts in words (tafqit): Arabic, English and French, character for character the same
    /// as the TypeScript core (dist/index.js). Every amount is handled as text, never as a
    /// floating-point or decimal number. Thread-safe.
    /// </summary>
    public static class Tafqit
    {
        private static readonly Engine Default = new Engine(BuiltInUnits.Create());

        /// <summary>
        /// Converts a number to words (src/index.ts tafqit). A null input is treated as "" (EMPTY).
        /// </summary>
        /// <exception cref="TafqitException">Invalid input or options; see <see cref="TafqitErrorCodes"/>.</exception>
        public static string Convert(string input, TafqitOptions? options = null) => Default.Convert(input, options);

        /// <summary>Same as <see cref="Convert"/>, but never throws a <see cref="TafqitException"/> (src/index.ts tryTafqit).</summary>
        public static TafqitResult TryConvert(string input, TafqitOptions? options = null) => Default.TryConvert(input, options);

        /// <summary>
        /// Canonical form of a number: digits only, no leading zeros, fraction without trailing zeros
        /// ("٠٠١٬٢٣٤٫٥٠" → "1234.5"); normalizeNumber(input).cleaned in the core.
        /// </summary>
        /// <exception cref="TafqitException">EMPTY, INVALID_FORMAT, NEGATIVE, AMBIGUOUS_SEPARATOR or TOO_LARGE.</exception>
        public static string Clean(string input) => Normalizer.Normalize(input, false).Cleaned;

        /// <summary>
        /// <see cref="Clean(string)"/> with the separator convention of an output language (normalizeNumber(input, language)):
        /// for "fr", "12,50" is 12.5; for "ar" and "en" (and null or "") it is AMBIGUOUS_SEPARATOR (docs/SPEC.md §1.1).
        /// </summary>
        /// <exception cref="TafqitException">INVALID_OPTION for an unknown language, or as <see cref="Clean(string)"/>.</exception>
        public static string Clean(string input, string? language)
        {
            if (!string.IsNullOrEmpty(language) && language != "ar" && language != "en" && language != "fr")
            {
                throw new TafqitException(TafqitErrorCodes.InvalidOption, "language=" + language);
            }
            return Normalizer.Normalize(input, language == "fr").Cleaned;
        }

        /// <summary>Every unit of the catalog, in data/units.json order.</summary>
        public static IReadOnlyList<UnitInfo> Units => Default.Units;

        /// <summary>
        /// The unit with this id, code, label or alias (trimmed, spaces collapsed, Latin letters in
        /// any case), or null.
        /// </summary>
        public static UnitInfo? FindUnit(string name) => Default.FindUnit(name);

        /// <summary>Output languages in picker order (Arabic first, the default).</summary>
        public static IReadOnlyList<LanguageInfo> Languages { get; } = new ReadOnlyCollection<LanguageInfo>(LanguageData.All);

        /// <summary>Arabic names of the unit categories (<see cref="UnitInfo.Category"/>), in display order. Same labels as the website (site/assets/app.js CATEGORY_LABELS).</summary>
        public static IReadOnlyDictionary<string, string> CategoryLabels { get; } = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>
            {
                ["currency-arab"] = "عملات عربية",
                ["currency-world"] = "عملات عالمية",
                ["crypto"] = "عملات رقمية",
                ["generic-currency"] = "عملات بلا تحديد بلد",
                ["weight"] = "الأوزان",
                ["volume"] = "الأحجام والسعات",
                ["length"] = "الأطوال",
                ["area"] = "المساحات",
                ["time"] = "الزمن",
                ["count"] = "المعدودات",
                ["data"] = "سعة البيانات",
                ["speed"] = "السرعة",
                ["none"] = "بدون وحدة",
            });
    }
}
