using System;
using System.Text;
using InstaWords.Core;

namespace InstaWords.App.Services
{
    /// <summary>
    /// The figures shown next to the words («الرقم المعتمد», the print amount box, the «عدة مبالغ»
    /// grid, history), written the way the amount is written in practice, and stating the amount
    /// the words state (10.125 SAR rounded → 10.13):
    /// - money with a fraction keeps every subunit digit: 45,750.50 SAR, 5.250 KWD, 1,750,000.500 IQD;
    /// - a whole amount takes the unit's <see cref="UnitInfo.DisplayDigits"/> (data/units.json
    ///   displayDigits, else the subunit's digits): 45,750.00 SAR, 5.000 KWD, but 1,750,000 IQD,
    ///   since Iraqi accountants never write fils;
    /// - units without a subunit, non-money units and plain numbers: as the engine cleaned them.
    /// Amounts stay strings throughout (never double/decimal), like the engine.
    /// </summary>
    public static class AmountFormat
    {
        /// <summary>The catalog categories that are money (data/units.json).</summary>
        private static readonly string[] MoneyCategories = { "currency-arab", "currency-world", "crypto", "generic-currency" };

        public static bool IsMoney(UnitInfo unit) => unit != null && Array.IndexOf(MoneyCategories, unit.Category) >= 0;

        /// <summary>Fraction digits of an amount with a fraction: the subunit's (2 for SAR, 3 for KWD and IQD) when it is money, else 0 (as cleaned).</summary>
        public static int DecimalsFor(UnitInfo unit) => IsMoney(unit) && unit.HasSubunit ? unit.SubunitDigits : 0;

        /// <summary>Same, by unit id, code or name (null or "" = none).</summary>
        public static int DecimalsFor(string unit) => DecimalsFor(Find(unit));

        /// <summary>Fraction digits of a whole amount: <see cref="UnitInfo.DisplayDigits"/> (2 for SAR, 3 for KWD, 0 for IQD) when it is money with a subunit, else 0.</summary>
        public static int WholeDecimalsFor(UnitInfo unit) => IsMoney(unit) && unit.HasSubunit ? Math.Min(unit.DisplayDigits, unit.SubunitDigits) : 0;

        /// <summary>Same, by unit id, code or name (null or "" = none).</summary>
        public static int WholeDecimalsFor(string unit) => WholeDecimalsFor(Find(unit));

        /// <summary><see cref="Used(string, int, int)"/> with this unit's digits: "1750000" IQD → "1750000", "1750000.5" IQD → "1750000.500".</summary>
        public static string Used(string cleaned, UnitInfo unit) => Used(cleaned, DecimalsFor(unit), WholeDecimalsFor(unit));

        /// <summary>Same, by unit id, code or name (null or "" = none).</summary>
        public static string Used(string cleaned, string unit) => Used(cleaned, Find(unit));

        /// <summary>For display: <see cref="Used(string, string)"/> with thousands separators ("1,750,000", "45,750.50").</summary>
        public static string Display(string cleaned, string unit) => Grouped(Used(cleaned, unit));

        private static UnitInfo Find(string unit) => Tafqit.FindUnit(string.IsNullOrEmpty(unit) ? "none" : unit);

        /// <summary>Same as <see cref="Used(string, int, int)"/> with a whole amount taking every digit too ("45750" → "45750.00").</summary>
        public static string Used(string cleaned, int decimals) => Used(cleaned, decimals, decimals);

        /// <summary>
        /// The amount the words state, from the engine's cleaned number (integer[.fraction]), without
        /// separators: "45750.5" → "45750.50", and extra digits rounded half-up like the engine
        /// ("10.125" → "10.13"). When the fraction is zero, after rounding too, it takes
        /// <paramref name="wholeDecimals"/> digits instead (at most <paramref name="decimals"/>):
        /// "45750" → "45750.00" with 2, "1750000" → "1750000" and "0.9996" → "1" with 0.
        /// Meant for successful conversions: in «error» mode a number with extra digits does not
        /// convert. decimals 0, or anything but a plain number: as is.
        /// </summary>
        public static string Used(string cleaned, int decimals, int wholeDecimals)
        {
            if (string.IsNullOrEmpty(cleaned) || decimals <= 0) return cleaned;
            var s = cleaned.Trim();
            var dot = s.IndexOf('.');
            var integer = dot < 0 ? s : s.Substring(0, dot);
            var fraction = dot < 0 ? "" : s.Substring(dot + 1);
            if (integer.Length == 0 || !AllDigits(integer) || !AllDigits(fraction)) return cleaned;
            if (fraction.Length > decimals)
            {
                // src/engine.ts roundHalfUp: on the digit string, half-up on the first dropped digit.
                var digits = integer + fraction.Substring(0, decimals);
                if (fraction[decimals] >= '5') digits = Increment(digits);
                integer = TrimLeadingZeros(digits.Substring(0, digits.Length - decimals));
                fraction = digits.Substring(digits.Length - decimals);
            }
            if (fraction.TrimEnd('0').Length == 0)
            {
                // A whole amount (the words name no subunit): written with the unit's usual digits.
                var whole = Math.Max(0, Math.Min(wholeDecimals, decimals));
                return whole == 0 ? integer : integer + "." + new string('0', whole);
            }
            return integer + "." + fraction.PadRight(decimals, '0');
        }

        /// <summary>For display: <see cref="Used(string, int)"/> with thousands separators ("45,750.50").</summary>
        public static string Display(string cleaned, int decimals) => Grouped(Used(cleaned, decimals));

        /// <summary>Thousands separators on the integer part: 1250.75 → 1,250.75 (anything else as is).</summary>
        public static string Grouped(string number)
        {
            if (string.IsNullOrEmpty(number)) return "";
            var s = number.Trim();
            var sign = "";
            if (s.StartsWith("-", StringComparison.Ordinal) || s.StartsWith("+", StringComparison.Ordinal)) { sign = s.Substring(0, 1); s = s.Substring(1); }
            var dot = s.IndexOf('.');
            var intPart = dot < 0 ? s : s.Substring(0, dot);
            var frac = dot < 0 ? "" : s.Substring(dot);
            if (intPart.Length == 0 || !AllDigits(intPart)) return number; // not a plain number: show as is
            var b = new StringBuilder();
            for (var i = 0; i < intPart.Length; i++)
            {
                if (i > 0 && (intPart.Length - i) % 3 == 0) b.Append(',');
                b.Append(intPart[i]);
            }
            return sign + b + frac;
        }

        private static bool AllDigits(string s)
        {
            foreach (var c in s) if (c < '0' || c > '9') return false;
            return true;
        }

        /// <summary>"0999" → "1000": +1 on a digit string (it may grow by one digit).</summary>
        private static string Increment(string digits)
        {
            var chars = digits.ToCharArray();
            for (var i = chars.Length - 1; i >= 0; i--)
            {
                if (chars[i] < '9') { chars[i]++; return new string(chars); }
                chars[i] = '0';
            }
            return "1" + new string(chars);
        }

        private static string TrimLeadingZeros(string digits)
        {
            var i = 0;
            while (i < digits.Length - 1 && digits[i] == '0') i++;
            return digits.Substring(i);
        }
    }
}
