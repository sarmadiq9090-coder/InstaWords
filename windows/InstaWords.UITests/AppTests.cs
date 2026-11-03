using System;
using System.IO;
using System.Linq;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using InstaWords.Core;
using Xunit;

namespace InstaWords.UITests
{
    public class AppTests
    {
        private static TafqitOptions Options(string unit = "SAR", string language = "ar", string prefix = "", string suffix = "") =>
            new TafqitOptions { Unit = unit, Language = language, Prefix = prefix, Suffix = suffix };

        /// <summary>Picks a unit the way a user does: focus the picker, type, Enter (UI Automation as a fallback).</summary>
        internal static void PickUnit(AppSession s, string query, string unitId)
        {
            var combo = s.Get("UnitPicker");
            combo.Focus();
            Keyboard.Type(query);
            Thread.Sleep(500);
            Keyboard.Press(VirtualKeyShort.ENTER);
            Thread.Sleep(400);
            if (IsSelected(combo, unitId)) return;

            var cb = combo.AsComboBox();
            cb.Expand();
            Thread.Sleep(300);
            s.Get("unit-" + unitId, cb).Patterns.SelectionItem.Pattern.Select();
            cb.Collapse();
            AppSession.WaitUntil(() => IsSelected(combo, unitId), "unit " + unitId + " selected");
        }

        private static bool IsSelected(AutomationElement combo, string unitId)
        {
            var selection = combo.Patterns.Selection.PatternOrDefault?.Selection.ValueOrDefault;
            return selection != null && selection.Any(e => e.AutomationId == "unit-" + unitId);
        }

        [Fact]
        public void ConvertsLiveInArabicAndEnglish()
        {
            using (var s = new AppSession())
            {
                var number = s.Get("NumberInput").AsTextBox();
                number.Focus();
                number.Enter("1250.75");
                PickUnit(s, "SAR", "SAR");

                var expected = Tafqit.Convert("1250.75", Options("SAR", "ar"));
                AppSession.WaitUntil(() => s.ResultText == expected, "Arabic result «" + expected + "», got «" + s.ResultText + "»");
                Assert.Equal("1,250.75", s.Get("CleanedText").Name);
                s.Screenshot(s.Window, "01-main-ar");

                s.Get("lang-en").Click();
                var english = Tafqit.Convert("1250.75", Options("SAR", "en"));
                AppSession.WaitUntil(() => s.ResultText == english, "English result «" + english + "», got «" + s.ResultText + "»");
                s.Screenshot(s.Window, "02-main-en");

                s.Get("lang-fr").Click();
                var french = Tafqit.Convert("1250.75", Options("SAR", "fr"));
                AppSession.WaitUntil(() => s.ResultText == french, "French result");
                s.Get("lang-ar").Click();

                // Ctrl+Enter copies the result.
                number.Focus();
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.ENTER);
                AppSession.WaitUntil(() => TestClipboard.GetText() == expected, "copied text");

                // Errors are shown inline.
                number.Text = "";
                number.Enter("12abc");
                AppSession.WaitUntil(() => s.Find("ErrorText") != null && !string.IsNullOrEmpty(s.Find("ErrorText").Name), "inline error");
                Assert.Equal(Tafqit.TryConvert("12abc", Options()).ErrorMessage, s.Find("ErrorText").Name);
                s.Screenshot(s.Window, "03-main-error");

                // Esc clears.
                Keyboard.Press(VirtualKeyShort.ESCAPE);
                AppSession.WaitUntil(() => number.Text == "", "cleared input");
            }
        }

