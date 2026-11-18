using System.Collections.Generic;
using System.Text;

namespace InstaWords.Core.Internal
{
    /// <summary>src/types.ts NormalizedNumber.</summary>
    internal readonly struct NormalizedNumber
    {
        internal NormalizedNumber(string integer, string fraction)
        {
            Integer = integer;
            Fraction = fraction;
        }

        /// <summary>Digits only, no leading zeros ("0" for zero).</summary>
        internal string Integer { get; }

        /// <summary>Fraction digits without trailing zeros ("" when none).</summary>
        internal string Fraction { get; }

        internal string Cleaned => Fraction.Length > 0 ? Integer + "." + Fraction : Integer;
    }

    /// <summary>Input cleaning, src/normalize.ts (docs/SPEC.md §1, separators §1.1). Pure string processing.</summary>
    internal static class Normalizer
    {
        private const char ArDecimal = '٫';   // ٫ always the decimal point
        private const char ArThousands = '٬'; // ٬ always a thousands separator
        private const char Space = ' ';           // a run of spaces between two digits

        /// <summary>
        /// normalizeNumber(input, language). <paramref name="decimalComma"/> is the French convention
        /// ("," is the decimal point); otherwise "," separates thousands and "." is the decimal point.
        /// </summary>
        internal static NormalizedNumber Normalize(string? input, bool decimalComma)
        {
            string s = input ?? "";
            // replace(EDGE_SPACES, "")
            int a = 0, b = s.Length - 1;
            while (a <= b && CharClasses.IsInputSpace(s[a])) a++;
            while (b >= a && CharClasses.IsInputSpace(s[b])) b--;
            if (b < a) throw new TafqitException(TafqitErrorCodes.Empty);
            s = s.Substring(a, b - a + 1);

            if (CharClasses.IsMinusSign(s[0])) throw new TafqitException(TafqitErrorCodes.Negative);
            if (s[0] == '+')
            {
                a = 1;
                while (a < s.Length && CharClasses.IsInputSpace(s[a])) a++;
                s = s.Substring(a);
            }

            // toAsciiDigits
            var body = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                int d = CharClasses.DigitValue(c);
                body.Append(d >= 0 ? (char)('0' + d) : c);
            }

            // Digit groups and the separators between them; spaces next to a separator are dropped.
            var groups = new List<StringBuilder> { new StringBuilder() };
            var seps = new StringBuilder();
            int digits = 0;
            bool space = false, afterSep = false;
            for (int i = 0; i < body.Length; i++)
            {
                char ch = body[i];
                if (ch >= '0' && ch <= '9')
                {
                    if (space)
                    {
                        seps.Append(Space);
                        groups.Add(new StringBuilder());
                    }
                    groups[groups.Count - 1].Append(ch);
                    digits++;
                    space = false;
                    afterSep = false;
                }
                else if (CharClasses.IsInputSpace(ch))
                {
                    if (!afterSep) space = true;
                }
                else if (ch == ',' || ch == '.' || ch == ArDecimal || ch == ArThousands)
                {
                    if (afterSep) throw new TafqitException(TafqitErrorCodes.InvalidFormat); // two separators in a row
                    seps.Append(ch);
                    groups.Add(new StringBuilder());
                    space = false;
                    afterSep = true;
                }
                else
                {
                    throw new TafqitException(TafqitErrorCodes.InvalidFormat);
                }
            }
            if (digits == 0) throw new TafqitException(TafqitErrorCodes.InvalidFormat);

