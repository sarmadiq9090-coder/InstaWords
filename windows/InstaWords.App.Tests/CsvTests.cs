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

        [Theory]
        [InlineData("book.xlsx")]
        [InlineData("BOOK.XLS")]
        [InlineData("sheet.ods")]
        [InlineData("macro.xlsm")]
        public void WorkbooksAreRefusedWithAdvice(string name) =>
            Assert.Equal(Csv.ImportProblem.Spreadsheet, Csv.Inspect(name, 10_000, new byte[] { 0x50, 0x4B, 3, 4 }));

        [Fact]
        public void LargeFilesAreRefusedBeforeReading()
        {
            Assert.Equal(Csv.ImportProblem.None, Csv.Inspect("a.csv", Csv.MaxImportBytes, Encoding.UTF8.GetBytes("1\n2")));
            Assert.Equal(Csv.ImportProblem.TooLarge, Csv.Inspect("a.csv", Csv.MaxImportBytes + 1, Encoding.UTF8.GetBytes("1\n2")));
            Assert.Equal(Csv.ImportProblem.Spreadsheet, Csv.Inspect("a.xlsx", Csv.MaxImportBytes * 5, null));
            Assert.Equal("20", Csv.Megabytes(Csv.MaxImportBytes));
            Assert.Equal("35.5", Csv.Megabytes(35 * 1048576L + 524288));
            Assert.Equal(100000, Csv.MaxImportRows);
        }

        [Fact]
        public void BinaryFilesAreNotReadAsText()
        {
            Assert.Equal(Csv.ImportProblem.NotText, Csv.Inspect("renamed.csv", 100, new byte[] { 0x50, 0x4B, 3, 4, 20, 0 }));        // zip (xlsx)
            Assert.Equal(Csv.ImportProblem.NotText, Csv.Inspect("old.csv", 100, new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1 }));        // OLE (xls)
            Assert.Equal(Csv.ImportProblem.NotText, Csv.Inspect("a.txt", 100, Encoding.ASCII.GetBytes("%PDF-1.7\n")));
            Assert.Equal(Csv.ImportProblem.NotText, Csv.Inspect("a.txt", 100, new byte[] { 0x31, 0x00, 0x32 }));
            Assert.Equal(Csv.ImportProblem.None, Csv.Inspect("a.txt", 100, Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("12")).ToArray()));
            Assert.Equal(Csv.ImportProblem.None, Csv.Inspect("a.csv", 100, Encoding.UTF8.GetBytes("المبلغ\r\n١٢٣")));
            Assert.Equal(Csv.ImportProblem.None, Csv.Inspect("empty.csv", 0, new byte[0]));
        }

        [Fact]
        public void ReadsTheHeadOfAFile()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(path, Enumerable.Range(0, 10).Select(i => (byte)('0' + i)).ToArray());
                Assert.Equal(Encoding.ASCII.GetBytes("0123"), Csv.ReadHead(path, 4));
                Assert.Equal(10, Csv.ReadHead(path, Csv.HeadBytes).Length);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void RefusalMessagesExistInBothLanguages()
        {
            foreach (var lang in new[] { "ar", "en" })
                foreach (var key in new[] { "Bulk.SpreadsheetFile", "Bulk.FileTooLarge", "Bulk.NotTextFile", "Bulk.TooManyRows", "Bulk.NotLoadedTitle" })
                    Assert.True(TestStrings.Table(lang).ContainsKey(key), lang + " " + key);
            TestStrings.With("en", () =>
                Assert.Equal("“book.xlsx” is too large (35.5 MB); the limit is 20 MB. Split it into smaller files.",
                    Loc.F("Bulk.FileTooLarge", "book.xlsx", "35.5", "20")));
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
