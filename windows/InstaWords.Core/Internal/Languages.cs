using System.Collections.Generic;

namespace InstaWords.Core.Internal
{
    /// <summary>src/lang/types.ts LanguageModule.</summary>
    internal abstract class LanguageModule
    {
        internal abstract string Id { get; }

        /// <summary>Forms of a unit in this language, or null when the catalog has none.</summary>
        internal abstract Forms? PickForms(UnitData unit);

        /// <summary>Forms of a subunit in this language, or null when the catalog has none.</summary>
        internal abstract Forms? PickForms(SubunitData subunit);

        /// <summary>A number with no counted noun. <paramref name="fraction"/>: digits after the point, "" when none.</summary>
        internal abstract string Plain(string integer, string fraction, int hf);

        /// <summary>A number (digits, no leading zeros) followed by its counted noun.</summary>
        internal abstract string Count(string digits, Forms forms, int hf);

        internal abstract string SubunitJoiner { get; }

        /// <summary>Modules in src/lang/index.ts order (the generator checks it is ar, en, fr).</summary>
        internal static readonly LanguageModule[] All = { new ArabicLanguage(), new EnglishLanguage(), new FrenchLanguage() };
    }

    /// <summary>src/lang/ar.ts (docs/SPEC.md §3–§11).</summary>
    internal sealed class ArabicLanguage : LanguageModule
    {
        internal override string Id => "ar";
        internal override string SubunitJoiner => ArabicWords.Waw;
        internal override Forms? PickForms(UnitData unit) => unit.Forms;
        internal override Forms? PickForms(SubunitData subunit) => subunit.Forms;

        /// <summary>§3 — words for 1..999. construct: 200 exactly is followed directly by a scale word.</summary>
        private static string GroupWords(int g, bool feminine, int hf, bool construct)
        {
            int h = g / 100, r = g % 100;
            var parts = new List<string>(2);
            if (h > 0) parts.Add(h == 2 && r == 0 && construct ? ArabicWords.HundredsDualConstruct[hf] : ArabicWords.Hundreds[hf][h]);
            if (r >= 1 && r <= 10) parts.Add((feminine ? ArabicWords.OnesF : ArabicWords.OnesM)[r]);
            else if (r >= 11 && r <= 19) parts.Add((feminine ? ArabicWords.TeensF : ArabicWords.TeensM)[r - 10]);
            else if (r >= 20)
            {
                int u = r % 10, t = r / 10;
                parts.Add(u == 0
                    ? ArabicWords.Tens[t]
                    : (feminine ? ArabicWords.CompoundOnesF : ArabicWords.CompoundOnesM)[u] + ArabicWords.Waw + ArabicWords.Tens[t]);
            }
            return string.Join(ArabicWords.Waw, parts);
        }

        /// <summary>§4 + §6 — a scale group (thousands and up).</summary>
        private static string ScaleTerm(int g, int k, int hf, bool construct)
        {
            if (g == 1) return ArabicWords.ScaleSingular[k];
            if (g == 2) return ArabicWords.ScaleDual[k];
            int r = g % 100;
            string noun;
            if (r <= 2) noun = ArabicWords.ScaleSingular[k];
            else if (r <= 10) noun = ArabicWords.ScalePlural[k];
            else noun = construct ? ArabicWords.ScaleSingular[k] : ArabicWords.ScaleAccusative[k];
            return GroupWords(g, false, hf, true) + " " + noun;
        }

        /// <summary>§7 — integer words without the counted noun. nounFollows triggers §6.2.</summary>
        private static string IntegerWords(string digits, bool feminine, int hf, bool nounFollows)
        {
            if (digits == "0") return ArabicWords.Zero;
            int[] groups = Digits.SplitGroups(digits);
            var parts = new List<string>(groups.Length);
            for (int k = groups.Length - 1; k >= 1; k--)
            {
                int g = groups[k];
                if (g == 0) continue;
                bool isLast = true;
                for (int j = 0; j < k; j++) if (groups[j] != 0) { isLast = false; break; }
                parts.Add(ScaleTerm(g, k, hf, isLast && nounFollows));
            }
            if (groups[0] > 0) parts.Add(GroupWords(groups[0], feminine, hf, false));
            return string.Join(ArabicWords.Waw, parts);
        }

