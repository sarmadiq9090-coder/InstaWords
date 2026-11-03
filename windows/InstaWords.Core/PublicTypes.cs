using System;
using System.Collections.Generic;

namespace InstaWords.Core
{
    /// <summary>
    /// Options of <see cref="Tafqit.Convert"/> (src/types.ts TafqitOptions). A null property
    /// means "not given" and takes the default, exactly like an omitted option in the core.
    /// </summary>
    public sealed class TafqitOptions
    {
        /// <summary>Unit id, ISO code, label or alias. Null or "" = "none" (no unit).</summary>
        public string? Unit { get; set; } = "none";

        /// <summary>Output language: "ar", "en" or "fr" (exact, lower case). Null or "" = "ar".</summary>
        public string Language { get; set; } = "ar";

        /// <summary>Arabic hundreds spelling: «مائة» or «مئة». Null or "" = «مائة». Validated for every language, used by Arabic only.</summary>
        public string HundredsForm { get; set; } = "مائة";

        /// <summary>Text placed before the words (trimmed; nothing when blank), e.g. «فقط».</summary>
        public string Prefix { get; set; } = "";

        /// <summary>Text placed after the words (trimmed; nothing when blank), e.g. «لا غير».</summary>
        public string Suffix { get; set; } = "";

        /// <summary>
        /// Extra fraction digits: "round" (half-up to the subunit) or "error". Null = "round".
        /// Unlike the other options, "" is not the default: it is INVALID_OPTION, as in the core.
        /// </summary>
        public string Decimals { get; set; } = "round";
    }

    /// <summary>Outcome of <see cref="Tafqit.TryConvert"/> (src/types.ts TafqitResult).</summary>
    public sealed class TafqitResult
    {
        internal TafqitResult(bool ok, string text, string? cleaned, string? errorCode, string? errorMessage)
        {
            Ok = ok;
            Text = text;
            Cleaned = cleaned;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        /// <summary>True when <see cref="Text"/> holds the words.</summary>
        public bool Ok { get; }

        /// <summary>The words (with prefix and suffix); "" when <see cref="Ok"/> is false.</summary>
        public string Text { get; }

        /// <summary>
        /// Canonical form of the input (integer[.fraction]), whenever the input itself is valid,
        /// even if the conversion failed for another reason (unknown unit, …). Null otherwise.
        /// </summary>
        public string? Cleaned { get; }

        /// <summary>One of <see cref="TafqitErrorCodes"/>; null when <see cref="Ok"/>.</summary>
        public string? ErrorCode { get; }

        /// <summary>Arabic message of <see cref="ErrorCode"/> (ERROR_MESSAGES[code], without detail); null when <see cref="Ok"/>.</summary>
        public string? ErrorMessage { get; }
    }

    /// <summary>Error raised by <see cref="Tafqit.Convert"/> and <see cref="Tafqit.Clean"/> (src/errors.ts TafqitError).</summary>
    public sealed class TafqitException : Exception
    {
        internal TafqitException(string code, string? detail = null)
            : base(Internal.ErrorMessages.Get(code))
        {
            Code = code;
            Detail = string.IsNullOrEmpty(detail) ? null : detail;
        }

        /// <summary>One of <see cref="TafqitErrorCodes"/>.</summary>
        public string Code { get; }

        /// <summary>
        /// Technical detail (e.g. "language=de", a unit name), or null. <see cref="Exception.Message"/> is the
        /// Arabic message without it; the core's message is <c>Message + " (" + Detail + ")"</c> when Detail is set.
        /// </summary>
        public string? Detail { get; }
    }

    /// <summary>A unit of the catalog (data/units.json).</summary>
    public sealed class UnitInfo
    {
        internal UnitInfo(string id, string category, string label, string? code, IReadOnlyList<string> aliases,
            bool hasSubunit, int subunitDigits, int displayDigits, string? englishName, string? frenchName)
        {
            Id = id;
            Category = category;
            Label = label;
            Code = code;
            Aliases = aliases;
            HasSubunit = hasSubunit;
            SubunitDigits = subunitDigits;
            DisplayDigits = displayDigits;
            EnglishName = englishName;
            FrenchName = frenchName;
        }

        /// <summary>Stable identifier, e.g. "SAR", "riyal", "none".</summary>
        public string Id { get; }

        /// <summary>Category id, e.g. "currency-arab" (see <see cref="Tafqit.CategoryLabels"/>).</summary>
        public string Category { get; }

        /// <summary>Arabic display name, e.g. «ريال سعودي».</summary>
        public string Label { get; }

        /// <summary>ISO 4217 (or similar) code, e.g. "SAR"; null when none.</summary>
        public string? Code { get; }

        /// <summary>Other accepted names.</summary>
        public IReadOnlyList<string> Aliases { get; }

        /// <summary>Whether fractions are written with a subunit (هللة, cent …).</summary>
        public bool HasSubunit { get; }

        /// <summary>
        /// Fraction digits the subunit takes: the number of zeros of its ratio in data/units.json
        /// (100 → 2 for SAR, 1000 → 3 for KWD), the precision amounts are rounded to; 0 without a subunit.
        /// </summary>
        public int SubunitDigits { get; }

        /// <summary>
        /// Decimals to show for a whole amount (no fraction, so the words name no subunit): the
        /// unit's <c>displayDigits</c> in data/units.json, where the subunit is unused in everyday
        /// writing (0 for IQD: 1,750,000, not 1,750,000.000), else <see cref="SubunitDigits"/>.
        /// An amount with a fraction shows all <see cref="SubunitDigits"/>. Display only: the words
        /// and the rounding never depend on it.
        /// </summary>
        public int DisplayDigits { get; }

        /// <summary>English singular name, e.g. "Saudi Riyal"; null for "none".</summary>
        public string? EnglishName { get; }

        /// <summary>French singular name, e.g. "riyal saoudien"; null for "none".</summary>
        public string? FrenchName { get; }

        /// <inheritdoc />
        public override string ToString() => Label;
    }

    /// <summary>An output language (src/lang/index.ts LANGUAGES).</summary>
    public sealed class LanguageInfo
    {
        internal LanguageInfo(string id, string label, bool rightToLeft, bool supportsHundredsForm)
        {
            Id = id;
            Label = label;
            RightToLeft = rightToLeft;
            SupportsHundredsForm = supportsHundredsForm;
        }

        /// <summary>"ar", "en" or "fr".</summary>
        public string Id { get; }

        /// <summary>Native name: العربية, English, Français.</summary>
        public string Label { get; }

        /// <summary>True for Arabic.</summary>
        public bool RightToLeft { get; }

        /// <summary>Whether <see cref="TafqitOptions.HundredsForm"/> changes the output.</summary>
        public bool SupportsHundredsForm { get; }

        /// <inheritdoc />
        public override string ToString() => Label;
    }
}
