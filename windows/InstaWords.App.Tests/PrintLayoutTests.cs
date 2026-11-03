using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using InstaWords.App.Services;
using Xunit;

namespace InstaWords.App.Tests
{
    /// <summary>«طباعة»: units, cheque calibration, word wrap with the dashed filler, template fields.</summary>
    public class PrintLayoutTests
    {
        private const double Eps = 1e-9;

        // ---------- mm ↔ DIP ----------

        [Fact]
        public void MillimetresAndDips()
        {
            Assert.Equal(96, Mm.ToDip(25.4), 9);           // one inch
            Assert.Equal(25.4, Mm.FromDip(96), 9);
            Assert.Equal(793.700787, Mm.ToDip(210), 5);    // A4 width
            Assert.Equal(96, Mm.PtToDip(72), 9);
            Assert.Equal(16, Mm.PtToDip(12), 9);
            foreach (var mm in new[] { 0, 0.5, 80, 175, 297 }) Assert.Equal(mm, Mm.FromDip(Mm.ToDip(mm)), 9);
        }

        [Theory]
        [InlineData("12.5", 12.5)]
        [InlineData("12,5", 12.5)]
        [InlineData("١٢٫٥", 12.5)]
        [InlineData(" -3 ", -3)]
        [InlineData("−2", -2)]
        [InlineData("0", 0)]
        public void ParsesTypedLengths(string text, double expected)
        {
            Assert.True(Mm.TryParse(text, out var mm));
            Assert.Equal(expected, mm, 9);
        }

        [Theory]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("1.2.3")]
        [InlineData(null)]
        public void RejectsNonNumbers(string text) => Assert.False(Mm.TryParse(text, out _));

        [Fact]
        public void FormatsLengths()
        {
            Assert.Equal("12.5", Mm.Format(12.5));
            Assert.Equal("3", Mm.Format(3.0));
            Assert.Equal("-0.4", Mm.Format(-0.44));
        }

        // ---------- Cheque layout and offsets ----------

        [Fact]
        public void DefaultBoxesFitTheCheque()
        {
            foreach (var rtl in new[] { false, true })
            {
                foreach (var f in new[] { PrintField.Date, PrintField.Party, PrintField.Figures, PrintField.Signature })
                    AssertInside(ChequeLayout.DefaultBox(f, 175, 80, rtl), 175, 80);
                foreach (var l in ChequeLayout.DefaultWordLines(175, 80, rtl)) AssertInside(l, 175, 80);
            }
        }

        private static void AssertInside(RectMm r, double w, double h)
        {
            Assert.True(r.Width > 0 && r.Height > 0, r.ToString());
            Assert.True(r.X >= 0 && r.Y >= 0 && r.Right <= w + Eps && r.Bottom <= h + Eps, r + " outside " + w + "×" + h);
        }

        [Fact]
        public void ArabicChequesAreMirrored()
        {
            var ltr = ChequeLayout.DefaultBox(PrintField.Date, 175, 80, rightToLeft: false);
            var rtl = ChequeLayout.DefaultBox(PrintField.Date, 175, 80, rightToLeft: true);
            Assert.Equal(ltr.Y, rtl.Y, 9);
            Assert.Equal(ltr.Width, rtl.Width, 9);
            Assert.Equal(175 - ltr.Right, rtl.X, 9);
            Assert.True(ltr.X > 87.5, "LTR date on the right");
            Assert.True(rtl.Right < 87.5, "RTL date on the left");
            // The figures box sits at the end of the second words line in both directions.
            var linesRtl = ChequeLayout.DefaultWordLines(175, 80, true);
            var figRtl = ChequeLayout.DefaultBox(PrintField.Figures, 175, 80, true);
            Assert.True(figRtl.Right <= linesRtl[1].X + Eps);
            var linesLtr = ChequeLayout.DefaultWordLines(175, 80, false);
            var figLtr = ChequeLayout.DefaultBox(PrintField.Figures, 175, 80, false);
            Assert.True(figLtr.X >= linesLtr[1].Right - Eps);
        }

