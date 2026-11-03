using System.IO;
using System.Linq;
using System.Text;
using InstaWords.App.Services;
using Xunit;

namespace InstaWords.App.Tests
{
    public class CsvTests
    {
        [Fact]
        public void OneColumnWithHeaderAndQuotes()
        {
            var values = Csv.ColumnValues("\uFEFFالمبلغ\r\n\"1,500\"\r\n200\r\n\r\n\"3,000.50\"\r\n");
            Assert.Equal(new[] { "1,500", "200", "3,000.50" }, values);
        }

        [Fact]
        public void UnquotedThousandsSeparatorStaysOneValue() =>
            Assert.Equal(new[] { "1,500", "20" }, Csv.ColumnValues("1,500\n20"));

        [Fact]
        public void SemicolonAndTabFilesUseTheFirstColumn()
        {
            Assert.Equal(new[] { "10", "20" }, Csv.ColumnValues("amount;note\n10;a\n20;b"));
            Assert.Equal(new[] { "10", "20" }, Csv.ColumnValues("10\tx\n20\ty"));
        }

        [Fact]
        public void ArabicDigitsAreNotAHeader() => Assert.Equal(new[] { "١٢٣", "٤" }, Csv.ColumnValues("١٢٣\n٤"));

        [Fact]
        public void PastedExcelColumn()
        {
            Assert.Equal(new[] { "1250.75", "300", "abc" }, Csv.PastedValues("1250.75\r\n300\t\r\n\r\n\"abc\"\r\n"));
        }

        [Theory]
        [InlineData("=1+2", "'=1+2")]
        [InlineData("+5", "'+5")]
        [InlineData("@SUM(A1)", "'@SUM(A1)")]
        [InlineData("-5", "-5")]
        [InlineData("-1,500.25", "-1,500.25")]
        [InlineData("-cmd", "'-cmd")]
        [InlineData("\tx", "'\tx")]
        [InlineData("ألف ريال", "ألف ريال")]
        [InlineData("", "")]
        public void GuardsAgainstFormulaInjection(string input, string expected) => Assert.Equal(expected, Csv.SafeCell(input));

        [Fact]
        public void WritesQuotedCells()
        {
            var text = Csv.Write(new[] { new[] { "a", "b,c", "say \"hi\"", "x\ny" } });
            Assert.Equal("a,\"b,c\",\"say \"\"hi\"\"\",\"x\ny\"\r\n", text);
        }

        [Fact]
        public void ReadsUtf8BomUtf16AndArabicAnsiFiles()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // built into .NET Framework
            var path = Path.GetTempFileName();
            try
            {
                const string text = "المبلغ\r\n١٢٣\r\n";
                File.WriteAllText(path, text, new UTF8Encoding(true));
                Assert.Equal(text, Csv.ReadText(path));
                File.WriteAllText(path, text, Encoding.Unicode);
                Assert.Equal(text, Csv.ReadText(path));
                File.WriteAllText(path, text, new UTF8Encoding(false));
                Assert.Equal(text, Csv.ReadText(path));
                File.WriteAllBytes(path, Encoding.GetEncoding(1256).GetBytes("المبلغ\r\n500\r\n"));
                Assert.Equal("المبلغ\r\n500\r\n", Csv.ReadText(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void RoundTripsThroughParse()
        {
            var rows = new[] { new[] { "الرقم", "التفقيط" }, new[] { "1,500", "ألف وخمسمائة" } };
            var parsed = Csv.Parse(Csv.Write(rows), ',');
            Assert.Equal(rows.Select(r => r.ToList()).ToList(), parsed);
        }
    }
}
