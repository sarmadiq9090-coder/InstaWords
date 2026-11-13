using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using InstaWords.App.Services;
using InstaWords.Core;
using Xunit;

namespace InstaWords.App.Tests
{
    /// <summary>
    /// «طباعة»: words that do not fit their printed lines are reported (Fits=false), so the print
    /// dialog can refuse to print them cut off (B3). Also the optional third cheque line and the strip.
    /// </summary>
    public class PrintFitTests
    {
        private const double Eps = 1e-9;

        // The app measures with WPF and IBM Plex Sans Arabic SemiBold, which these tests cannot load.
        // Shaped with HarfBuzz, that font gives 0.415–0.43 em per character for Arabic amounts in words
        // and 0.46–0.48 em for French and English. "Does not fit" cases use a lower bound (narrower
        // than the real text: if even that cannot fit, the real text cannot), "fits" cases an upper bound.
        private const double ArabicLow = 0.40;
        private const double ArabicHigh = 0.45;
        private const double LatinLow = 0.44;
        private const double LatinHigh = 0.50;

        private static Func<string, double, double> PerChar(double em) => (text, size) => text.Length * em * size;

        private const string Huge = "999999999999999.99"; // 999 trillion … and 99 subunits: the largest amount

        private static string Arabic(string amount) =>
            Tafqit.Convert(amount, new TafqitOptions { Unit = "SAR", Prefix = "فقط", Suffix = "لا غير" });

        private static string French(string amount) =>
            Tafqit.Convert(amount, new TafqitOptions { Unit = "EUR", Language = "fr", Prefix = "la somme de", Suffix = "seulement" });

        /// <summary>The cheque's word lines can hold at most this many DIPs of text, whatever the wrapping.</summary>
        private static double Capacity(ChequeProfile p) => ChequeLayout.WordLines(p, true).Sum(l => Mm.ToDip(l.Width));

        [Fact]
        public void TheSmallestSizeIsSixtyPercentButNeverBelowSevenPoints()
        {
            Assert.Equal(Mm.PtToDip(7.2), PrintFit.MinSize(Mm.PtToDip(12)), 9);
            Assert.Equal(Mm.PtToDip(7), PrintFit.MinSize(Mm.PtToDip(10)), 9);
            Assert.Equal(Mm.PtToDip(14.4), PrintFit.MinSize(Mm.PtToDip(24)), 9);
        }

        [Fact]
        public void HugeAmountWithPrefixAndSuffixDoesNotFitTheDefaultCheque()
        {
            var p = new ChequeProfile(); // 175 × 80 mm, 12 pt, two lines
            var words = French(Huge);
            Assert.StartsWith("la somme de neuf cent quatre-vingt-dix-neuf billions", words);
            Assert.EndsWith("centimes seulement", words);
            // Even narrower than the real font, at the smallest size, the words are longer than both lines together.
            Assert.True(words.Length * LatinLow * PrintFit.MinSize(Mm.PtToDip(12)) > Capacity(p));

            var fit = PrintFit.Cheque(p, rightToLeft: false, words, PerChar(LatinLow));
            Assert.False(fit.Fits);
            Assert.Equal(PrintFit.MinSize(Mm.PtToDip(12)), fit.FontSize, 9);
            Assert.Equal(2, fit.Lines.Count);
            // Nothing is dropped: the overflow is on the last line (which the dialog refuses to print).
            Assert.Equal(words, string.Join(" ", fit.Lines));
            Assert.True(PerChar(LatinLow)(fit.Lines[1], fit.FontSize) > Mm.ToDip(ChequeLayout.WordLines(p, false)[1].Width));
        }

        [Fact]
        public void HugeArabicAmountWithPrefixAndSuffixFitsTheDefaultChequeAtASmallerSize()
        {
            // «فقط تسعمائة وتسعة وتسعون تريليوناً … وتسع وتسعون هللة لا غير» (183 characters): about 195 mm
            // at 7.2 pt in the real font, against 250 mm of lines, so it fits, smaller than 12 pt.
            var p = new ChequeProfile();
            var words = Arabic(Huge);
            Assert.StartsWith("فقط تسعمائة وتسعة وتسعون تريليوناً", words);
            Assert.EndsWith("هللة لا غير", words);
            var fit = PrintFit.Cheque(p, rightToLeft: true, words, PerChar(ArabicHigh));
            Assert.True(fit.Fits);
            Assert.True(fit.FontSize < Mm.PtToDip(12) - Eps, "shrunk below 12 pt");
            Assert.Equal(words, string.Join(" ", fit.Lines.Where(l => l.Length > 0)));
        }

