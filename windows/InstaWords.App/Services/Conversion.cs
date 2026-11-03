using System;
using System.Globalization;
using System.Text;
using InstaWords.Core;

namespace InstaWords.App.Services
{
    /// <summary>Thin wrapper over the engine that never throws into the UI.</summary>
    public static class Conversion
    {
        public static TafqitResult Run(string input, TafqitOptions options)
        {
            try
            {
                return Tafqit.TryConvert(input ?? "", options) ?? Fail("unknown", "تعذّر التحويل.");
            }
            catch (Exception ex)
            {
                return Fail("internal", "خطأ غير متوقع: " + ex.Message);
            }
        }

        private static TafqitResult Fail(string code, string message) =>
            new TafqitResult { Ok = false, ErrorCode = code, ErrorMessage = message };

        /// <summary>«الرقم المعتمد» for display: the cleaned number with thousands separators (1,250.75).</summary>
        public static string Grouped(string cleaned)
        {
            if (string.IsNullOrEmpty(cleaned)) return "";
            var s = cleaned.Trim();
            var sign = "";
            if (s.StartsWith("-", StringComparison.Ordinal) || s.StartsWith("+", StringComparison.Ordinal)) { sign = s.Substring(0, 1); s = s.Substring(1); }
            var dot = s.IndexOf('.');
            var intPart = dot < 0 ? s : s.Substring(0, dot);
            var frac = dot < 0 ? "" : s.Substring(dot);
            foreach (var ch in intPart) if (ch < '0' || ch > '9') return cleaned; // not a plain number: show as is
            var b = new StringBuilder();
            for (var i = 0; i < intPart.Length; i++)
            {
                if (i > 0 && (intPart.Length - i) % 3 == 0) b.Append(',');
                b.Append(intPart[i]);
            }
            return sign + b + frac;
        }

        public static string FormatCount(int n) => n.ToString("N0", CultureInfo.InvariantCulture);
    }
}
