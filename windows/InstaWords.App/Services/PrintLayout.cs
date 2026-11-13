using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

// Pure logic (no WPF): compiled into the app and linked into InstaWords.App.Tests.
// «طباعة»: templates, paper sizes, cheque calibration profiles, and the layout math the
// renderer (Views/PrintRenderer.cs) draws from. All positions are millimetres from the
// physical top-left corner of the paper, whatever the language.
namespace InstaWords.App.Services
{
    public enum PrintTemplate
    {
        /// <summary>شيك: values only, at calibrated positions on a pre-printed cheque.</summary>
        Cheque,
        /// <summary>سند قبض: a complete receipt voucher on plain paper.</summary>
        Receipt,
        /// <summary>سند صرف: a complete payment voucher on plain paper.</summary>
        Payment,
        /// <summary>A plain «amount in words» strip.</summary>
        Strip,
    }

    /// <summary>What a template prints. Cheque fields can be moved one by one (calibration).</summary>
    public enum PrintField
    {
        /// <summary>Calibration only: moves every field at once (printer margins).</summary>
        All,
        Title,
        Company,
        Number,
        Date,
        /// <summary>Cheque: pay to. Receipt: received from. Payment: paid to.</summary>
        Party,
        Figures,
        Words,
        Purpose,
        Signature,
    }

    /// <summary>Millimetres, device-independent pixels (1/96 inch) and points.</summary>
    public static class Mm
    {
        public const double DipPerMm = 96.0 / 25.4;
        public static double ToDip(double mm) => mm * DipPerMm;
        public static double FromDip(double dip) => dip / DipPerMm;
        public static double PtToDip(double pt) => pt * 96.0 / 72.0;

        /// <summary>Reads a length typed by the user ("12.5", "12,5", "١٢٫٥", "-3"); false when it is not a number.</summary>
        public static bool TryParse(string text, out double mm)
        {
            mm = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var b = new StringBuilder();
            foreach (var ch in text.Trim())
            {
                if (ch >= '٠' && ch <= '٩') b.Append((char)('0' + (ch - '٠')));
                else if (ch >= '۰' && ch <= '۹') b.Append((char)('0' + (ch - '۰')));
                else if (ch == ',' || ch == '٫') b.Append('.');
                else if (ch == '−') b.Append('-');
                else b.Append(ch);
            }
            return double.TryParse(b.ToString(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out mm)
                   && !double.IsNaN(mm) && !double.IsInfinity(mm);
        }

        public static string Format(double mm) => Math.Round(mm, 1).ToString("0.#", CultureInfo.InvariantCulture);
    }

    /// <summary>A rectangle in millimetres.</summary>
    public struct RectMm : IEquatable<RectMm>
    {
        public RectMm(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }
        public double Right => X + Width;
        public double Bottom => Y + Height;

        public RectMm Offset(double dx, double dy) => new RectMm(X + dx, Y + dy, Width, Height);

        /// <summary>The same box seen in a mirror (right-to-left layouts) on paper of this width.</summary>
        public RectMm MirrorX(double paperWidth) => new RectMm(paperWidth - X - Width, Y, Width, Height);

        public bool Equals(RectMm o) => Close(X, o.X) && Close(Y, o.Y) && Close(Width, o.Width) && Close(Height, o.Height);
        public override bool Equals(object obj) => obj is RectMm r && Equals(r);
        public override int GetHashCode() => Math.Round(X, 3).GetHashCode() ^ Math.Round(Y, 3).GetHashCode();
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##} {2:0.##}×{3:0.##}", X, Y, Width, Height);
        private static bool Close(double a, double b) => Math.Abs(a - b) < 1e-6;
    }

    /// <summary>A paper size preset (or "custom").</summary>
    public sealed class PaperPreset
    {
        public PaperPreset(string id, string labelKey, double width, double height)
        {
            Id = id;
            LabelKey = labelKey;
            WidthMm = width;
            HeightMm = height;
        }

        public string Id { get; }
        /// <summary>Key of its name in Strings.*.xaml.</summary>
        public string LabelKey { get; }
        public double WidthMm { get; }
        public double HeightMm { get; }
        public bool IsCustom => Id == Papers.CustomId;
    }

    public static class Papers
    {
        public const string CustomId = "custom";
        public static readonly PaperPreset Cheque = new PaperPreset("cheque", "Paper.Cheque", 175, 80);
        public static readonly PaperPreset ChequeLarge = new PaperPreset("cheque-large", "Paper.ChequeLarge", 203, 92);
        public static readonly PaperPreset ChequeSmall = new PaperPreset("cheque-small", "Paper.ChequeSmall", 152.4, 69.85);
        public static readonly PaperPreset A4 = new PaperPreset("a4", "Paper.A4", 210, 297);
        public static readonly PaperPreset A5 = new PaperPreset("a5", "Paper.A5", 148, 210);
        public static readonly PaperPreset A5Landscape = new PaperPreset("a5-landscape", "Paper.A5Landscape", 210, 148);
        public static readonly PaperPreset Custom = new PaperPreset(CustomId, "Paper.Custom", 0, 0);

