using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using InstaWords.Core;
using Xunit;

namespace InstaWords.UITests
{
    /// <summary>The English interface (P1-13) and «طباعة» / Print (P2-15).</summary>
    public class PrintAndLanguageTests
    {
        private static readonly Regex ArabicLetter = new Regex("[ء-ي]");

        private static string Words(string number, string unit = "SAR", string language = "ar") =>
            Tafqit.Convert(number, new TafqitOptions { Unit = unit, Language = language });

        private static bool IsLeftOf(AutomationElement a, AutomationElement b) => a.BoundingRectangle.Left < b.BoundingRectangle.Left;

        /// <summary>All the preview's word lines (a cheque wraps them over two lines), joined.</summary>
        private static string PreviewWords(AppSession s, Window dialog) =>
            string.Join(" ", dialog.FindAllDescendants(s.Cf.ByAutomationId("PreviewWords")).Select(e => e.Name).Where(n => !string.IsNullOrEmpty(n)));

        private static string NameOf(AppSession s, Window dialog, string id) =>
            dialog.FindFirstDescendant(s.Cf.ByAutomationId(id))?.Name ?? "";

        private static void Select(AppSession s, Window dialog, string id) =>
            s.Get(id, dialog).Patterns.SelectionItem.Pattern.Select();

        private static void Close(AppSession s, Window dialog)
        {
            s.Get("PrintClose", dialog).AsButton().Invoke();
            AppSession.WaitUntil(() => !s.App.GetAllTopLevelWindows(s.Automation).Any(w => SafeId(w) == "PrintWindow"), "print dialog closed");
        }

        private static string SafeId(AutomationElement e)
        {
            try { return e.AutomationId; } catch { return null; }
        }

        [Fact]
        public void EnglishInterface()
        {
            using (var s = new AppSession(AppSession.EnglishSettings))
            {
                // Labels are English and the layout runs left to right.
                AppSession.WaitUntil(() => s.Get("NavSingle").Name == "Convert", "English navigation, got «" + s.Get("NavSingle").Name + "»");
                Assert.Equal("Bulk", s.Get("NavBulk").Name);
                Assert.Equal("Settings", s.Get("NavSettings").Name);
                Assert.Equal("Copy", s.Get("CopyButton").Name);
                Assert.Equal("Print", s.Get("PrintButton").Name);
                Assert.True(IsLeftOf(s.Get("NavSingle"), s.Get("NavBulk")), "LTR: Convert is left of Bulk");
                Assert.True(IsLeftOf(s.Get("CopyButton"), s.Get("PrintButton")), "LTR: Copy is left of Print");

                // The output language is independent: still Arabic words.
                var number = s.Get("NumberInput").AsTextBox();
                number.Focus();
                number.Enter("1250.75");
                AppTests.PickUnit(s, "SAR", "SAR");
                var expected = Words("1250.75");
                AppSession.WaitUntil(() => s.ResultText == expected, "Arabic words in the English interface");
                s.Screenshot(s.Window, "12-main-en-ui");

                // Engine errors are shown in English.
                number.Text = "";
                number.Enter("12abc");
                Assert.Equal(TafqitErrorCodes.InvalidFormat, Tafqit.TryConvert("12abc", new TafqitOptions()).ErrorCode);
                AppSession.WaitUntil(() => s.Find("ErrorText")?.Name == "The number format is not valid.", "English error, got «" + s.Find("ErrorText")?.Name + "»");
                Assert.DoesNotMatch(ArabicLetter, s.Find("ErrorText").Name);
                number.Text = "";
                number.Enter("1250.75");

                // Bulk tab.
                s.Get("NavBulk").Click();
                TestClipboard.SetText("1250.75\r\n300\r\nabc\r\n");
                s.Get("PasteButton").Click();
                AppSession.WaitUntil(() => s.Get("CountAll").Name == "3", "3 rows");
                Assert.Equal("Copy results", s.Get("CopyAllButton").Name);
                Assert.Equal("Print", s.Get("BulkPrintButton").Name);
                AppSession.WaitUntil(() => (s.Get("BulkStatus").Name ?? "").StartsWith("Pasted 3 numbers"), "English status");
                s.Screenshot(s.Window, "13-bulk-en-ui");

                // Settings.
                s.Get("NavSettings").Click();
                AppSession.WaitUntil(() => (s.Get("UiLanguageTitle").Name ?? "").StartsWith("Interface language"), "interface language card");
                AppSession.WaitUntil(() => (s.Get("HotkeyStatus").Name ?? "").Contains("works from any program"), "English hotkey status");
                Assert.True(s.Get("UiLangEn").Patterns.SelectionItem.Pattern.IsSelected.Value, "English selected");
                s.Screenshot(s.Window, "14-settings-en-ui");

                // Report a problem or suggestion: the no-GitHub option first.
                s.Get("ReportButton").Click();
                var report = s.GetTopLevel("ReportWindow");
                report.FindFirstDescendant(s.Cf.ByAutomationId("ReportDescription")).AsTextBox().Enter("I typed a number and nothing appeared.");
                AppSession.WaitUntil(() => (report.FindFirstDescendant(s.Cf.ByAutomationId("ReportPreview")).AsTextBox().Text ?? "").Contains("nothing appeared"), "English report preview");
                Assert.Contains(NameOf(s, report, "ReportPrimary"), new[] { "Contact FastKiwi", "Send by email" });
                Assert.Equal("Open a GitHub issue (needs a GitHub account)", NameOf(s, report, "ReportOpenGitHub"));
                Assert.Equal("Copy the report", NameOf(s, report, "ReportCopy"));
                Assert.StartsWith("Nothing is sent automatically", NameOf(s, report, "ReportHint"));
                Assert.True(IsLeftOf(report.FindFirstDescendant(s.Cf.ByAutomationId("ReportPrimary")), report.FindFirstDescendant(s.Cf.ByAutomationId("ReportCopy"))), "LTR: primary option first");
                s.Screenshot(report, "23-report-en-ui");
                s.Get("ReportClose", report).AsButton().Invoke();
                AppSession.WaitUntil(() => !s.App.GetAllTopLevelWindows(s.Automation).Any(w => SafeId(w) == "ReportWindow"), "report closed");

                // Print dialog (Ctrl+P from the Convert tab).
                s.Get("NavSingle").Click();
                Thread.Sleep(300);
                s.Get("NumberInput").Focus();
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_P);
                var dialog = s.GetTopLevel("PrintWindow");
                Assert.Equal("Cheque", NameOf(s, dialog, "PrintTemplateCheque"));
                Select(s, dialog, "PrintTemplateReceipt");
                AppSession.WaitUntil(() => NameOf(s, dialog, "PreviewTitle") == "Receipt Voucher", "English receipt title");
                AppSession.WaitUntil(() => PreviewWords(s, dialog) == expected, "words on the English receipt");
                Assert.Contains("Microsoft Print to PDF", NameOf(s, dialog, "PrintPdfHint"));
                s.Screenshot(dialog, "15-print-en-ui");
                Close(s, dialog);
            }
        }

