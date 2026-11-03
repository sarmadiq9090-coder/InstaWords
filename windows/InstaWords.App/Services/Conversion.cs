using System;
using System.Globalization;
using InstaWords.Core;

namespace InstaWords.App.Services
{
    /// <summary>The outcome of one conversion, as the UI needs it.</summary>
    public sealed class ConversionResult
    {
        public bool Ok { get; set; }
        public string Text { get; set; } = "";
        /// <summary>The engine's canonical form of the input (1234.5), whenever the input itself is valid.</summary>
        public string Cleaned { get; set; }
        /// <summary>
        /// The amount the words state, without separators: money with its full subunit decimals and
        /// rounded like the words (45750.50, 10.13); otherwise <see cref="Cleaned"/>. See <see cref="AmountFormat"/>.
        /// </summary>
        public string Amount { get; set; }
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
                // The figures follow the words only when there are words (an «error»-mode extra digit is not rounded).
                var amount = r.Ok ? AmountFormat.Used(r.Cleaned, AmountFormat.DecimalsFor(options?.Unit)) : r.Cleaned;
                // The engine's messages are Arabic; the English interface maps the code instead.
                return new ConversionResult
                {
                    Ok = r.Ok, Text = r.Text ?? "", Cleaned = r.Cleaned, Amount = amount, ErrorCode = r.ErrorCode,
                    ErrorMessage = r.Ok ? null : Loc.Error(r.ErrorCode, r.ErrorMessage),
                };
            }
            catch (Exception ex)
            {
                return new ConversionResult { Ok = false, ErrorCode = "internal", ErrorMessage = Loc.F("Err.Internal", ex.Message) };
            }
        }

        /// <summary>«الرقم المعتمد» for display: thousands separators (1,250.75); pass <see cref="ConversionResult.Amount"/>.</summary>
        public static string Grouped(string number) => AmountFormat.Grouped(number);

        public static string FormatCount(int n) => n.ToString("N0", CultureInfo.InvariantCulture);
    }
}