        public const double MinSide = 40;
        public const double MaxSide = 500;

        /// <summary>The presets offered for a template, the default first.</summary>
        public static IReadOnlyList<PaperPreset> For(PrintTemplate t)
        {
            switch (t)
            {
                case PrintTemplate.Cheque: return new[] { Cheque, ChequeLarge, ChequeSmall, Custom };
                case PrintTemplate.Strip: return new[] { A4, A5Landscape, Cheque };
                default: return new[] { A5Landscape, A5, A4 };
            }
        }

        public static PaperPreset Find(PrintTemplate t, string id) => For(t).FirstOrDefault(p => p.Id == id) ?? For(t)[0];

        /// <summary>The preset with these dimensions, or "custom".</summary>
        public static PaperPreset Match(double width, double height) =>
            new[] { Cheque, ChequeLarge, ChequeSmall }.FirstOrDefault(p => Math.Abs(p.WidthMm - width) < 0.05 && Math.Abs(p.HeightMm - height) < 0.05) ?? Custom;

        public static double ClampSide(double mm) => Math.Max(MinSide, Math.Min(MaxSide, mm));
    }

    /// <summary>A per-field X/Y offset (mm) of a cheque profile.</summary>
    [DataContract(Name = "offset", Namespace = "")]
    public sealed class FieldOffset
    {
        [DataMember(Name = "field", Order = 0)] public string Field { get; set; }
        [DataMember(Name = "x", Order = 1)] public double X { get; set; }
        [DataMember(Name = "y", Order = 2)] public double Y { get; set; }
    }

    /// <summary>
    /// A cheque book / printer pair: paper size, font size and per-field offsets in mm, found once
    /// with «طباعة تجريبية» (a calibration grid) and then reused.
    /// </summary>
    [DataContract(Name = "profile", Namespace = "")]
    public sealed class ChequeProfile
    {
        public ChequeProfile() => SetDefaults();

        [OnDeserializing]
        private void OnDeserializing(StreamingContext _) => SetDefaults();

        private void SetDefaults()
        {
            Id = Guid.NewGuid().ToString("N").Substring(0, 8);
            Name = "";
            PaperWidthMm = Papers.Cheque.WidthMm;
            PaperHeightMm = Papers.Cheque.HeightMm;
            FontSizePt = 12;
            ShowSignature = true;
            PrintFrame = false;
            Offsets = new List<FieldOffset>();
            ThirdWordsLine = false;
        }

        [DataMember(Name = "id", Order = 0)] public string Id { get; set; }
        [DataMember(Name = "name", Order = 1)] public string Name { get; set; }
        [DataMember(Name = "paperWidthMm", Order = 2)] public double PaperWidthMm { get; set; }
        [DataMember(Name = "paperHeightMm", Order = 3)] public double PaperHeightMm { get; set; }
        [DataMember(Name = "fontSizePt", Order = 4)] public double FontSizePt { get; set; }
        [DataMember(Name = "showSignature", Order = 5)] public bool ShowSignature { get; set; }
        /// <summary>Also print the cheque frame and field labels (for plain paper).</summary>
        [DataMember(Name = "printFrame", Order = 6)] public bool PrintFrame { get; set; }
        [DataMember(Name = "offsets", Order = 7)] public List<FieldOffset> Offsets { get; set; }
        /// <summary>A third line for the amount in words, under the second (long amounts; off by default).</summary>
        [DataMember(Name = "thirdWordsLine", Order = 8)] public bool ThirdWordsLine { get; set; }

        public static string Key(PrintField f) => f.ToString().ToLowerInvariant();

        public void GetOffset(PrintField field, out double x, out double y)
        {
            var o = Offsets?.FirstOrDefault(e => e != null && e.Field == Key(field));
            x = o?.X ?? 0;
            y = o?.Y ?? 0;
        }

        public void SetOffset(PrintField field, double x, double y)
        {
            if (Offsets == null) Offsets = new List<FieldOffset>();
            Offsets.RemoveAll(e => e == null || e.Field == Key(field));
            if (Math.Abs(x) > 1e-9 || Math.Abs(y) > 1e-9) Offsets.Add(new FieldOffset { Field = Key(field), X = Math.Round(x, 2), Y = Math.Round(y, 2) });
        }

        public void ResetOffsets() => Offsets = new List<FieldOffset>();

