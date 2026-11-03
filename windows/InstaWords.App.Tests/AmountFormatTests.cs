using System.Linq;
using InstaWords.App.Services;
using InstaWords.Core;
using Xunit;

namespace InstaWords.App.Tests
{
    /// <summary>
    /// The figures beside the words («الرقم المعتمد», print amount box, «عدة مبالغ», history): money
    /// with its subunit's decimals from data/units.json, equal to the amount the words state.
    /// </summary>
    public class AmountFormatTests
    {
        private static ConversionResult Run(string input, string unit, string decimals = "round") =>
            Conversion.Run(input, new TafqitOptions { Unit = unit, Decimals = decimals });

        private static string Shown(string input, string unit, string decimals = "round") =>
            Conversion.Grouped(Run(input, unit, decimals).Amount);

        [Theory]
        [InlineData("45750.5", "SAR", "45,750.50")]
        [InlineData("45750", "SAR", "45,750.00")]
        [InlineData("45,750.50", "SAR", "45,750.50")]
        [InlineData("1250.75", "SAR", "1,250.75")]
        [InlineData("0.5", "USD", "0.50")]
        [InlineData("5.25", "KWD", "5.250")]
        [InlineData("1750000", "IQD", "1,750,000.000")]
        [InlineData("12.5", "BHD", "12.500")]
        [InlineData("3", "JOD", "3.000")]
        [InlineData("7.1", "OMR", "7.100")]
        [InlineData("1.5", "BTC", "1.50000000")]
        public void MoneyKeepsItsSubunitDecimals(string input, string unit, string expected) =>
            Assert.Equal(expected, Shown(input, unit));

        [Theory]
        [InlineData("12.5", "none", "12.5")]
        [InlineData("12.50", "none", "12.5")]
        [InlineData("1250", "none", "1,250")]
        [InlineData("12.5", "kilogram", "12.5")] // a subunit (gram), but not money
        [InlineData("2.5", "meter", "2.5")]
        [InlineData("1500", "JPY", "1,500")]     // money without a subunit
        [InlineData("12", "piece", "12")]
        public void OtherUnitsAndPlainNumbersAreUnchanged(string input, string unit, string expected) =>
            Assert.Equal(expected, Shown(input, unit));

        [Theory]
        [InlineData("10.125", "SAR", "10.13")]
        [InlineData("10.124", "SAR", "10.12")]
        [InlineData("0.995", "SAR", "1.00")]
        [InlineData("999.999", "USD", "1,000.00")]
        [InlineData("5.2504", "KWD", "5.250")]
        [InlineData("5.2505", "KWD", "5.251")]
        [InlineData("9.9999", "KWD", "10.000")]
        public void RoundedLikeTheWords(string input, string unit, string expected)
        {
            var r = Run(input, unit);
            Assert.True(r.Ok);
            Assert.Equal(expected, Conversion.Grouped(r.Amount));
            // The figures state what the words state.
            Assert.Equal(Tafqit.Convert(r.Amount, new TafqitOptions { Unit = unit }), r.Text);
        }

        [Fact]
        public void DecimalsErrorModeShowsNoRoundedAmount()
        {
            var r = Run("10.125", "SAR", "error");
            Assert.False(r.Ok);
            Assert.Equal(TafqitErrorCodes.TooManyDecimals, r.ErrorCode);
            Assert.Equal("10.125", r.Amount); // the bulk grid shows what was read, not a rounded figure
            Assert.Equal("10.13", Run("10.13", "SAR", "error").Amount);
            Assert.Equal("10.10", Run("10.1", "SAR", "error").Amount);
        }

        [Theory]
        [InlineData("٤٥٧٥٠٫٥", "SAR", "45,750.50")]
        [InlineData("٤٥٬٧٥٠", "SAR", "45,750.00")]
        [InlineData("٥٫٢٥", "KWD", "5.250")]
        [InlineData("١٠٫١٢٥", "SAR", "10.13")]
        [InlineData("١٢٫٥", "none", "12.5")]
        public void ArabicIndicInput(string input, string unit, string expected) =>
            Assert.Equal(expected, Shown(input, unit));