        [Fact]
        public void HugeArabicAmountDoesNotFitANarrowCheque()
        {
            var p = new ChequeProfile { PaperWidthMm = 120, PaperHeightMm = 60 };
            var words = Arabic(Huge);
            Assert.True(words.Length * ArabicLow * PrintFit.MinSize(Mm.PtToDip(12)) > Capacity(p));
            Assert.False(PrintFit.Cheque(p, true, words, PerChar(ArabicLow)).Fits);
        }

        [Fact]
        public void ALargeFontSizeCanMakeArabicWordsOverflow()
        {
            // At 24 pt the words may only shrink to 14.4 pt.
            var p = new ChequeProfile { FontSizePt = 24 };
            Assert.False(PrintFit.Cheque(p, true, Arabic(Huge), PerChar(ArabicLow)).Fits);
            p.FontSizePt = 12;
            Assert.True(PrintFit.Cheque(p, true, Arabic(Huge), PerChar(ArabicHigh)).Fits);
        }

        [Theory]
        [InlineData("1250.75")]
        [InlineData("300")]
        [InlineData("45750.5")]
        public void ShortAmountsFitAtTheChosenSize(string amount)
        {
            var p = new ChequeProfile();
            var fit = PrintFit.Cheque(p, true, Arabic(amount), PerChar(ArabicHigh));
            Assert.True(fit.Fits);
            Assert.Equal(Mm.PtToDip(12), fit.FontSize, 9);
            var french = PrintFit.Cheque(p, false, French(amount), PerChar(LatinHigh));
            Assert.True(french.Fits);
        }

        [Fact]
        public void EmptyWordsFit() => Assert.True(PrintFit.Cheque(new ChequeProfile(), true, "", PerChar(ArabicHigh)).Fits);

        // ---------- The optional third cheque line ----------

        [Fact]
        public void AThirdLineMakesTheHugeAmountFit()
        {
            var p = new ChequeProfile { ThirdWordsLine = true };
            var fit = PrintFit.Cheque(p, false, French(Huge), PerChar(LatinHigh));
            Assert.True(fit.Fits);
            Assert.Equal(3, fit.Lines.Count);
        }

        [Fact]
        public void TheThirdLineSitsInsideTheChequeBetweenTheFiguresAndTheSignature()
        {
            foreach (var rtl in new[] { false, true })
            {
                var two = ChequeLayout.DefaultWordLines(175, 80, rtl);
                var three = ChequeLayout.DefaultWordLines(175, 80, rtl, thirdLine: true);
                Assert.Equal(2, two.Length);
                Assert.Equal(3, three.Length);
                Assert.Equal(two[0], three[0]);
                Assert.Equal(two[1], three[1]);
                var third = three[2];
                Assert.True(third.X >= 0 && third.Right <= 175 + Eps && third.Bottom <= 80 + Eps, third.ToString());
                Assert.True(third.Y >= three[1].Bottom - Eps, "under the second line");
                Assert.True(third.Y >= ChequeLayout.DefaultBox(PrintField.Figures, 175, 80, rtl).Bottom - Eps, "under the figures box");
                Assert.True(third.Bottom <= ChequeLayout.DefaultBox(PrintField.Signature, 175, 80, rtl).Y + Eps, "above the signature");
            }
            var p = new ChequeProfile { ThirdWordsLine = true };
            p.SetOffset(PrintField.Words, 1, 2);
            var moved = ChequeLayout.WordLines(p, true);
            Assert.Equal(ChequeLayout.DefaultWordLines(175, 80, true, true)[2].Offset(1, 2), moved[2]);
        }

        [Fact]
        public void TheThirdLineIsSavedAndCopied()
        {
            var p = new ChequeProfile { Name = "A", ThirdWordsLine = true };
            Assert.True(p.Clone("B").ThirdWordsLine);
            Assert.False(new ChequeProfile().ThirdWordsLine);

            var s = new PrintSettings();
            s.Normalize("x");
            s.CurrentProfile.ThirdWordsLine = true;
            var json = Serialize(s);
            Assert.Contains("\"thirdWordsLine\":true", json);
            var back = Deserialize(json);
            back.Normalize("x");
            Assert.True(back.CurrentProfile.ThirdWordsLine);
            // Settings saved before this option: off.
            var old = Deserialize("{\"profiles\":[{\"name\":\"Old\"}]}");
            old.Normalize("x");
            Assert.False(old.CurrentProfile.ThirdWordsLine);
        }

        // ---------- Suggestions ----------