        [Fact]
        public void InterfaceLanguageSwitchesLive()
        {
            using (var s = new AppSession())
            {
                Assert.Equal("تفقيط رقم", s.Get("NavSingle").Name);
                Assert.True(IsLeftOf(s.Get("NavBulk"), s.Get("NavSingle")), "RTL: تفقيط رقم is right of عدة مبالغ");

                s.Get("NavSettings").Click();
                s.Get("UiLangEn").Patterns.SelectionItem.Pattern.Select();
                AppSession.WaitUntil(() => s.Get("NavSingle").Name == "Convert", "switched to English");
                AppSession.WaitUntil(() => IsLeftOf(s.Get("NavSingle"), s.Get("NavBulk")), "switched to left-to-right");
                Assert.Equal("Settings", s.Get("NavSettings").Name);
                s.Screenshot(s.Window, "22-language-switch-en");

                s.Get("UiLangAr").Patterns.SelectionItem.Pattern.Select();
                AppSession.WaitUntil(() => s.Get("NavSingle").Name == "تفقيط رقم", "back to Arabic");
                AppSession.WaitUntil(() => IsLeftOf(s.Get("NavBulk"), s.Get("NavSingle")), "back to right-to-left");
                AppSession.WaitUntil(() => (s.Get("HotkeyStatus").Name ?? "").Contains("يعمل من أي برنامج"), "Arabic hotkey status again");
            }
        }