        [Theory]
        [InlineData("999999999999999", "SAR", "999,999,999,999,999.00")]
        [InlineData("999999999999999.99", "SAR", "999,999,999,999,999.99")]
        [InlineData("123456789012345.675", "SAR", "123,456,789,012,345.68")] // a double would lose this
        [InlineData("999999999999999.9994", "KWD", "999,999,999,999,999.999")]
        [InlineData("900719925474099.3", "IQD", "900,719,925,474,099.300")]
        [InlineData("999999999999999.123456789", "none", "999,999,999,999,999.123456789")]
        public void LargeAmountsStayExact(string input, string unit, string expected) =>
            Assert.Equal(expected, Shown(input, unit));

        [Fact]
        public void RoundingPastTheLimitIsAnError()
        {
            // 999,999,999,999,999.995 rounds to 1,000 trillion: the engine refuses it, so no figures either.
            var r = Run("999999999999999.995", "SAR");
            Assert.False(r.Ok);
            Assert.Equal(TafqitErrorCodes.TooLarge, r.ErrorCode);
            // The formatter alone carries over without losing digits.
            Assert.Equal("1000000000000000.00", AmountFormat.Used("999999999999999.995", 2));
        }

        [Fact]
        public void UsedWorksOnStrings()
        {
            Assert.Equal("45750.50", AmountFormat.Used("45750.5", 2));
            Assert.Equal("45750.00", AmountFormat.Used("45750", 2));
            Assert.Equal("0.000", AmountFormat.Used("0", 3));
            Assert.Equal("0.01", AmountFormat.Used("0.005", 2));
            Assert.Equal("45750.5", AmountFormat.Used("45750.5", 0));
            Assert.Equal("", AmountFormat.Used("", 2));
            Assert.Null(AmountFormat.Used(null, 2));
            Assert.Equal("abc", AmountFormat.Used("abc", 2));
            Assert.Equal("45,750.50", AmountFormat.Display("45750.5", 2));
            Assert.Equal("1,250", AmountFormat.Grouped("1250"));
            Assert.Equal("-1,250.5", AmountFormat.Grouped("-1250.5"));
        }

        [Fact]
        public void DecimalsComeFromTheCatalog()
        {
            Assert.Equal(2, AmountFormat.DecimalsFor("SAR"));
            Assert.Equal(2, AmountFormat.DecimalsFor("ريال سعودي"));
            foreach (var code in new[] { "KWD", "IQD", "BHD", "JOD", "OMR" }) Assert.Equal(3, AmountFormat.DecimalsFor(code));
            Assert.Equal(8, AmountFormat.DecimalsFor("BTC"));
            Assert.Equal(0, AmountFormat.DecimalsFor("none"));
            Assert.Equal(0, AmountFormat.DecimalsFor(""));
            Assert.Equal(0, AmountFormat.DecimalsFor((string)null));
            Assert.Equal(0, AmountFormat.DecimalsFor("nope"));
            Assert.Equal(0, AmountFormat.DecimalsFor("kilogram"));
            // Every currency with a subunit gets its subunit's digits; nothing else gets any.
            foreach (var u in Tafqit.Units)
                Assert.Equal(AmountFormat.IsMoney(u) && u.HasSubunit ? u.SubunitDigits : 0, AmountFormat.DecimalsFor(u));
            Assert.Contains(Tafqit.Units, u => AmountFormat.IsMoney(u) && u.SubunitDigits == 3);
            Assert.DoesNotContain(Tafqit.Units.Where(u => !AmountFormat.IsMoney(u)), u => AmountFormat.DecimalsFor(u) != 0);
        }

        [Fact]
        public void PrintedFiguresUseTheAmount()
        {
            var r = Run("45750.5", "SAR");
            var cheque = new PrintForm { Template = PrintTemplate.Cheque, Cleaned = r.Amount, Words = r.Text };
            Assert.Equal("#45,750.50#", PrintTemplates.Map(cheque).Single(i => i.Field == PrintField.Figures).Value);
            var receipt = new PrintForm { Template = PrintTemplate.Receipt, Cleaned = Run("5.25", "KWD").Amount, UnitText = "KWD" };
            Assert.Equal("5.250 KWD", PrintTemplates.Map(receipt).Single(i => i.Field == PrintField.Figures).Value);
        }
    }
}