        [Fact]
        public void BoxesScaleWithThePaper()
        {
            var a = ChequeLayout.DefaultBox(PrintField.Party, 175, 80, false);
            var b = ChequeLayout.DefaultBox(PrintField.Party, 350, 160, false);
            Assert.Equal(new RectMm(a.X * 2, a.Y * 2, a.Width * 2, a.Height * 2), b);
        }

        [Fact]
        public void OffsetsMoveFieldsInMillimetres()
        {
            var p = new ChequeProfile();
            var date0 = ChequeLayout.Box(p, PrintField.Date, false);
            var party0 = ChequeLayout.Box(p, PrintField.Party, false);
            var words0 = ChequeLayout.WordLines(p, false);

            p.SetOffset(PrintField.All, 2, -1.5);  // printer margin: everything
            p.SetOffset(PrintField.Date, -4, 3);   // and the date on its own
            p.SetOffset(PrintField.Words, 0.5, 0);

            Assert.Equal(date0.Offset(-2, 1.5), ChequeLayout.Box(p, PrintField.Date, false));
            Assert.Equal(party0.Offset(2, -1.5), ChequeLayout.Box(p, PrintField.Party, false));
            var words = ChequeLayout.WordLines(p, false);
            for (var i = 0; i < words.Length; i++) Assert.Equal(words0[i].Offset(2.5, -1.5), words[i]);

            // Offsets are physical: the same shift in a right-to-left cheque.
            var rtl0 = ChequeLayout.DefaultBox(PrintField.Date, p.PaperWidthMm, p.PaperHeightMm, true);
            Assert.Equal(rtl0.Offset(-2, 1.5), ChequeLayout.Box(p, PrintField.Date, true));
        }

        [Fact]
        public void ZeroOffsetsAreNotStored()
        {
            var p = new ChequeProfile();
            p.SetOffset(PrintField.Date, 1, 1);
            p.SetOffset(PrintField.Date, 0, 0);
            Assert.Empty(p.Offsets);
            p.SetOffset(PrintField.Figures, 1.234, -5);
            p.GetOffset(PrintField.Figures, out var x, out var y);
            Assert.Equal(1.23, x, 9);
            Assert.Equal(-5, y, 9);
            p.GetOffset(PrintField.Signature, out x, out y);
            Assert.Equal(0, x);
            Assert.Equal(0, y);
            p.ResetOffsets();
            Assert.Empty(p.Offsets);
        }

        [Fact]
        public void ClonesAreIndependent()
        {
            var p = new ChequeProfile { Name = "A", PaperWidthMm = 200, FontSizePt = 14 };
            p.SetOffset(PrintField.Party, 3, 4);
            var c = p.Clone("B");
            Assert.NotEqual(p.Id, c.Id);
            Assert.Equal("B", c.Name);
            Assert.Equal(200, c.PaperWidthMm);
            Assert.Equal(14, c.FontSizePt);
            c.SetOffset(PrintField.Party, 9, 9);
            p.GetOffset(PrintField.Party, out var x, out _);
            Assert.Equal(3, x);
        }

        [Fact]
        public void NormalizeRepairsProfiles()
        {
            var p = new ChequeProfile { Id = "", PaperWidthMm = double.NaN, PaperHeightMm = 5000, FontSizePt = 0, Offsets = null };
            p.Normalize();
            Assert.False(string.IsNullOrEmpty(p.Id));
            Assert.Equal(175, p.PaperWidthMm);
            Assert.Equal(Papers.MaxSide, p.PaperHeightMm);
            Assert.Equal(12, p.FontSizePt);
            Assert.NotNull(p.Offsets);
        }

        [Fact]
        public void PaperPresets()
        {
            Assert.Same(Papers.Cheque, Papers.For(PrintTemplate.Cheque)[0]);
            Assert.Equal(175, Papers.Cheque.WidthMm);
            Assert.Equal(80, Papers.Cheque.HeightMm);
            Assert.Contains(Papers.Custom, Papers.For(PrintTemplate.Cheque));
            Assert.Same(Papers.Cheque, Papers.Match(175, 80));
            Assert.Same(Papers.Custom, Papers.Match(176, 80));
            Assert.Same(Papers.A5Landscape, Papers.Find(PrintTemplate.Receipt, "nonsense"));
            Assert.Same(Papers.A4, Papers.Find(PrintTemplate.Payment, "a4"));
            Assert.Equal(Papers.MinSide, Papers.ClampSide(1));
        }

