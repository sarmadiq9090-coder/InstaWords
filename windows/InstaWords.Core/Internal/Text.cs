using System.Collections.Generic;
using System.Text;

namespace InstaWords.Core.Internal
{
    /// <summary>JavaScript string semantics that .NET does not share (trim, /\s/, toLowerCase).</summary>
    internal static class JsText
    {
        /// <summary>String.prototype.trim().</summary>
        internal static string Trim(string s)
        {
            int a = 0, b = s.Length - 1;
            while (a <= b && CharClasses.IsJsSpace(s[a])) a++;
            while (b >= a && CharClasses.IsJsSpace(s[b])) b--;
            return s.Substring(a, b - a + 1);
        }

        /// <summary>src/catalog.ts unitKey: trimmed, /\s+/ collapsed to one space, lower case.</summary>
        internal static string UnitKey(string name)
        {
            var sb = new StringBuilder(name.Length);
            bool pending = false;
            foreach (char c in name)
            {
                if (CharClasses.IsJsSpace(c))
                {
                    if (sb.Length > 0) pending = true;
                    continue;
                }
                if (pending)
                {
                    sb.Append(' ');
                    pending = false;
                }
                if (c >= 'A' && c <= 'Z') sb.Append((char)(c + 32));
                else if (c < 0x80) sb.Append(c);
                else sb.Append(CharClasses.LowerNonAscii(c) ?? c.ToString());
            }
            return sb.ToString();
        }

        /// <summary>parts.filter(Boolean).join(" ")</summary>
        internal static string JoinNonEmpty(params string[] parts)
        {
            var kept = new List<string>(parts.Length);
            foreach (var p in parts) if (p.Length > 0) kept.Add(p);
            return string.Join(" ", kept);
        }
    }

    /// <summary>Digit-string helpers. Amounts stay strings; only 3-digit groups become ints.</summary>
    internal static class Digits
    {
        /// <summary>src/lang/types.ts splitGroups: 3-digit groups, least significant first.</summary>
        internal static int[] SplitGroups(string digits)
        {
            var groups = new int[(digits.Length + 2) / 3];
            int i = 0;
            for (int end = digits.Length; end > 0; end -= 3)
            {
                int v = 0;
                for (int j = end > 3 ? end - 3 : 0; j < end; j++) v = v * 10 + (digits[j] - '0');
                groups[i++] = v;
            }
            return groups;
        }

        /// <summary>Value of the last two digits.</summary>
        internal static int LastTwo(string digits)
        {
            int v = digits[digits.Length - 1] - '0';
            if (digits.Length >= 2) v += 10 * (digits[digits.Length - 2] - '0');
            return v;
        }

        /// <summary>Strips leading zeros ("" → "0").</summary>
        internal static string TrimLeadingZeros(string s)
        {
            int i = 0;
            while (i < s.Length && s[i] == '0') i++;
            return i == s.Length ? "0" : s.Substring(i);
        }

        /// <summary>Number of leading '0' characters.</summary>
        internal static int LeadingZeros(string s)
        {
            int i = 0;
            while (i < s.Length && s[i] == '0') i++;
            return i;
        }

        /// <summary>Adds one to a digit string.</summary>
        internal static string Increment(string digits)
        {
            var a = digits.ToCharArray();
            int i = a.Length - 1;
            while (i >= 0 && a[i] == '9') a[i--] = '0';
            if (i < 0) return "1" + new string(a);
            a[i] = (char)(a[i] + 1);
            return new string(a);
        }
    }
}