        /// <summary>§5 — number + counted noun.</summary>
        internal override string Count(string digits, Forms f, int hf)
        {
            var forms = (NounForms)f;
            if (digits == "0") return ArabicWords.Zero + " " + forms.Singular;
            if (digits == "1") return forms.One ?? forms.Singular + " " + (forms.Feminine ? ArabicWords.OnesF : ArabicWords.OnesM)[1];
            if (digits == "2") return forms.Dual;
            int r = Digits.LastTwo(digits);
            string noun;
            if (r <= 2) noun = forms.Singular;
            else if (r <= 10) noun = forms.Plural;
            else noun = forms.Accusative;
            return IntegerWords(digits, forms.Feminine, hf, true) + " " + noun;
        }

        /// <summary>§10 — fraction digits for the no-unit case: leading zeros read as صفر.</summary>
        private static string FractionDigitsWords(string fraction, int hf)
        {
            int lead = Digits.LeadingZeros(fraction);
            var words = new List<string>(lead + 1);
            for (int i = 0; i < lead; i++) words.Add(ArabicWords.Zero);
            if (lead < fraction.Length) words.Add(IntegerWords(fraction.Substring(lead), false, hf, false));
            return string.Join(" ", words);
        }

        internal override string Plain(string integer, string fraction, int hf)
        {
            string intText = IntegerWords(integer, false, hf, false);
            return fraction.Length > 0 ? intText + " " + ArabicWords.DecimalPoint + " " + FractionDigitsWords(fraction, hf) : intText;
        }
    }

    /// <summary>src/lang/en.ts (docs/SPEC.md §17.1).</summary>
    internal sealed class EnglishLanguage : LanguageModule
    {
        internal override string Id => "en";
        internal override string SubunitJoiner => EnglishWords.SubunitJoiner;
        internal override Forms? PickForms(UnitData unit) => unit.En;
        internal override Forms? PickForms(SubunitData subunit) => subunit.En;

        private static string GroupWords(int g)
        {
            int h = g / 100, r = g % 100;
            var parts = new List<string>(2);
            if (h > 0) parts.Add(EnglishWords.Small[h] + " " + EnglishWords.Hundred);
            if (r > 0 && r < 20) parts.Add(EnglishWords.Small[r]);
            else if (r >= 20) parts.Add(EnglishWords.Tens[r / 10] + (r % 10 != 0 ? EnglishWords.Hyphen + EnglishWords.Small[r % 10] : ""));
            return string.Join(" ", parts);
        }

        private static string IntegerWords(string digits)
        {
            if (digits == "0") return EnglishWords.Small[0];
            int[] groups = Digits.SplitGroups(digits);
            var parts = new List<string>(groups.Length);
            for (int k = groups.Length - 1; k >= 0; k--)
            {
                int g = groups[k];
                if (g == 0) continue;
                parts.Add(k == 0 ? GroupWords(g) : GroupWords(g) + " " + EnglishWords.Scales[k]);
            }
            return string.Join(" ", parts);
        }

        internal override string Plain(string integer, string fraction, int hf)
        {
            string intText = IntegerWords(integer);
            if (fraction.Length == 0) return intText;
            // Digits after the point are read one by one: Twelve Point Zero Five.
            var words = new string[fraction.Length];
            for (int i = 0; i < fraction.Length; i++) words[i] = EnglishWords.Small[fraction[i] - '0'];
            return intText + " " + EnglishWords.Point + " " + string.Join(" ", words);
        }

        internal override string Count(string digits, Forms f, int hf)
        {
            var forms = (ForeignForms)f;
            if (digits == "1") return forms.One ?? EnglishWords.Small[1] + " " + forms.Singular;
            return IntegerWords(digits) + " " + forms.Plural;
        }
    }

    /// <summary>src/lang/fr.ts (docs/SPEC.md §17.2). Traditional orthography, lowercase.</summary>
    internal sealed class FrenchLanguage : LanguageModule
    {
        internal override string Id => "fr";
        internal override string SubunitJoiner => FrenchWords.SubunitJoiner;
        internal override Forms? PickForms(UnitData unit) => unit.Fr;
        internal override Forms? PickForms(SubunitData subunit) => subunit.Fr;