        [Fact]
        public void PrintPreviewShowsTheWords()
        {
            using (var s = new AppSession())
            {
                var number = s.Get("NumberInput").AsTextBox();
                number.Focus();
                number.Enter("1250.75");
                AppTests.PickUnit(s, "SAR", "SAR");
                var expected = Words("1250.75");
                AppSession.WaitUntil(() => s.ResultText == expected, "result");

                s.Get("PrintButton").Click();
                var dialog = s.GetTopLevel("PrintWindow");
                Select(s, dialog, "PrintTemplateCheque");
                AppSession.WaitUntil(() => s.Get("PrintWords", dialog).AsTextBox().Text == expected, "words in the dialog");
                AppSession.WaitUntil(() => PreviewWords(s, dialog) == expected, "words on the cheque, got «" + PreviewWords(s, dialog) + "»");
                AppSession.WaitUntil(() => NameOf(s, dialog, "PreviewFigures") == "#1,250.75#", "figures box");
                s.Get("PrintParty", dialog).AsTextBox().Enter("مؤسسة النخيل التجارية");
                Thread.Sleep(400);
                s.Screenshot(dialog, "16-print-cheque");

                Select(s, dialog, "PrintTemplateReceipt");
                AppSession.WaitUntil(() => NameOf(s, dialog, "PreviewTitle") == "سند قبض", "receipt title");
                AppSession.WaitUntil(() => PreviewWords(s, dialog) == expected, "words on the receipt");
                s.Get("PrintPurpose", dialog).AsTextBox().Enter("دفعة إيجار شهر نوفمبر");
                Thread.Sleep(400);
                s.Screenshot(dialog, "17-print-receipt");

                Select(s, dialog, "PrintTemplatePayment");
                AppSession.WaitUntil(() => NameOf(s, dialog, "PreviewTitle") == "سند صرف", "payment title");
                AppSession.WaitUntil(() => PreviewWords(s, dialog) == expected, "words on the payment voucher");
                s.Screenshot(dialog, "18-print-payment");

                Select(s, dialog, "PrintTemplateStrip");
                AppSession.WaitUntil(() => PreviewWords(s, dialog) == expected, "words on the strip");
                s.Screenshot(dialog, "19-print-strip");

                // A typed amount updates the preview live, in the current output language.
                Select(s, dialog, "PrintTemplateCheque");
                var amount = s.Get("PrintAmount", dialog).AsTextBox();
                amount.Text = "";
                amount.Enter("300");
                AppSession.WaitUntil(() => PreviewWords(s, dialog) == Words("300"), "live preview after typing");
                AppSession.WaitUntil(() => NameOf(s, dialog, "PreviewFigures") == "#300.00#", "whole amount with decimals, got «" + NameOf(s, dialog, "PreviewFigures") + "»");

                // Money keeps its subunit decimals in the amount box: 45,750.50, not 45,750.5.
                amount.Text = "";
                amount.Enter("45750.5");
                AppSession.WaitUntil(() => NameOf(s, dialog, "PreviewFigures") == "#45,750.50#", "cheque figures 45,750.50, got «" + NameOf(s, dialog, "PreviewFigures") + "»");
                Thread.Sleep(400);
                s.Screenshot(dialog, "16b-print-cheque-decimals");
                Select(s, dialog, "PrintTemplateReceipt");
                AppSession.WaitUntil(() => NameOf(s, dialog, "PreviewFigures") == "45,750.50 SAR", "voucher amount 45,750.50 SAR, got «" + NameOf(s, dialog, "PreviewFigures") + "»");
                Thread.Sleep(400);
                s.Screenshot(dialog, "17b-print-receipt-decimals");
                Select(s, dialog, "PrintTemplateCheque");
                amount.Text = "";
                amount.Enter("300");
                AppSession.WaitUntil(() => PreviewWords(s, dialog) == Words("300"), "back to 300");

                // Calibration: the grid and an offset.
                s.Get("PrintShowGrid", dialog).AsToggleButton().Toggle();
                s.Get("PrintOffsets", dialog).Patterns.ExpandCollapse.Pattern.Expand();
                var x = s.Get("PrintOffsetAllX", dialog).AsTextBox();
                x.Text = "";
                x.Enter("2.5");
                Keyboard.Press(VirtualKeyShort.ENTER);
                AppSession.WaitUntil(() => s.Get("PrintOffsetAllX", dialog).AsTextBox().Text == "2.5", "offset kept");
                Thread.Sleep(400);
                s.Screenshot(dialog, "20-print-calibration");
                Close(s, dialog);

                // Bulk: one page per selected row (here: every valid row).
                s.Get("NavBulk").Click();
                TestClipboard.SetText("1250.75\r\n300\r\nabc\r\n15,000\r\n");
                s.Get("PasteButton").Click();
                AppSession.WaitUntil(() => s.Get("CountOk").Name == "3", "3 valid rows");
                s.Get("BulkPrintButton").Click();
                var bulk = s.GetTopLevel("PrintWindow");
                AppSession.WaitUntil(() => NameOf(s, bulk, "PrintPageInfo").Contains("3"), "3 pages, got «" + NameOf(s, bulk, "PrintPageInfo") + "»");
                Assert.False(string.IsNullOrEmpty(NameOf(s, bulk, "PrintBulkInfo")));
                Select(s, bulk, "PrintTemplateReceipt");
                AppSession.WaitUntil(() => bulk.FindAllDescendants(s.Cf.ByAutomationId("PreviewTitle")).Length == 3, "three receipts");
                var words = bulk.FindAllDescendants(s.Cf.ByAutomationId("PreviewWords")).Select(e => e.Name).ToList();
                Assert.Equal(new[] { Words("1250.75"), Words("300"), Words("15,000") }, words);
                s.Screenshot(bulk, "21-print-bulk");
                Close(s, bulk);
            }
        }
    }
}