            // The decimal point: index into seps, or -1.
            string sep = seps.ToString();
            string text = body.ToString();
            int k = sep.Length;
            char last = k > 0 ? sep[k - 1] : '\0';
            int dec = -1;
            if (sep.IndexOf(ArDecimal) >= 0)
            {
                // ٫ in every language: once, last, and never with ".".
                if (sep.IndexOf(ArDecimal) != k - 1 || sep.IndexOf('.') >= 0) throw new TafqitException(TafqitErrorCodes.InvalidFormat);
                dec = k - 1;
            }
            else if (!decimalComma)
            {
                // Arabic and English: "." is the decimal point and must come after every other separator.
                int dot = sep.IndexOf('.');
                if (dot >= 0)
                {
                    if (dot != k - 1) throw Ambiguous(text, last);
                    dec = dot;
                }
            }
            else if (last == ',' || last == '.')
            {
                // French: the last "," or "." is the decimal point when it is the only one of its kind.
                if (sep.IndexOf(last) == k - 1) dec = k - 1;
            }

            // Thousands separators: one kind, a first group of 1–3 digits, then groups of exactly 3.
            int end = dec < 0 ? k : dec;
            for (int i = 0; i < end; i++)
            {
                if (sep[i] != sep[0] || groups[i + 1].Length != 3) throw Ambiguous(text, last);
            }
            if (end > 0 && !IsLeadGroup(groups[0].ToString())) throw Ambiguous(text, last);

            var intDigits = new StringBuilder();
            for (int i = 0; i <= end; i++) intDigits.Append(groups[i]);
            string integer = Digits.TrimLeadingZeros(intDigits.ToString());
            string fraction = dec < 0 ? "" : groups[k].ToString();
            int fe = fraction.Length;
            while (fe > 0 && fraction[fe - 1] == '0') fe--;
            fraction = fraction.Substring(0, fe);
            if (integer.Length > ArabicWords.MaxIntegerDigits) throw new TafqitException(TafqitErrorCodes.TooLarge);
            return new NormalizedNumber(integer, fraction);
        }

        /// <summary>1–3 digits once the leading zeros are dropped.</summary>
        private static bool IsLeadGroup(string g)
        {
            int z = 0;
            while (z < g.Length && g[z] == '0') z++;
            int n = g.Length - z;
            return n >= 1 && n <= 3;
        }

        /// <summary>The AMBIGUOUS_SEPARATOR error with its readings («هل تقصد 12.50 أم 1250؟»).</summary>
        private static TafqitException Ambiguous(string body, char lastSeparator)
        {
            bool commaFirst = lastSeparator == ',';
            var readings = new List<string>(2);
            foreach (var r in new[] { Reading(body, commaFirst), Reading(body, !commaFirst) })
            {
                if (r != null && !readings.Contains(r)) readings.Add(r);
            }
            string? detail = readings.Count == 0 ? null : ErrorMessages.AskStart + string.Join(ErrorMessages.AskOr, readings) + ErrorMessages.AskEnd;
            return new TafqitException(TafqitErrorCodes.AmbiguousSeparator, detail, readings.ToArray());
        }

        /// <summary>
        /// One reading: "," is the decimal point (<paramref name="decimalComma"/>) or a thousands separator,
        /// "." the other one; spaces and ٬ are dropped, ٫ is the decimal point. No leading zeros, the fraction
        /// as written. Null when it is not a number.
        /// </summary>
        private static string? Reading(string body, bool decimalComma)
        {
            var t = new StringBuilder(body.Length);
            foreach (char ch in body)
            {
                if (ch >= '0' && ch <= '9') t.Append(ch);
                else if (ch == ArDecimal) t.Append('.');
                else if (ch == ',') { if (decimalComma) t.Append('.'); }
                else if (ch == '.') { if (!decimalComma) t.Append('.'); }
            }
            string s = t.ToString();
            int dot = -1, digitCount = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] != '.') digitCount++;
                else if (dot < 0) dot = i;
                else return null;
            }
            if (digitCount == 0) return null;
            string integer = Digits.TrimLeadingZeros(dot < 0 ? s : s.Substring(0, dot));
            string fraction = dot < 0 ? "" : s.Substring(dot + 1);
            return fraction.Length > 0 ? integer + "." + fraction : integer;
        }
    }
}
