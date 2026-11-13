using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace InstaWords.Core.Internal
{
    /// <summary>
    /// An engine bound to a units catalog: src/index.ts createTafqit + src/engine.ts phrase +
    /// src/catalog.ts lookup. The catalog is validated by the generator (src/catalog.ts).
    /// </summary>
    internal sealed class Engine
    {
        private readonly UnitData[] _units;
        private readonly Dictionary<string, int> _index = new Dictionary<string, int>(StringComparer.Ordinal);

        internal Engine(UnitData[] units)
        {
            _units = units;
            var infos = new UnitInfo[units.Length];
            for (int i = 0; i < units.Length; i++)
            {
                var u = units[i];
                foreach (var key in u.Keys)
                {
                    if (_index.TryGetValue(key, out int other) && other != i) throw new InvalidOperationException($"duplicate unit name \"{key}\"");
                    _index[key] = i;
                }
                infos[i] = new UnitInfo(u.Id, u.Category, u.Label, u.Code, Array.AsReadOnly((string[])u.Aliases.Clone()),
                    u.Subunit != null, u.Subunit?.Digits ?? 0, u.DisplayDigits, u.En?.Singular, u.Fr?.Singular);
            }
            Units = new ReadOnlyCollection<UnitInfo>(infos);
        }

        internal IReadOnlyList<UnitInfo> Units { get; }

        private int Find(string name) => _index.TryGetValue(JsText.UnitKey(name), out int i) ? i : -1;

        internal UnitInfo? FindUnit(string? name)
        {
            if (name == null) return null;
            int i = Find(name);
            return i < 0 ? null : Units[i];
        }

        private UnitData GetUnit(string? name)
        {
            int i = Find(string.IsNullOrEmpty(name) ? "none" : name!);
            if (i < 0) throw new TafqitException(TafqitErrorCodes.UnknownUnit, name);
            return _units[i];
        }

        private static int ResolveLanguage(string? v)
        {
            if (string.IsNullOrEmpty(v)) return 0;
            for (int i = 0; i < LanguageModule.All.Length; i++) if (LanguageModule.All[i].Id == v) return i;
            throw new TafqitException(TafqitErrorCodes.InvalidOption, "language=" + v);
        }

        private static int ResolveHundreds(string? v)
        {
            if (string.IsNullOrEmpty(v)) return 0;
            int i = Array.IndexOf(ArabicWords.HundredsForms, v);
            if (i < 0) throw new TafqitException(TafqitErrorCodes.InvalidOption, "hundredsForm=" + v);
            return i;
        }

        internal string Convert(string? input, TafqitOptions? options) => Run(input, options, null);

        /// <summary>tafqit() with the warnings collected in <paramref name="warnings"/> (when not null).</summary>
        private string Run(string? input, TafqitOptions? options, List<string>? warnings)
        {
            var o = options ?? new TafqitOptions();
            var language = LanguageModule.All[ResolveLanguage(o.Language)];
            var unit = GetUnit(o.Unit);
            // Validated for every language, but only Arabic uses it.
            int hf = ResolveHundreds(o.HundredsForm);
            var number = Normalizer.Normalize(input, language.Id == "fr");
            string decimals = o.Decimals ?? "round";
            if (decimals != "round" && decimals != "error") throw new TafqitException(TafqitErrorCodes.InvalidOption, "decimals=" + decimals);
            string text = Phrase(number, unit, hf, decimals == "error", language, warnings);
            return JsText.JoinNonEmpty(o.Prefix == null ? "" : JsText.Trim(o.Prefix), text, o.Suffix == null ? "" : JsText.Trim(o.Suffix));
        }

        internal TafqitResult TryConvert(string? input, TafqitOptions? options)
        {
            string? cleaned = null;
            try
            {
                // The separator convention of the output language; Arabic's when the language is invalid.
                cleaned = Normalizer.Normalize(input, options?.Language == "fr").Cleaned;
            }
            catch (TafqitException)
            {
                // reported below
            }
            try
            {
                var warnings = new List<string>();
                string text = Run(input, options, warnings);
                return new TafqitResult(true, text, cleaned, null, null, warnings.AsReadOnly());
            }
            catch (TafqitException e)
            {
                return new TafqitResult(false, "", cleaned, e.Code, e.Message, null, e.Readings);
            }
        }

        /// <summary>src/engine.ts roundHalfUp: half-up to d fraction digits, on strings.</summary>
        internal static (string Integer, string Subunit) RoundHalfUp(string integer, string fraction, int d)
        {
            string head = fraction.Length >= d ? fraction.Substring(0, d) : fraction.PadRight(d, '0');
            string digits = integer + head;
            if ((fraction.Length > d ? fraction[d] : '0') >= '5') digits = Digits.Increment(digits);
            string intPart = Digits.TrimLeadingZeros(digits.Substring(0, digits.Length - d));
            if (intPart.Length > ArabicWords.MaxIntegerDigits) throw new TafqitException(TafqitErrorCodes.TooLarge);
            return (intPart, digits.Substring(digits.Length - d));
        }

        /// <summary>src/engine.ts phrase: the full phrase without prefix/suffix.</summary>
        private static string Phrase(NormalizedNumber number, UnitData unit, int hf, bool decimalsError, LanguageModule lang, List<string>? warnings)
        {
            string integer = number.Integer;
            string fraction = number.Fraction;

            if (unit.Forms == null)
            {
                if (fraction.Length > 15) throw new TafqitException(TafqitErrorCodes.TooManyDecimals);
                return lang.Plain(integer, fraction, hf);
            }

            var forms = lang.PickForms(unit);
            if (forms == null) throw new TafqitException(TafqitErrorCodes.NoLanguageForms, unit.Id + ": " + lang.Id);

            string sub = "0";
            if (fraction.Length > 0)
            {
                if (unit.Subunit == null) throw new TafqitException(TafqitErrorCodes.FractionNotSupported, unit.Label);
                int d = unit.Subunit.Digits;
                string subDigits = fraction.PadRight(d, '0');
                if (fraction.Length > d)
                {
                    if (decimalsError) throw new TafqitException(TafqitErrorCodes.TooManyDecimals, unit.Label + ": " + d.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    (integer, subDigits) = RoundHalfUp(integer, fraction, d);
                }
                sub = Digits.TrimLeadingZeros(subDigits);
                // A non-zero amount below half the smallest subunit (0.004 SAR) is written «صفر»: say so.
                if (integer == "0" && sub == "0") warnings?.Add(TafqitWarningCodes.RoundedToZero);
            }

            if (sub == "0") return lang.Count(integer, forms, hf);
            var subForms = lang.PickForms(unit.Subunit!);
            if (subForms == null) throw new TafqitException(TafqitErrorCodes.NoLanguageForms, unit.Id + ".subunit: " + lang.Id);
            string subText = lang.Count(sub, subForms, hf);
            if (integer == "0") return subText;
            return lang.Count(integer, forms, hf) + lang.SubunitJoiner + subText;
        }
    }
}
