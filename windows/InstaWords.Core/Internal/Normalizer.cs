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

    /// <summary>Input cleaning, src/normalize.ts (docs/SPEC.md §1). Pure string processing.</summary>
    internal static class Normalizer
    {
        internal static NormalizedNumber Normalize(string? input)
        {
            input ??= "";
            // String(input).replace(SPACES, "")
            var sb = new StringBuilder(input.Length);
            foreach (char c in input) if (!CharClasses.IsInputSpace(c)) sb.Append(c);
            if (sb.Length == 0) throw new TafqitException(TafqitErrorCodes.Empty);

            // toAsciiDigits(s).replace(THOUSANDS, "").replace(DECIMAL, ".")
            var t = new StringBuilder(sb.Length);
            for (int i = 0; i < sb.Length; i++)
            {
                char c = sb[i];
                int d = CharClasses.DigitValue(c);
                if (d >= 0) t.Append((char)('0' + d));
                else if (CharClasses.IsThousandsSeparator(c)) { }
                else if (CharClasses.IsDecimalPoint(c)) t.Append('.');
                else t.Append(c);
            }
            string s = t.ToString();
            if (s.Length > 0 && CharClasses.IsMinusSign(s[0])) throw new TafqitException(TafqitErrorCodes.Negative);
            if (s.Length > 0 && s[0] == '+') s = s.Substring(1);

            // /^(\d+(\.\d*)?|\.\d+)$/ with ASCII digits: at most one '.', at least one digit.
            int dot = -1, digitCount = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c >= '0' && c <= '9') digitCount++;
                else if (c == '.' && dot < 0) dot = i;
                else throw new TafqitException(TafqitErrorCodes.InvalidFormat);
            }
            if (digitCount == 0) throw new TafqitException(TafqitErrorCodes.InvalidFormat);

            string integer = dot < 0 ? s : s.Substring(0, dot);
            string fraction = dot < 0 ? "" : s.Substring(dot + 1);
            integer = Digits.TrimLeadingZeros(integer);
            int end = fraction.Length;
            while (end > 0 && fraction[end - 1] == '0') end--;
            fraction = fraction.Substring(0, end);
            if (integer.Length > ArabicWords.MaxIntegerDigits) throw new TafqitException(TafqitErrorCodes.TooLarge);
            return new NormalizedNumber(integer, fraction);
        }
    }
}