        public ChequeProfile Clone(string name) => new ChequeProfile
        {
            Name = name,
            PaperWidthMm = PaperWidthMm,
            PaperHeightMm = PaperHeightMm,
            FontSizePt = FontSizePt,
            ShowSignature = ShowSignature,
            PrintFrame = PrintFrame,
            ThirdWordsLine = ThirdWordsLine,
            Offsets = (Offsets ?? new List<FieldOffset>()).Where(o => o != null).Select(o => new FieldOffset { Field = o.Field, X = o.X, Y = o.Y }).ToList(),
        };

        /// <summary>Keeps loaded values usable (hand-edited or damaged settings).</summary>
        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(Id)) Id = Guid.NewGuid().ToString("N").Substring(0, 8);
            if (Name == null) Name = "";
            PaperWidthMm = Valid(PaperWidthMm) && PaperWidthMm > 0 ? Papers.ClampSide(PaperWidthMm) : Papers.Cheque.WidthMm;
            PaperHeightMm = Valid(PaperHeightMm) && PaperHeightMm > 0 ? Papers.ClampSide(PaperHeightMm) : Papers.Cheque.HeightMm;
            FontSizePt = Valid(FontSizePt) && FontSizePt > 0 ? Math.Max(MinFontPt, Math.Min(MaxFontPt, FontSizePt)) : 12;
            if (Offsets == null) Offsets = new List<FieldOffset>();
            Offsets.RemoveAll(o => o == null || string.IsNullOrEmpty(o.Field) || !Valid(o.X) || !Valid(o.Y) || Math.Abs(o.X) > 200 || Math.Abs(o.Y) > 200);
        }

        public const double MinFontPt = 7;
        public const double MaxFontPt = 24;

        private static bool Valid(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }

    /// <summary>Everything «طباعة» remembers (part of settings.json).</summary>
    [DataContract(Name = "print", Namespace = "")]
    public sealed class PrintSettings
    {
        public PrintSettings() => SetDefaults();

        [OnDeserializing]
        private void OnDeserializing(StreamingContext _) => SetDefaults();

        private void SetDefaults()
        {
            LastTemplate = "cheque";
            LastProfileId = null;
            Profiles = new List<ChequeProfile>();
            VoucherPaper = Papers.A5Landscape.Id;
            StripPaper = Papers.A4.Id;
            CompanyName = "";
            LogoPath = null;
            NextNumber = "1";
        }

        [DataMember(Name = "lastTemplate", Order = 0)] public string LastTemplate { get; set; }
        [DataMember(Name = "lastProfile", Order = 1)] public string LastProfileId { get; set; }
        [DataMember(Name = "profiles", Order = 2)] public List<ChequeProfile> Profiles { get; set; }
        [DataMember(Name = "voucherPaper", Order = 3)] public string VoucherPaper { get; set; }
        [DataMember(Name = "stripPaper", Order = 4)] public string StripPaper { get; set; }
        /// <summary>Voucher header (optional).</summary>
        [DataMember(Name = "companyName", Order = 5)] public string CompanyName { get; set; }
        /// <summary>Voucher logo: a copy kept in the data folder.</summary>
        [DataMember(Name = "logoPath", Order = 6)] public string LogoPath { get; set; }
        /// <summary>The next voucher number (incremented after printing when it is a number).</summary>
        [DataMember(Name = "nextNumber", Order = 7)] public string NextNumber { get; set; }

        public static string TemplateId(PrintTemplate t) => t.ToString().ToLowerInvariant();

        public static PrintTemplate ParseTemplate(string id)
        {
            foreach (PrintTemplate t in Enum.GetValues(typeof(PrintTemplate)))
                if (TemplateId(t) == id) return t;
            return PrintTemplate.Cheque;
        }

        public PrintTemplate Template
        {
            get => ParseTemplate(LastTemplate);
            set => LastTemplate = TemplateId(value);
        }

        /// <summary>Makes the stored values usable: at least one profile, a valid current one.</summary>
        public void Normalize(string defaultProfileName)
        {
            if (Profiles == null) Profiles = new List<ChequeProfile>();
            Profiles.RemoveAll(p => p == null);
            foreach (var p in Profiles) p.Normalize();
            // Ids must be unique (copied settings files).
            var seen = new HashSet<string>();
            foreach (var p in Profiles)
                while (!seen.Add(p.Id)) p.Id = Guid.NewGuid().ToString("N").Substring(0, 8);
            if (Profiles.Count == 0) Profiles.Add(new ChequeProfile { Name = defaultProfileName });
            if (Profiles.All(p => p.Id != LastProfileId)) LastProfileId = Profiles[0].Id;
            LastTemplate = TemplateId(Template);
            VoucherPaper = Papers.Find(PrintTemplate.Receipt, VoucherPaper).Id;
            StripPaper = Papers.Find(PrintTemplate.Strip, StripPaper).Id;
            if (CompanyName == null) CompanyName = "";
            if (NextNumber == null) NextNumber = "1";
        }