        [Fact]
        public void FormattingOptionsApplyPresets()
        {
            using (var s = new AppSession())
            {
                var number = s.Get("NumberInput").AsTextBox();
                number.Enter("300");
                PickUnit(s, "SAR", "SAR");

                var expander = s.Get("OptionsExpander");
                expander.Patterns.ExpandCollapse.Pattern.Expand();
                s.Get("PresetOnly").AsButton().Invoke();
                var only = Tafqit.Convert("300", Options("SAR", "ar", "فقط", "لا غير"));
                AppSession.WaitUntil(() => s.ResultText == only, "«فقط … لا غير» result");

                s.Get("HundredsMia").Patterns.SelectionItem.Pattern.Select();
                var mia = Tafqit.Convert("300", new TafqitOptions { Unit = "SAR", HundredsForm = "مئة", Prefix = "فقط", Suffix = "لا غير" });
                AppSession.WaitUntil(() => s.ResultText == mia, "مئة result");
                s.Screenshot(s.Window, "04-options");

                // Switching language swaps the ready-made phrase for its English equivalent.
                s.Get("lang-en").Patterns.SelectionItem.Pattern.Select();
                var en = Tafqit.Convert("300", Options("SAR", "en", "", "Only"));
                AppSession.WaitUntil(() => s.ResultText == en, "English preset result");
            }
        }