        private static string One(bool feminine) => feminine ? FrenchWords.OneF : FrenchWords.Small[1];

        /// <summary>1..99. final: no other number word follows — then 80 is written quatre-vingts.</summary>
        private static string Below100(int n, bool feminine, bool final)
        {
            if (n == 1) return One(feminine);
            if (n < 20) return FrenchWords.Small[n];
            int t = n / 10, u = n % 10;
            if (t == 7 || t == 9)
            {
                string b = t == 7 ? FrenchWords.Tens[6] : FrenchWords.FourTwenty;
                return t == 7 && u == 1 ? b + FrenchWords.Et + FrenchWords.Small[11] : b + FrenchWords.Hyphen + FrenchWords.Small[10 + u];
            }
            if (t == 8)
            {
                return u == 0
                    ? FrenchWords.FourTwenty + (final ? FrenchWords.PluralS : "")
                    : FrenchWords.FourTwenty + FrenchWords.Hyphen + (u == 1 ? One(feminine) : FrenchWords.Small[u]);
            }
            if (u == 0) return FrenchWords.Tens[t];
            return u == 1 ? FrenchWords.Tens[t] + FrenchWords.Et + One(feminine) : FrenchWords.Tens[t] + FrenchWords.Hyphen + FrenchWords.Small[u];
        }

        /// <summary>1..999. cent takes s when multiplied and final with nothing after it in the group.</summary>
        private static string GroupWords(int g, bool feminine, bool final)
        {
            int h = g / 100, r = g % 100;
            var parts = new List<string>(2);
            if (h == 1) parts.Add(FrenchWords.Cent);
            else if (h > 1) parts.Add(FrenchWords.Small[h] + " " + FrenchWords.Cent + (r == 0 && final ? FrenchWords.PluralS : ""));
            if (r > 0) parts.Add(Below100(r, feminine, final));
            return string.Join(" ", parts);
        }

        /// <summary>Integer words. The gender applies to the last group only; higher groups are masculine.</summary>
        private static string IntegerWords(string digits, bool feminine)
        {
            if (digits == "0") return FrenchWords.Small[0];
            int[] groups = Digits.SplitGroups(digits);
            var parts = new List<string>(groups.Length);
            for (int k = groups.Length - 1; k >= 1; k--)
            {
                int g = groups[k];
                if (g == 0) continue;
                if (k == 1) parts.Add(g == 1 ? FrenchWords.Mille : GroupWords(g, false, false) + " " + FrenchWords.Mille);
                else parts.Add(g == 1
                    ? One(false) + " " + FrenchWords.ScaleNouns[k]
                    : GroupWords(g, false, true) + " " + FrenchWords.ScaleNouns[k] + FrenchWords.PluralS);
            }
            if (groups[0] > 0) parts.Add(GroupWords(groups[0], feminine, true));
            return string.Join(" ", parts);
        }

        internal override string Plain(string integer, string fraction, int hf)
        {
            string intText = IntegerWords(integer, false);
            if (fraction.Length == 0) return intText;
            // Leading zeros are read as zéro, the rest as a whole number: douze virgule zéro cinq.
            int lead = Digits.LeadingZeros(fraction);
            var words = new List<string>(lead + 1);
            for (int i = 0; i < lead; i++) words.Add(FrenchWords.Small[0]);
            if (lead < fraction.Length) words.Add(IntegerWords(fraction.Substring(lead), false));
            return intText + " " + FrenchWords.Point + " " + string.Join(" ", words);
        }

        internal override string Count(string digits, Forms f, int hf)
        {
            var forms = (ForeignForms)f;
            if (digits == "0") return FrenchWords.Small[0] + " " + forms.Singular;
            if (digits == "1") return forms.One ?? One(forms.Feminine) + " " + forms.Singular;
            string words = IntegerWords(digits, forms.Feminine);
            // Exactly N million/milliard/billion: un million de dirhams, deux milliards d'euros.
            string link = digits.EndsWith("000000", System.StringComparison.Ordinal)
                ? (forms.Plural.Length > 0 && CharClasses.FrenchElides(forms.Plural[0]) ? FrenchWords.DeElided : FrenchWords.De)
                : "";
            return words + " " + link + forms.Plural;
        }
    }
}
