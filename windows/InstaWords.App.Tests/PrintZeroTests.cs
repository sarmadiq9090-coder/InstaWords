using System.Collections.Generic;
using System.Linq;
using InstaWords.App.Services;
using InstaWords.Core;
using Xunit;

namespace InstaWords.App.Tests
{
    /// <summary>
    /// «طباعة»: an amount rounded to zero (0.004 SAR → «صفر ريال سعودي», ROUNDED_TO_ZERO) is never printed
    /// silently: the print dialog lists the affected pages in a red notice and keeps Print disabled.
    /// </summary>
    public class PrintZeroTests
    {
        private static Dictionary<string, string> Ar => TestStrings.Table("ar");
        private static Dictionary<string, string> En => TestStrings.Table("en");

        private static readonly TafqitOptions Sar = new TafqitOptions { Unit = "SAR" };

        /// <summary>A page as the print dialog builds it from one conversion.</summary>
        private static PrintForm Page(string input)
        {
            var r = Conversion.Run(input, Sar);
            Assert.True(r.Ok, input);
            return new PrintForm { Cleaned = r.Amount ?? "", Words = r.Text, RoundedToZero = r.RoundedToZero };
        }

        [Fact]
        public void TheConversionCarriesTheWarningCode()
        {
            var r = Conversion.Run("0.004", Sar);
            Assert.True(r.Ok);
            Assert.Equal("صفر ريال سعودي", r.Text);
            Assert.Equal(new[] { TafqitWarningCodes.RoundedToZero }, r.Warnings);
            Assert.True(r.RoundedToZero);

            // A real zero, a real amount and an error have no such warning.
            Assert.False(Conversion.Run("0", Sar).RoundedToZero);
            Assert.False(Conversion.Run("0.01", Sar).RoundedToZero);
            var bad = Conversion.Run("12abc", Sar);
            Assert.False(bad.Ok);
            Assert.Empty(bad.Warnings);
            Assert.False(bad.RoundedToZero);
        }

        [Fact]
        public void NoPageRoundedToZeroMeansNoNotice()
        {
            var pages = new[] { Page("300"), Page("0"), Page("0.01") };
            Assert.Empty(PrintZero.Pages(pages));
            Assert.Null(PrintZero.Notice(PrintZero.Pages(pages), 3));
            Assert.Empty(PrintZero.Pages(null));
            Assert.Equal("", PrintZero.PageList(new int[0]));
        }

        [Fact]
        public void OnePageGetsTheEngineWarningAndNoPageNumbers()
        {
            var pages = PrintZero.Pages(new[] { Page("0.004") });
            Assert.Equal(new[] { 1 }, pages);
            Assert.Equal(Ar["Warn.ROUNDED_TO_ZERO"] + " " + Ar["Print.Zero.Notice"], PrintZero.Notice(pages, 1));
            TestStrings.With("en", () =>
                Assert.Equal(En["Warn.ROUNDED_TO_ZERO"] + " " + En["Print.Zero.Notice"], PrintZero.Notice(pages, 1)));
        }

        [Fact]
        public void BulkListsThePageNumbersAmongThePagesThatPrint()
        {
            // Empty rows have no page; the numbers count the pages that print, as the overflow notice does.
            var forms = new List<PrintForm>
            {
                Page("300"), new PrintForm(), Page("0.004"), Page("12"), Page("0.001"), Page("0.0049"),
            };
            var pages = PrintZero.Pages(forms);
            Assert.Equal(new[] { 2, 4, 5 }, pages);
            Assert.Equal("2، 4، 5", PrintZero.PageList(pages));
            Assert.Equal(Ar["Warn.ROUNDED_TO_ZERO"] + " " + Ar["Print.Zero.Notice"] + " " + string.Format(Ar["Print.Zero.Pages"], "3", "5", "2، 4، 5"),
                PrintZero.Notice(pages, 5));
            TestStrings.With("en", () =>
            {
                Assert.Equal("2, 4, 5", PrintZero.PageList(pages));
                var notice = PrintZero.Notice(pages, 5);
                Assert.EndsWith("3 of 5 pages were rounded to zero: pages 2, 4, 5.", notice);
                Assert.StartsWith(En["Warn.ROUNDED_TO_ZERO"], notice);
            });
        }

        [Fact]
        public void LongListsAreCut()
        {
            var forms = Enumerable.Range(0, PrintZero.MaxListed + 5).Select(_ => new PrintForm { Cleaned = "0.00", RoundedToZero = true });
            var pages = PrintZero.Pages(forms);
            Assert.Equal(PrintZero.MaxListed + 5, pages.Count);
            var list = PrintZero.PageList(pages);
            Assert.EndsWith("10…", list);
            Assert.DoesNotContain("11", list);
            Assert.Contains(string.Format(Ar["Print.Zero.Pages"], "15", "1,200", list), PrintZero.Notice(pages, 1200));
        }

        [Fact]
        public void NoticeStringsHaveTheirPlaceholders()
        {
            foreach (var t in new[] { Ar, En })
            {
                Assert.Contains("{0}", t["Print.Zero.Pages"]);
                Assert.Contains("{1}", t["Print.Zero.Pages"]);
                Assert.Contains("{2}", t["Print.Zero.Pages"]);
                Assert.False(string.IsNullOrWhiteSpace(t["Print.Zero.Notice"]));
                Assert.False(string.IsNullOrWhiteSpace(t["Print.Zero.Blocked"]));
            }
        }
    }
}