        [Fact]
        public void BulkPasteConvertsEveryRow()
        {
            using (var s = new AppSession())
            {
                s.Get("NavBulk").Click();
                PickUnit(s, "SAR", "SAR");
                var inputs = new[] { "1250.75", "٣٠٠٠", "15,000", "abc", "0.5" };
                TestClipboard.SetText(string.Join("\r\n", inputs) + "\r\n");
                s.Get("PasteButton").Click();

                AppSession.WaitUntil(() => s.Get("CountAll").Name == "5", "5 rows");
                Assert.Equal("4", s.Get("CountOk").Name);
                Assert.Equal("1", s.Get("CountErr").Name);
                var grid = s.Get("BulkGrid").AsGrid();
                Assert.Equal(5, grid.RowCount);
                s.Screenshot(s.Window, "05-bulk");

                s.Get("CopyAllButton").Click();
                var expected = string.Join("\r\n", inputs.Select(i => Tafqit.TryConvert(i, Options("SAR")).Text));
                AppSession.WaitUntil(() => TestClipboard.GetText() == expected, "copied results");

                // Ctrl+V on the grid replaces the rows.
                TestClipboard.SetText("المبلغ\r\n10\r\n20\r\n");
                s.Get("BulkGrid").Focus();
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);
                AppSession.WaitUntil(() => s.Get("CountAll").Name == "2", "header skipped, 2 rows");
            }
        }

        [Fact]
        public void BulkHandlesTenThousandRows()
        {
            using (var s = new AppSession())
            {
                s.Get("NavBulk").Click();
                TestClipboard.SetText(string.Join("\r\n", Enumerable.Range(1, 10000).Select(i => (i * 7.25).ToString(System.Globalization.CultureInfo.InvariantCulture))));
                var started = DateTime.UtcNow;
                s.Get("PasteButton").Click();
                AppSession.WaitUntil(() => s.Get("CountOk").Name == "10,000", "10,000 converted rows", 30000);
                var elapsed = DateTime.UtcNow - started;
                Assert.True(elapsed < TimeSpan.FromSeconds(20), "took " + elapsed);
                // The window stays responsive: switching tabs works right away.
                s.Get("NavSingle").Click();
                AppSession.WaitUntil(() => s.Find("NumberInput") is AutomationElement n && !n.IsOffscreen, "single tab after bulk");
            }
        }

        [Fact]
        public void HotkeyConvertsTheClipboard()
        {
            using (var s = new AppSession(AppSession.DefaultSettings.Replace("\"unit\": \"USD\"", "\"unit\": \"SAR\"")))
            {
                TestClipboard.SetText("500");
                // Another program has the focus in real use; the hotkey is system-wide.
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_T);
                var expected = Tafqit.Convert("500", Options("SAR"));
                AppSession.WaitUntil(() => TestClipboard.GetText() == expected, "clipboard words for 500", 15000);

                var toast = s.GetTopLevel("Toast");
                AppSession.WaitUntil(() => toast.FindFirstDescendant(s.Cf.ByAutomationId("ToastBody"))?.Name == expected, "toast text");
                s.Screenshot(toast, "06-toast");

                // An invalid clipboard shows an error toast and leaves the clipboard alone.
                TestClipboard.SetText("hello");
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_T);
                AppSession.WaitUntil(() => (toast.FindFirstDescendant(s.Cf.ByAutomationId("ToastTitle"))?.Name ?? "").Contains("hello"), "error toast");
                Assert.Equal("hello", TestClipboard.GetText());
                s.Screenshot(toast, "07-toast-error");

                // The conversion is in the history.
                Assert.NotNull(s.Window.FindFirstDescendant(s.Cf.ByName(expected)));
            }
        }

        [Fact]
        public void SettingsAboutAndReportScreens()
        {
            using (var s = new AppSession())
            {
                s.Get("NavSettings").Click();
                AppSession.WaitUntil(() => (s.Get("HotkeyStatus").Name ?? "").Contains("Ctrl+Alt+T"), "hotkey status");
                s.Screenshot(s.Window, "08-settings");

                Keyboard.Press(VirtualKeyShort.F1);
                var about = s.GetTopLevel("AboutWindow");
                Assert.Contains("الإصدار", about.FindFirstDescendant(s.Cf.ByAutomationId("AboutVersion")).Name);
                s.Screenshot(about, "09-about");
                about.FindFirstDescendant(s.Cf.ByAutomationId("AboutClose")).AsButton().Invoke();

                s.Get("ReportButton").Click();
                var report = s.GetTopLevel("ReportWindow");
                report.FindFirstDescendant(s.Cf.ByAutomationId("ReportTitle")).AsTextBox().Enter("النتيجة لا تظهر");
                report.FindFirstDescendant(s.Cf.ByAutomationId("ReportDescription")).AsTextBox().Enter("كتبت رقماً ولم يظهر شيء.");
                AppSession.WaitUntil(() => (report.FindFirstDescendant(s.Cf.ByAutomationId("ReportPreview")).AsTextBox().Text ?? "").Contains("كتبت رقماً"), "report preview");
                var preview = report.FindFirstDescendant(s.Cf.ByAutomationId("ReportPreview")).AsTextBox().Text;
                Assert.Contains("InstaWords", preview);   // technical info is attached by default
                Assert.DoesNotContain("1250", preview);    // never amounts
                s.Screenshot(report, "10-report");
                report.FindFirstDescendant(s.Cf.ByAutomationId("ReportClose")).AsButton().Invoke();
            }
        }

        [Fact]
        public void OffersAnUpdateFromTheFeed()
        {
            const string feed = @"{
  ""tag_name"": ""v99.0.0"", ""name"": ""InstaWords v99.0.0"", ""draft"": false, ""prerelease"": false,
  ""html_url"": ""https://github.com/sarmadiq9090-coder/tafket/releases/tag/v99.0.0"",
  ""body"": ""## InstaWords 99\n\n- **تفقيط أسرع** للقوائم الطويلة\n- إصلاحات في الفرنسية\n- Faster bulk conversion"",
  ""assets"": [{ ""name"": ""InstaWords_99.0.0_x64-setup.exe"", ""size"": 1234,
    ""browser_download_url"": ""https://github.com/sarmadiq9090-coder/tafket/releases/download/v99.0.0/InstaWords_99.0.0_x64-setup.exe"",
    ""digest"": ""sha256:0000000000000000000000000000000000000000000000000000000000000000"" }]
}";
            using (var s = new AppSession(updateFeedJson: feed))
            {
                var dialog = s.GetTopLevel("UpdateWindow", 20000);
                Assert.Contains("99.0.0", dialog.FindFirstDescendant(s.Cf.ByAutomationId("UpdateHeadline")).Name);
                var notes = dialog.FindFirstDescendant(s.Cf.ByAutomationId("UpdateNotes")).AsTextBox().Text;
                Assert.Contains("• تفقيط أسرع للقوائم الطويلة", notes);
                s.Screenshot(dialog, "11-update");
                dialog.FindFirstDescendant(s.Cf.ByAutomationId("UpdateLater")).AsButton().Invoke();
                AppSession.WaitUntil(() => s.Find("UpdateBadge") is AutomationElement b && !b.IsOffscreen, "update badge in the header");
            }
        }
    }
}