        // ---------- Word wrap and filler ----------

        /// <summary>One unit per character: easy to reason about.</summary>
        private static double Chars(string s) => s.Length;

        [Fact]
        public void WrapsWholeWordsOverTheLines()
        {
            var lines = WordWrap.Wrap("aaa bbb ccc ddd", new double[] { 7, 7 }, Chars, out var fits);
            Assert.True(fits);
            Assert.Equal(new[] { "aaa bbb", "ccc ddd" }, lines);
        }

        [Fact]
        public void ShortTextLeavesTheSecondLineEmpty()
        {
            var lines = WordWrap.Wrap("ألف ريال", new double[] { 40, 30 }, Chars, out var fits);
            Assert.True(fits);
            Assert.Equal(new[] { "ألف ريال", "" }, lines);
        }

        [Fact]
        public void TooMuchTextGoesOntoTheLastLine()
        {
            var lines = WordWrap.Wrap("aaa bbb ccc ddd eee", new double[] { 7, 7 }, Chars, out var fits);
            Assert.False(fits);
            Assert.Equal(new[] { "aaa bbb", "ccc ddd eee" }, lines);
            // Nothing is ever lost.
            Assert.Equal("aaa bbb ccc ddd eee", string.Join(" ", lines));
        }

        [Fact]
        public void AWordWiderThanTheLineDoesNotFit()
        {
            var lines = WordWrap.Wrap("abcdefghij k", new double[] { 5, 5 }, Chars, out var fits);
            Assert.False(fits);
            Assert.Equal(new[] { "abcdefghij", "k" }, lines);
        }

        [Fact]
        public void EmptyTextFits()
        {
            var lines = WordWrap.Wrap("  ", new double[] { 5, 5 }, Chars, out var fits);
            Assert.True(fits);
            Assert.Equal(new[] { "", "" }, lines);
        }

        [Fact]
        public void FitShrinksTheFontUntilTheTextFits()
        {
            // Width = characters × size × 0.5: two lines of 60 units hold 120 / size characters each.
            const string text = "one thousand two hundred fifty";
            Func<string, double, double> measure = (s, size) => s.Length * size * 0.5;
            var big = WordWrap.Fit(text, new double[] { 60, 60 }, measure, maxSize: 12, minSize: 6, step: 1);
            Assert.True(big.Fits);
            Assert.Equal(7, big.FontSize); // the largest size at which it fits: «one thousand two» / «hundred fifty»
            Assert.Equal(new[] { "one thousand two", "hundred fifty" }, big.Lines);
            Assert.All(big.Lines, l => Assert.True(measure(l, big.FontSize) <= 60, l));
            Assert.Equal(text, string.Join(" ", big.Lines.Where(l => l.Length > 0)));

            // A text that fits at full size keeps it.
            var small = WordWrap.Fit("one", new double[] { 30, 30 }, measure, 12, 6, 1);
            Assert.Equal(12, small.FontSize);
            Assert.True(small.Fits);

            // Too long even at the smallest size: smallest size, flagged.
            var huge = WordWrap.Fit(string.Join(" ", Enumerable.Repeat("word", 40)), new double[] { 30, 30 }, measure, 12, 6, 1);
            Assert.False(huge.Fits);
            Assert.Equal(6, huge.FontSize);
        }

        [Fact]
        public void FillerFollowsTheTextLeftToRight()
        {
            var f = WordWrap.FillerFor(lineStart: 10, lineWidth: 100, textWidth: 40, rightToLeft: false, gap: 2, minWidth: 3);
            Assert.NotNull(f);
            Assert.Equal(52, f.Value.Start, 9);
            Assert.Equal(58, f.Value.Width, 9);
            Assert.Equal(110, f.Value.End, 9); // up to the end of the line
        }

        [Fact]
        public void FillerFollowsTheTextRightToLeft()
        {
            // Arabic text starts at the right edge (110); the dashes fill from the left edge to the text.
            var f = WordWrap.FillerFor(10, 100, 40, rightToLeft: true, gap: 2, minWidth: 3);
            Assert.NotNull(f);
            Assert.Equal(10, f.Value.Start, 9);
            Assert.Equal(58, f.Value.Width, 9);
            Assert.Equal(110 - 40 - 2, f.Value.End, 9);
        }