        public ChequeProfile CurrentProfile => Profiles?.FirstOrDefault(p => p.Id == LastProfileId) ?? Profiles?.FirstOrDefault();

        /// <summary>"41" → "42", "R-0099" → "R-0100"; anything else stays as it is.</summary>
        public static string Increment(string number)
        {
            if (string.IsNullOrEmpty(number)) return number;
            var end = number.Length;
            var start = end;
            while (start > 0 && number[start - 1] >= '0' && number[start - 1] <= '9') start--;
            if (start == end) return number;
            var digits = number.Substring(start);
            if (!decimal.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var n)) return number;
            var next = (n + 1).ToString(CultureInfo.InvariantCulture).PadLeft(digits.Length, '0');
            return number.Substring(0, start) + next;
        }
    }

    /// <summary>One printed item: what, its label (a Strings key, or null), and its text.</summary>
    public sealed class PrintItem
    {
        public PrintItem(PrintField field, string labelKey, string value)
        {
            Field = field;
            LabelKey = labelKey;
            Value = value ?? "";
        }

        public PrintField Field { get; }
        public string LabelKey { get; }
        public string Value { get; }
    }

    /// <summary>The fields typed in the print dialog, for one page.</summary>
    public sealed class PrintForm
    {
        public PrintTemplate Template { get; set; }
        /// <summary>
        /// The amount in figures, without separators: the conversion's amount used (money with its
        /// subunit decimals, e.g. 1250.75, 45750.50; <see cref="ConversionResult.Amount"/>).
        /// </summary>
        public string Cleaned { get; set; } = "";
        /// <summary>The amount in words, with the user's prefix/suffix.</summary>
        public string Words { get; set; } = "";
        /// <summary>Currency code or unit name shown next to the figures (vouchers).</summary>
        public string UnitText { get; set; } = "";
        public string Date { get; set; } = "";
        public string Party { get; set; } = "";
        public string Purpose { get; set; } = "";
        public string Number { get; set; } = "";
        public string Company { get; set; } = "";
        public bool ShowSignature { get; set; } = true;
        /// <summary>
        /// The words say «صفر» for an amount that is not zero (0.004 SAR, ROUNDED_TO_ZERO;
        /// <see cref="ConversionResult.RoundedToZero"/>): the print dialog does not print it (<see cref="PrintZero"/>).
        /// </summary>
        public bool RoundedToZero { get; set; }
    }

    /// <summary>
    /// Amounts rounded to zero (ROUNDED_TO_ZERO: 0.004 SAR is written «صفر ريال سعودي») are never printed
    /// silently on a cheque or voucher: like words that would be cut off, the print dialog shows a red
    /// notice above the preview (with the page numbers in a bulk job) and keeps Print disabled.
    /// </summary>
    public static class PrintZero
    {
        /// <summary>At most this many page numbers are listed in the notice; the rest is counted.</summary>
        public const int MaxListed = 10;

        /// <summary>
        /// 1-based numbers, among the pages that print (those with an amount, as <c>FitReport</c> counts
        /// them), of the pages whose amount was rounded to zero; empty when there is none.
        /// </summary>
        public static IReadOnlyList<int> Pages(IEnumerable<PrintForm> pages)
        {
            var list = new List<int>();
            var n = 0;
            foreach (var form in pages ?? Enumerable.Empty<PrintForm>())
            {
                if (form == null || string.IsNullOrEmpty(form.Cleaned)) continue;
                n++;
                if (form.RoundedToZero) list.Add(n);
            }
            return list;
        }

        /// <summary>"3, 7, 12" («3، 7، 12» in Arabic; at most <see cref="MaxListed"/> numbers, then "…").</summary>
        public static string PageList(IReadOnlyList<int> pages)
        {
            if (pages == null || pages.Count == 0) return "";
            var shown = pages.Take(MaxListed).Select(p => p.ToString("N0", CultureInfo.InvariantCulture));
            return string.Join(Loc.IsEnglish ? ", " : "، ", shown) + (pages.Count > MaxListed ? "…" : "");
        }

        /// <summary>
        /// The notice: the engine's warning (Warn.ROUNDED_TO_ZERO) and that it will not print; for a job of
        /// several pages, how many and which. Null when no page is affected.
        /// </summary>
        public static string Notice(IReadOnlyList<int> pages, int printedPages)
        {
            if (pages == null || pages.Count == 0) return null;
            var text = Loc.T("Warn.ROUNDED_TO_ZERO") + " " + Loc.T("Print.Zero.Notice");
            if (printedPages > 1)
                text += " " + Loc.F("Print.Zero.Pages", pages.Count.ToString("N0", CultureInfo.InvariantCulture),
                    printedPages.ToString("N0", CultureInfo.InvariantCulture), PageList(pages));
            return text;
        }
    }

    /// <summary>Which fields each template has and how the dialog's fields map onto them.</summary>
    public static class PrintTemplates
    {
        public static readonly PrintTemplate[] All = { PrintTemplate.Cheque, PrintTemplate.Receipt, PrintTemplate.Payment, PrintTemplate.Strip };

        public static bool IsVoucher(PrintTemplate t) => t == PrintTemplate.Receipt || t == PrintTemplate.Payment;

        /// <summary>The dialog inputs a template uses (the amount is always there).</summary>
        public static IReadOnlyList<PrintField> Inputs(PrintTemplate t)
        {
            switch (t)
            {
                case PrintTemplate.Cheque: return new[] { PrintField.Date, PrintField.Party, PrintField.Signature };
                case PrintTemplate.Receipt:
                case PrintTemplate.Payment: return new[] { PrintField.Company, PrintField.Number, PrintField.Date, PrintField.Party, PrintField.Purpose };
                default: return new PrintField[0];
            }
        }

        public static bool Uses(PrintTemplate t, PrintField f) => Inputs(t).Contains(f);

        /// <summary>The cheque fields that can be moved (calibration), «All» first.</summary>
        public static readonly PrintField[] Calibrated = { PrintField.All, PrintField.Date, PrintField.Party, PrintField.Figures, PrintField.Words, PrintField.Signature };

        public static string NameKey(PrintTemplate t) => "Print.Template." + t;
        public static string TitleKey(PrintTemplate t) => t == PrintTemplate.Payment ? "Print.PaymentTitle" : t == PrintTemplate.Receipt ? "Print.ReceiptTitle" : null;

        /// <summary>The label of the «party» field: pay to / received from / paid to.</summary>
        public static string PartyKey(PrintTemplate t) =>
            t == PrintTemplate.Receipt ? "Print.ReceivedFrom" : t == PrintTemplate.Payment ? "Print.PaidTo" : "Print.PayTo";

        /// <summary>Signature boxes at the foot of a voucher, in reading order.</summary>
        public static IReadOnlyList<string> SignatureKeys(PrintTemplate t)
        {
            switch (t)
            {
                case PrintTemplate.Receipt: return new[] { "Print.Sign.Accountant", "Print.Sign.Cashier", "Print.Sign.Payer" };
                case PrintTemplate.Payment: return new[] { "Print.Sign.Accountant", "Print.Sign.Approved", "Print.Sign.Recipient" };
                case PrintTemplate.Cheque: return new[] { "Print.Sign.Cheque" };
                default: return new string[0];
            }
        }

        /// <summary>The figures as printed: "#1,250.75#" (the guards stop digits being added).</summary>
        public static string Figures(string cleaned, bool guard = true)
        {
            var grouped = Grouped(cleaned);
            if (grouped.Length == 0) return "";
            return guard ? "#" + grouped + "#" : grouped;
        }

        /// <summary>1250.75 → 1,250.75 (same as the window's «الرقم المعتمد»).</summary>
        public static string Grouped(string cleaned)
        {
            if (string.IsNullOrEmpty(cleaned)) return "";
            var s = cleaned.Trim();
            var dot = s.IndexOf('.');
            var intPart = dot < 0 ? s : s.Substring(0, dot);
            var frac = dot < 0 ? "" : s.Substring(dot);
            if (intPart.Length == 0 || intPart.Any(c => c < '0' || c > '9')) return s;
            var b = new StringBuilder();
            for (var i = 0; i < intPart.Length; i++)
            {
                if (i > 0 && (intPart.Length - i) % 3 == 0) b.Append(',');
                b.Append(intPart[i]);
            }
            return b + frac;
        }

        /// <summary>What a page of this template prints, in order, from the dialog's fields.</summary>
        public static IReadOnlyList<PrintItem> Map(PrintForm f)
        {
            var t = f.Template;
            var items = new List<PrintItem>();
            switch (t)
            {
                case PrintTemplate.Cheque:
                    items.Add(new PrintItem(PrintField.Date, "Print.Date", f.Date));
                    items.Add(new PrintItem(PrintField.Party, PartyKey(t), f.Party));
                    items.Add(new PrintItem(PrintField.Words, "Print.TheSumOf", f.Words));
                    items.Add(new PrintItem(PrintField.Figures, null, Figures(f.Cleaned)));
                    if (f.ShowSignature) items.Add(new PrintItem(PrintField.Signature, "Print.Sign.Cheque", ""));
                    break;
                case PrintTemplate.Receipt:
                case PrintTemplate.Payment:
                    if (!string.IsNullOrWhiteSpace(f.Company)) items.Add(new PrintItem(PrintField.Company, null, f.Company.Trim()));
                    items.Add(new PrintItem(PrintField.Title, null, TitleKey(t)));
                    items.Add(new PrintItem(PrintField.Number, "Print.Number", f.Number));
                    items.Add(new PrintItem(PrintField.Date, "Print.Date", f.Date));
                    items.Add(new PrintItem(PrintField.Figures, "Print.Amount", Grouped(f.Cleaned) + (string.IsNullOrEmpty(f.UnitText) ? "" : " " + f.UnitText)));
                    items.Add(new PrintItem(PrintField.Party, PartyKey(t), f.Party));
                    items.Add(new PrintItem(PrintField.Words, "Print.TheSumOf", f.Words));
                    items.Add(new PrintItem(PrintField.Purpose, t == PrintTemplate.Receipt ? "Print.PurposeReceipt" : "Print.PurposePayment", f.Purpose));
                    foreach (var key in SignatureKeys(t)) items.Add(new PrintItem(PrintField.Signature, key, ""));
                    break;
                default:
                    items.Add(new PrintItem(PrintField.Words, null, f.Words));
                    items.Add(new PrintItem(PrintField.Figures, null, Figures(f.Cleaned)));
                    break;
            }
            return items;
        }
    }

    /// <summary>
    /// Where each cheque field goes. The defaults follow a common 175×80 mm cheque and scale with
    /// the paper; Arabic cheques are the mirror image (date and figures box on the left). The
    /// profile's offsets are then added in physical millimetres (+x right, +y down).
    /// </summary>
    public static class ChequeLayout
    {
        private const double RefWidth = 175;
        private const double RefHeight = 80;

        /// <summary>A left-to-right cheque of the reference size.</summary>
        private static RectMm Reference(PrintField f)
        {
            switch (f)
            {
                case PrintField.Date: return new RectMm(128, 8, 38, 7);
                case PrintField.Party: return new RectMm(24, 20, 140, 7);
                case PrintField.Figures: return new RectMm(129, 40, 38, 9);
                case PrintField.Signature: return new RectMm(112, 60, 54, 10);
                default: return new RectMm(0, 0, 0, 0);
            }
        }

        /// <summary>The lines of the amount in words (the second one stops before the figures box).</summary>
        // Line 1 leaves room at its start for a pre-printed «the sum of»; line 2 runs from the edge.
        // The optional third line sits under the figures box and above the signature.
        private static RectMm[] ReferenceWords(bool thirdLine) => thirdLine
            ? new[] { new RectMm(30, 32, 136, 7), new RectMm(10, 41, 114, 7), new RectMm(10, 50, 156, 7) }
            : new[] { new RectMm(30, 32, 136, 7), new RectMm(10, 41, 114, 7) };

        private static RectMm Scale(RectMm r, double w, double h) =>
            new RectMm(r.X * w / RefWidth, r.Y * h / RefHeight, r.Width * w / RefWidth, r.Height * h / RefHeight);

        /// <summary>A field's box before offsets.</summary>
        public static RectMm DefaultBox(PrintField f, double paperWidth, double paperHeight, bool rightToLeft)
        {
            var r = Scale(Reference(f), paperWidth, paperHeight);
            return rightToLeft ? r.MirrorX(paperWidth) : r;
        }

        public static RectMm[] DefaultWordLines(double paperWidth, double paperHeight, bool rightToLeft, bool thirdLine = false) =>
            ReferenceWords(thirdLine).Select(r => Scale(r, paperWidth, paperHeight)).Select(r => rightToLeft ? r.MirrorX(paperWidth) : r).ToArray();

        /// <summary>A field's box on the paper: default + «All» offset + its own offset.</summary>
        public static RectMm Box(ChequeProfile p, PrintField f, bool rightToLeft)
        {
            Offsets(p, f, out var dx, out var dy);
            return DefaultBox(f, p.PaperWidthMm, p.PaperHeightMm, rightToLeft).Offset(dx, dy);
        }

        public static RectMm[] WordLines(ChequeProfile p, bool rightToLeft)
        {
            Offsets(p, PrintField.Words, out var dx, out var dy);
            return DefaultWordLines(p.PaperWidthMm, p.PaperHeightMm, rightToLeft, p.ThirdWordsLine).Select(r => r.Offset(dx, dy)).ToArray();
        }

        private static void Offsets(ChequeProfile p, PrintField f, out double dx, out double dy)
        {
            p.GetOffset(PrintField.All, out var ax, out var ay);
            p.GetOffset(f, out var fx, out var fy);
            dx = ax + fx;
            dy = ay + fy;
        }
    }

    /// <summary>The amount in words broken into lines, and the dashed filler after it.</summary>
    public sealed class WrappedWords
    {
        public WrappedWords(IReadOnlyList<string> lines, double fontSize, bool fits)
        {
            Lines = lines;
            FontSize = fontSize;
            Fits = fits;
        }

        /// <summary>One entry per available line ("" for an unused line).</summary>
        public IReadOnlyList<string> Lines { get; }
        public double FontSize { get; }
        /// <summary>False when even the smallest size needs more lines than there are (the last line overflows).</summary>
        public bool Fits { get; }
    }

    /// <summary>A dashed filler segment on one line, in the same unit as the line.</summary>
    public struct Filler
    {
        public Filler(double start, double width)
        {
            Start = start;
            Width = width;
        }

        public double Start { get; }
        public double Width { get; }
        public double End => Start + Width;
    }

    public static class WordWrap
    {
        /// <summary>
        /// Greedy word wrap over lines of the given widths. Words are kept whole; a single word wider
        /// than a line stays on it (and makes the result not fit). Extra words go onto the last line.
        /// </summary>
        public static List<string> Wrap(string text, IList<double> lineWidths, Func<string, double> measure, out bool fits)
        {
            var words = (text ?? "").Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var lines = new List<string>();
            fits = true;
            var i = 0;
            for (var line = 0; line < lineWidths.Count; line++)
            {
                var last = line == lineWidths.Count - 1;
                var current = "";
                while (i < words.Length)
                {
                    var candidate = current.Length == 0 ? words[i] : current + " " + words[i];
                    if (current.Length > 0 && measure(candidate) > lineWidths[line] + 1e-9)
                    {
                        if (!last) break;
                        fits = false; // no line left: keep going on the last one
                    }
                    else if (current.Length == 0 && measure(candidate) > lineWidths[line] + 1e-9) fits = false;
                    current = candidate;
                    i++;
                }
                lines.Add(current);
            }
            if (lineWidths.Count == 0 && words.Length > 0) fits = false;
            return lines;
        }

        /// <summary>
        /// The largest font size (from <paramref name="maxSize"/> down to <paramref name="minSize"/>) at which
        /// the text fits the lines. <paramref name="measure"/>(text, size) is the text's width at that size.
        /// </summary>
        public static WrappedWords Fit(string text, IList<double> lineWidths, Func<string, double, double> measure,
            double maxSize, double minSize, double step = 0.5)
        {
            if (step <= 0) step = 0.5;
            List<string> lines = null;
            var fits = false;
            var size = maxSize;
            for (; size >= minSize - 1e-9; size -= step)
            {
                var s = size;
                lines = Wrap(text, lineWidths, t => measure(t, s), out fits);
                if (fits) return new WrappedWords(lines, s, true);
            }
            size = minSize;
            lines = Wrap(text, lineWidths, t => measure(t, minSize), out fits);
            return new WrappedWords(lines, size, fits);
        }

        /// <summary>
        /// The dashed filler after the text on a line from <paramref name="lineStart"/> (left edge) of
        /// <paramref name="lineWidth"/>: the text sits at the line's start side (right for right-to-left),
        /// the filler covers the rest after a small gap. Null when less than <paramref name="minWidth"/> is left.
        /// </summary>
        public static Filler? FillerFor(double lineStart, double lineWidth, double textWidth, bool rightToLeft, double gap, double minWidth)
        {
            var used = textWidth <= 0 ? 0 : textWidth + gap;
            var width = lineWidth - used;
            if (width < minWidth || width <= 0) return null;
            return rightToLeft ? new Filler(lineStart, width) : new Filler(lineStart + used, width);
        }
    }

    /// <summary>
    /// The «amount in words» strip: a frame at the top of the sheet with two lines of words (three
    /// when two are not enough), the figures box at the end of the last line.
    /// </summary>
    public static class StripLayout
    {
        public const double Margin = 15;
        public const double LineHeight = 9;
        public const double FiguresWidth = 42;
        public const double FontPt = 13;
        public const double FiguresFontPt = 12;
        public const int MinLines = 2;
        public const int MaxLines = 3;

        private static double Inner(double paperWidth) => paperWidth - 2 * Margin;

        public static RectMm Frame(double paperWidth, int lines) =>
            new RectMm(Margin, Margin, Inner(paperWidth), LineHeight * lines + 8);

        public static RectMm[] WordLines(double paperWidth, int lines, bool rightToLeft)
        {
            var width = Inner(paperWidth);
            var result = new RectMm[lines];
            for (var i = 0; i < lines; i++)
            {
                var last = i == lines - 1;
                var r = new RectMm(Margin + 4, Margin + 3 + LineHeight * i, last ? width - 8 - FiguresWidth - 3 : width - 8, LineHeight);
                result[i] = rightToLeft ? r.MirrorX(paperWidth) : r;
            }
            return result;
        }

        public static RectMm Figures(double paperWidth, int lines, bool rightToLeft)
        {
            var r = new RectMm(Margin + Inner(paperWidth) - 4 - FiguresWidth, Margin + 3 + LineHeight * (lines - 1) + 1, FiguresWidth, LineHeight - 2);
            return rightToLeft ? r.MirrorX(paperWidth) : r;
        }
    }

    /// <summary>
    /// Does the amount in words fit its printed lines? The words shrink from the chosen size down to
    /// 60 % of it (never below <see cref="ChequeProfile.MinFontPt"/>); when even that is not enough,
    /// <see cref="WrappedWords.Fits"/> is false: the end would be cut off on paper, so the print dialog
    /// shows a red notice and does not print. <c>measure(text, size)</c> is the text's width in DIPs.
    /// </summary>
    public static class PrintFit
    {
        public const double ShrinkFactor = 0.6;
        public const double Step = 0.5;

        /// <summary>The smallest size (DIPs) the words may shrink to from <paramref name="sizeDip"/>.</summary>
        public static double MinSize(double sizeDip) => Math.Max(Mm.PtToDip(ChequeProfile.MinFontPt), sizeDip * ShrinkFactor);

        public static WrappedWords Words(string words, IEnumerable<RectMm> lines, double sizeDip, Func<string, double, double> measure) =>
            WordWrap.Fit(words, lines.Select(l => Mm.ToDip(l.Width)).ToList(), measure, sizeDip, MinSize(sizeDip), Step);

        /// <summary>The cheque's word lines (two, or three with <see cref="ChequeProfile.ThirdWordsLine"/>).</summary>
        public static WrappedWords Cheque(ChequeProfile p, bool rightToLeft, string words, Func<string, double, double> measure) =>
            Words(words, ChequeLayout.WordLines(p, rightToLeft), Mm.PtToDip(p.FontSizePt), measure);

        /// <summary>The strip: two lines, or three when two are not enough. <paramref name="lineCount"/> is the number used.</summary>
        public static WrappedWords Strip(double paperWidth, bool rightToLeft, string words, Func<string, double, double> measure, out int lineCount)
        {
            WrappedWords fit = null;
            for (lineCount = StripLayout.MinLines; lineCount <= StripLayout.MaxLines; lineCount++)
            {
                fit = Words(words, StripLayout.WordLines(paperWidth, lineCount, rightToLeft), Mm.PtToDip(StripLayout.FontPt), measure);
                if (fit.Fits) return fit;
            }
            lineCount = StripLayout.MaxLines;
            return fit;
        }

        /// <summary>
        /// What the user can change when the words do not fit, as Strings keys (most useful first).
        /// <paramref name="paper"/> is the strip's or voucher's paper (cheques use their profile).
        /// </summary>
        public static IReadOnlyList<string> Suggestions(PrintTemplate t, ChequeProfile p, PaperPreset paper, bool hasPrefixOrSuffix)
        {
            var keys = new List<string>();
            switch (t)
            {
                case PrintTemplate.Cheque:
                    if (p != null && !p.ThirdWordsLine) keys.Add("Print.Fix.ThirdLine");
                    // A smaller size helps only while 60 % of it is clearly above the 7 pt floor (not at the default 12 pt).
                    if (p != null && MinSize(Mm.PtToDip(p.FontSizePt)) > Mm.PtToDip(ChequeProfile.MinFontPt) * 1.1) keys.Add("Print.Fix.Font");
                    if (hasPrefixOrSuffix) keys.Add("Print.Fix.Affixes");
                    keys.Add("Print.Fix.ChequePaper");
                    break;
                case PrintTemplate.Strip:
                    if (hasPrefixOrSuffix) keys.Add("Print.Fix.Affixes");
                    if (paper != null && paper.WidthMm < Papers.A4.WidthMm - 0.05) keys.Add("Print.Fix.WiderPaper");
                    keys.Add("Print.Fix.UseVoucher");
                    break;
                default:
                    keys.Add("Print.Fix.ShorterText");
                    if (hasPrefixOrSuffix) keys.Add("Print.Fix.Affixes");
                    if (paper == null || paper.Id != Papers.A4.Id) keys.Add("Print.Fix.BiggerPaper");
                    break;
            }
            return keys;
        }
    }
}