        [Fact]
        public void ChequeSuggestions()
        {
            var p = new ChequeProfile();
            // At 12 pt the words already shrink to 7.2 pt: a smaller font would not help, so it is not offered.
            Assert.Equal(new[] { "Print.Fix.ThirdLine", "Print.Fix.Affixes", "Print.Fix.ChequePaper" },
                PrintFit.Suggestions(PrintTemplate.Cheque, p, null, hasPrefixOrSuffix: true));
            Assert.Equal(new[] { "Print.Fix.ThirdLine", "Print.Fix.ChequePaper" },
                PrintFit.Suggestions(PrintTemplate.Cheque, p, null, hasPrefixOrSuffix: false));
            p.FontSizePt = 18;
            p.ThirdWordsLine = true;
            Assert.Equal(new[] { "Print.Fix.Font", "Print.Fix.ChequePaper" },
                PrintFit.Suggestions(PrintTemplate.Cheque, p, null, false));
        }

        [Fact]
        public void StripAndVoucherSuggestions()
        {
            Assert.Equal(new[] { "Print.Fix.Affixes", "Print.Fix.WiderPaper", "Print.Fix.UseVoucher" },
                PrintFit.Suggestions(PrintTemplate.Strip, null, Papers.Cheque, true));
            Assert.Equal(new[] { "Print.Fix.UseVoucher" }, PrintFit.Suggestions(PrintTemplate.Strip, null, Papers.A4, false));
            Assert.Equal(new[] { "Print.Fix.ShorterText", "Print.Fix.BiggerPaper" }, PrintFit.Suggestions(PrintTemplate.Receipt, null, Papers.A5Landscape, false));
            Assert.Equal(new[] { "Print.Fix.ShorterText", "Print.Fix.Affixes" }, PrintFit.Suggestions(PrintTemplate.Payment, null, Papers.A4, true));
        }

        [Fact]
        public void EverySuggestionHasAString()
        {
            var ar = TestStrings.Table("ar");
            var keys = new[] { PrintTemplate.Cheque, PrintTemplate.Strip, PrintTemplate.Receipt }
                .SelectMany(t => PrintFit.Suggestions(t, new ChequeProfile { FontSizePt = 24 }, Papers.Cheque, true)).Distinct();
            foreach (var k in keys) Assert.True(ar.ContainsKey(k), k);
        }

        // ---------- Strip ----------

        [Fact]
        public void TheStripKeepsItsTwoLineLayout()
        {
            // The same boxes as before the strip could grow (A4, left to right).
            var lines = StripLayout.WordLines(210, 2, false);
            Assert.Equal(new RectMm(19, 18, 172, 9), lines[0]);
            Assert.Equal(new RectMm(19, 27, 127, 9), lines[1]);
            Assert.Equal(new RectMm(149, 28, 42, 7), StripLayout.Figures(210, 2, false));
            Assert.Equal(new RectMm(15, 15, 180, 26), StripLayout.Frame(210, 2));
            // Right to left: mirrored.
            Assert.Equal(lines[1].MirrorX(210), StripLayout.WordLines(210, 2, true)[1]);
            Assert.Equal(StripLayout.Figures(210, 2, false).MirrorX(210), StripLayout.Figures(210, 2, true));
        }

        [Fact]
        public void TheStripGrowsAThirdLineOnlyWhenNeeded()
        {
            var shortFit = PrintFit.Strip(210, true, Arabic("1250.75"), PerChar(ArabicHigh), out var shortLines);
            Assert.True(shortFit.Fits);
            Assert.Equal(2, shortLines);

            // On A4 the huge French amount needs three lines (mechanism check with one width model).
            var words = French(Huge);
            var measure = PerChar(LatinHigh);
            Assert.True(words.Length * LatinHigh * PrintFit.MinSize(Mm.PtToDip(StripLayout.FontPt)) > StripLayout.WordLines(210, 2, false).Sum(l => Mm.ToDip(l.Width)));
            Assert.False(PrintFit.Words(words, StripLayout.WordLines(210, 2, false), Mm.PtToDip(StripLayout.FontPt), measure).Fits);
            var fit = PrintFit.Strip(210, false, words, measure, out var count);
            Assert.True(fit.Fits);
            Assert.Equal(3, count);
            var three = StripLayout.WordLines(210, 3, false);
            Assert.Equal(three[0].Width, three[1].Width, 9);           // full lines…
            Assert.True(three[2].Width < three[1].Width);              // …and the last one stops at the figures
            Assert.Equal(three[2].Y + 1, StripLayout.Figures(210, 3, false).Y, 9);
            Assert.True(StripLayout.Frame(210, 3).Bottom >= three[2].Bottom);
        }

        [Fact]
        public void TheStripReportsWordsThatDoNotFitEvenOnThreeLines()
        {
            var words = string.Join(" ", Enumerable.Repeat(French(Huge), 3));
            var fit = PrintFit.Strip(175, false, words, PerChar(LatinLow), out var count);
            Assert.False(fit.Fits);
            Assert.Equal(StripLayout.MaxLines, count);
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