        [Fact]
        public void AnEmptyLineIsAllFiller()
        {
            var f = WordWrap.FillerFor(10, 100, 0, true, 2, 3);
            Assert.Equal(10, f.Value.Start, 9);
            Assert.Equal(100, f.Value.Width, 9);
        }

        [Fact]
        public void NoFillerWhenTheLineIsFull()
        {
            Assert.Null(WordWrap.FillerFor(10, 100, 96, false, 2, 3));
            Assert.Null(WordWrap.FillerFor(10, 100, 120, true, 2, 3));
        }

        // ---------- Template fields ----------

        private static PrintForm Form(PrintTemplate t) => new PrintForm
        {
            Template = t, Cleaned = "1250.75", Words = "ألف ومائتان وخمسون", UnitText = "SAR", Date = "03/11/2026",
            Party = "شركة النخيل", Purpose = "دفعة إيجار", Number = "42", Company = "مؤسسة الأمل", ShowSignature = true,
        };

        [Fact]
        public void ChequeFields()
        {
            var items = PrintTemplates.Map(Form(PrintTemplate.Cheque));
            Assert.Equal(new[] { PrintField.Date, PrintField.Party, PrintField.Words, PrintField.Figures, PrintField.Signature }, items.Select(i => i.Field));
            Assert.Equal("Print.PayTo", items.Single(i => i.Field == PrintField.Party).LabelKey);
            Assert.Equal("#1,250.75#", items.Single(i => i.Field == PrintField.Figures).Value);
            Assert.Equal("ألف ومائتان وخمسون", items.Single(i => i.Field == PrintField.Words).Value);
            Assert.DoesNotContain(items, i => i.Field == PrintField.Number || i.Field == PrintField.Purpose || i.Field == PrintField.Company);

            var f = Form(PrintTemplate.Cheque);
            f.ShowSignature = false;
            Assert.DoesNotContain(PrintTemplates.Map(f), i => i.Field == PrintField.Signature);
        }

        [Fact]
        public void ReceiptFields()
        {
            var items = PrintTemplates.Map(Form(PrintTemplate.Receipt));
            Assert.Equal("مؤسسة الأمل", items.Single(i => i.Field == PrintField.Company).Value);
            Assert.Equal("Print.ReceiptTitle", items.Single(i => i.Field == PrintField.Title).Value);
            Assert.Equal("42", items.Single(i => i.Field == PrintField.Number).Value);
            Assert.Equal("03/11/2026", items.Single(i => i.Field == PrintField.Date).Value);
            Assert.Equal("1,250.75 SAR", items.Single(i => i.Field == PrintField.Figures).Value);
            Assert.Equal("Print.ReceivedFrom", items.Single(i => i.Field == PrintField.Party).LabelKey);
            Assert.Equal("شركة النخيل", items.Single(i => i.Field == PrintField.Party).Value);
            Assert.Equal("Print.PurposeReceipt", items.Single(i => i.Field == PrintField.Purpose).LabelKey);
            Assert.Equal(PrintTemplates.SignatureKeys(PrintTemplate.Receipt), items.Where(i => i.Field == PrintField.Signature).Select(i => i.LabelKey));
        }

        [Fact]
        public void PaymentFields()
        {
            var items = PrintTemplates.Map(Form(PrintTemplate.Payment));
            Assert.Equal("Print.PaymentTitle", items.Single(i => i.Field == PrintField.Title).Value);
            Assert.Equal("Print.PaidTo", items.Single(i => i.Field == PrintField.Party).LabelKey);
            Assert.Equal("Print.PurposePayment", items.Single(i => i.Field == PrintField.Purpose).LabelKey);
            Assert.Contains(items, i => i.LabelKey == "Print.Sign.Recipient");

            // No company name: no header line.
            var f = Form(PrintTemplate.Payment);
            f.Company = "  ";
            Assert.DoesNotContain(PrintTemplates.Map(f), i => i.Field == PrintField.Company);
        }

