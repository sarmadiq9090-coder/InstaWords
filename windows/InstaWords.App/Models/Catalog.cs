using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using InstaWords.Core;

namespace InstaWords.App.Models
{
    /// <summary>A unit as shown in the picker: grouped by category, searchable in Arabic, English, French and by code.</summary>
    public sealed class UnitItem
    {
        public UnitItem(UnitInfo info, string categoryLabel, int categoryOrder)
        {
            Info = info;
            CategoryLabel = categoryLabel;
            CategoryOrder = categoryOrder;
            var parts = new List<string> { info.Id, info.Label, info.Code, info.EnglishName, info.FrenchName, categoryLabel };
            if (info.Aliases != null) parts.AddRange(info.Aliases);
            SearchKey = " " + string.Join(" ", parts.Where(p => !string.IsNullOrEmpty(p)).Select(Catalog.Fold)) + " ";
        }

        public UnitInfo Info { get; }
        public string Id => Info.Id;
        public string Label => Info.Label;
        public string Code => string.IsNullOrEmpty(Info.Code) ? null : Info.Code;
        public bool HasCode => Code != null;
        public string CategoryLabel { get; }
        public int CategoryOrder { get; }
        public string Display => HasCode ? Label + " (" + Code + ")" : Label;
        /// <summary>Folded, space-delimited text all search terms are matched against.</summary>
        public string SearchKey { get; }
        public string AutomationId => "unit-" + Id;

        public bool Matches(string[] terms)
        {
            foreach (var t in terms)
                if (SearchKey.IndexOf(t, StringComparison.Ordinal) < 0) return false;
            return true;
        }

        public override string ToString() => Display;
    }

    public sealed class LanguageItem
    {
        public LanguageItem(LanguageInfo info) => Info = info;
        public LanguageInfo Info { get; }
        public string Id => Info.Id;
        public string Label => Info.Label;
        public string AutomationId => "lang-" + Id;
        public FlowDirection Flow => Info.RightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        public override string ToString() => Label;
    }

    /// <summary>The engine's units and languages, prepared once for the UI.</summary>
    public static class Catalog
    {
        private static IReadOnlyList<UnitItem> _units;
        private static IReadOnlyList<LanguageItem> _languages;
        private static Dictionary<string, string> _categoryLabels;

        public static IReadOnlyList<LanguageItem> Languages =>
            _languages ?? (_languages = Tafqit.Languages.Select(l => new LanguageItem(l)).ToList());

        public static IReadOnlyList<UnitItem> Units
        {
            get
            {
                if (_units != null) return _units;
                var order = new Dictionary<string, int>();
                // Category order: as listed by the engine's labels, then by first appearance.
                foreach (var key in CategoryLabels.Keys) if (!order.ContainsKey(key)) order[key] = order.Count;
                foreach (var u in Tafqit.Units) if (!order.ContainsKey(u.Category ?? "")) order[u.Category ?? ""] = order.Count;
                _units = Tafqit.Units
                    .Select(u => new UnitItem(u, CategoryLabel(u.Category), order[u.Category ?? ""]))
                    .ToList();
                return _units;
            }
        }

        public static Dictionary<string, string> CategoryLabels
        {
            get
            {
                if (_categoryLabels != null) return _categoryLabels;
                var d = new Dictionary<string, string>();
                foreach (var kv in Tafqit.CategoryLabels) d[kv.Key] = kv.Value;
                return _categoryLabels = d;
            }
        }

        public static string CategoryLabel(string category) =>
            category != null && CategoryLabels.TryGetValue(category, out var label) ? label : "وحدات أخرى";

        public static UnitItem FindUnit(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var exact = Units.FirstOrDefault(u => u.Id == id);
            if (exact != null) return exact;
            var info = Tafqit.FindUnit(id);
            return info == null ? null : Units.FirstOrDefault(u => u.Id == info.Id);
        }

        public static LanguageItem FindLanguage(string id) =>
            Languages.FirstOrDefault(l => l.Id == id) ?? Languages.FirstOrDefault(l => l.Id == "ar") ?? Languages.FirstOrDefault();

        public static FlowDirection FlowFor(string languageId) => FindLanguage(languageId)?.Flow ?? FlowDirection.RightToLeft;

        public static bool SupportsHundredsForm(string languageId) => FindLanguage(languageId)?.Info.SupportsHundredsForm ?? false;

        /// <summary>
        /// Folds text for searching: lower case, no diacritics or tatweel, and the usual Arabic
        /// letter variants merged (أ إ آ ٱ → ا, ة → ه, ى → ي, ؤ → و, ئ → ي).
        /// </summary>
        public static string Fold(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var d = s.Normalize(NormalizationForm.FormD);
            var b = new StringBuilder(d.Length);
            foreach (var ch in d)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat == UnicodeCategory.NonSpacingMark || ch == 'ـ') continue;
                switch (ch)
                {
                    case 'أ': case 'إ': case 'آ': case 'ٱ': b.Append('ا'); break;
                    case 'ة': b.Append('ه'); break;
                    case 'ى': case 'ئ': b.Append('ي'); break;
                    case 'ؤ': b.Append('و'); break;
                    default: b.Append(char.ToLowerInvariant(ch)); break;
                }
            }
            return b.ToString();
        }

        public static string[] SearchTerms(string query) =>
            Fold(query ?? "").Split(new[] { ' ', '\t', '(', ')', ',', '،' }, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>Ready-made prefix/suffix per output language (same as the website).</summary>
    public static class Presets
    {
        public sealed class Preset
        {
            public Preset(string prefix, string suffix, string label) { Prefix = prefix; Suffix = suffix; Label = label; }
            public string Prefix { get; }
            public string Suffix { get; }
            public string Label { get; }
        }

        private static readonly Dictionary<string, Preset> Map = new Dictionary<string, Preset>
        {
            ["ar"] = new Preset("فقط", "لا غير", "فقط … لا غير"),
            ["en"] = new Preset("", "Only", "… Only"),
            ["fr"] = new Preset("Arrêtée la présente facture à la somme de", "", "Arrêtée la présente facture à la somme de …"),
        };

        public static Preset For(string language) => language != null && Map.TryGetValue(language, out var p) ? p : Map["ar"];
    }
}
