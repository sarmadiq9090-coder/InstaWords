using System;
using System.Globalization;
using System.Text;
using InstaWords.Core;

namespace InstaWords.App.Services
{
    /// <summary>The outcome of one conversion, as the UI needs it.</summary>
    public sealed class ConversionResult
    {
        public bool Ok { get; set; }
        public string Text { get; set; } = "";
        public string Cleaned { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
    }

    /// <summary>Thin wrapper over the engine that never throws into the UI.</summary>
    public static class Conversion
    {
        public static ConversionResult Run(string input, TafqitOptions options)
        {
            try
            {
                var r = Tafqit.TryConvert(input ?? "", options);
                // The engine's messages are Arabic; the English interface maps the code instead.
                return new ConversionResult
                {
                    Ok = r.Ok, Text = r.Text ?? "", Cleaned = r.Cleaned, ErrorCode = r.ErrorCode,
                    ErrorMessage = r.Ok ? null : Loc.Error(r.ErrorCode, r.ErrorMessage),
                };
            }
            catch (Exception ex)
            {
                return new ConversionResult { Ok = false, ErrorCode = "internal", ErrorMessage = Loc.F("Err.Internal", ex.Message) };
            }
        }

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