        [Fact]
        public void StripFields()
        {
            var items = PrintTemplates.Map(Form(PrintTemplate.Strip));
            Assert.Equal(new[] { PrintField.Words, PrintField.Figures }, items.Select(i => i.Field));
        }

        [Fact]
        public void DialogInputsPerTemplate()
        {
            Assert.True(PrintTemplates.Uses(PrintTemplate.Cheque, PrintField.Party));
            Assert.True(PrintTemplates.Uses(PrintTemplate.Cheque, PrintField.Signature));
            Assert.False(PrintTemplates.Uses(PrintTemplate.Cheque, PrintField.Number));
            Assert.True(PrintTemplates.Uses(PrintTemplate.Receipt, PrintField.Purpose));
            Assert.True(PrintTemplates.Uses(PrintTemplate.Payment, PrintField.Company));
            Assert.Empty(PrintTemplates.Inputs(PrintTemplate.Strip));
            Assert.True(PrintTemplates.IsVoucher(PrintTemplate.Payment));
            Assert.False(PrintTemplates.IsVoucher(PrintTemplate.Cheque));
            Assert.Equal(PrintField.All, PrintTemplates.Calibrated[0]);
        }

        [Theory]
        [InlineData("1250.75", "#1,250.75#")]
        [InlineData("1000000", "#1,000,000#")]
        [InlineData("0.5", "#0.5#")]
        [InlineData("999", "#999#")]
        [InlineData("", "")]
        public void FiguresAreGuarded(string cleaned, string expected) => Assert.Equal(expected, PrintTemplates.Figures(cleaned));

        // ---------- Settings ----------

        [Theory]
        [InlineData("41", "42")]
        [InlineData("9", "10")]
        [InlineData("R-0099", "R-0100")]
        [InlineData("2026/001", "2026/002")]
        [InlineData("A", "A")]
        [InlineData("", "")]
        public void VoucherNumbersIncrement(string number, string expected) => Assert.Equal(expected, PrintSettings.Increment(number));

        [Fact]
        public void SettingsNormalizeAndRoundTrip()
        {
            var s = new PrintSettings { LastTemplate = "nonsense", VoucherPaper = "x", Profiles = null };
            s.Normalize("شيكاتي");
            Assert.Equal(PrintTemplate.Cheque, s.Template);
            Assert.Single(s.Profiles);
            Assert.Equal("شيكاتي", s.CurrentProfile.Name);
            Assert.Equal(s.Profiles[0].Id, s.LastProfileId);
            Assert.Equal(Papers.A5Landscape.Id, s.VoucherPaper);

            s.Template = PrintTemplate.Payment;
            s.CurrentProfile.SetOffset(PrintField.Words, 1.5, -2);
            s.Profiles.Add(s.CurrentProfile.Clone("Bank B"));
            s.Profiles[1].Id = s.Profiles[0].Id; // duplicate ids (copied settings) are repaired
            var json = Serialize(s);
            var back = Deserialize(json);
            back.Normalize("x");
            Assert.Equal(PrintTemplate.Payment, back.Template);
            Assert.Equal(2, back.Profiles.Count);
            Assert.NotEqual(back.Profiles[0].Id, back.Profiles[1].Id);
            back.Profiles[0].GetOffset(PrintField.Words, out var x, out var y);
            Assert.Equal(1.5, x);
            Assert.Equal(-2, y);
            Assert.Contains("\"lastTemplate\":\"payment\"", json);
        }

        [Fact]
        public void MissingFieldsGetDefaults()
        {
            var back = Deserialize("{\"profiles\":[{\"name\":\"Old\"}]}");
            back.Normalize("x");
            Assert.Equal(175, back.CurrentProfile.PaperWidthMm);
            Assert.True(back.CurrentProfile.ShowSignature);
            Assert.Equal("1", back.NextNumber);
            Assert.Equal(PrintTemplate.Cheque, back.Template);
        }

        private static string Serialize(PrintSettings s)
        {
            using (var ms = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(PrintSettings)).WriteObject(ms, s);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        private static PrintSettings Deserialize(string json)
        {
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                return (PrintSettings)new DataContractJsonSerializer(typeof(PrintSettings)).ReadObject(ms);
        }
    }
}
