using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using Xunit;

namespace InstaWords.UITests
{
    /// <summary>
    /// Microsoft Store listing screenshots (docs/distribution/microsoft-store.md), in the Arabic and the
    /// English interface: store-ar-1-convert … store-ar-4-settings and store-en-1-convert … store-en-4-settings.
    /// The main window and the print dialog are maximised and each picture is the screen's work area, which
    /// the maximised window fills exactly: no desktop, no taskbar. CI sets a 1920×1080 screen first, so the
    /// pictures are about 1920×1040 (Partner Center needs at least 1366×768).
    /// Every amount in words, history entry and preview is the app's own output, made through the UI.
    /// </summary>
    public class StoreScreenshotTests
    {
        private sealed class Listing
        {
            public string Ui;
            public (string Amount, string Unit)[] History;  // converted and copied first, oldest first
            public (string Amount, string Unit) Main;          // on screen in the convert picture
            public string[] Column;                            // the Excel column for the bulk picture
            public string ColumnUnit;
            public string Company, Party, Purpose;
        }

        private static readonly Listing Arabic = new Listing
        {
            Ui = "ar",
            History = new[] { ("3,750", "SAR"), ("12,400.75", "AED"), ("1,750,000", "IQD"), ("2,150.25", "USD") },
            Main = ("45,750.25", "SAR"),
            Column = new[] { "المبلغ", "1,250.75", "3,400.00", "15,000.00", "860.45", "27,999.99", "120,000.00", "4,315.25" },
            ColumnUnit = "SAR",
            Company = "مؤسسة النخيل التجارية",
            Party = "شركة الواحة للمقاولات",
            Purpose = "دفعة من قيمة عقد صيانة المبنى",
        };

        private static readonly Listing English = new Listing
        {
            Ui = "en",
            History = new[] { ("2,150.25", "USD"), ("12,400.75", "AED"), ("3,750", "SAR"), ("860.45", "EUR") },
            Main = ("18,945.75", "USD"),
            Column = new[] { "Amount", "1,250.75", "3,400.00", "15,000.00", "860.45", "27,999.99", "120,000.00", "4,315.25" },
            ColumnUnit = "AED",
            Company = "Palm Trading Co.",
            Party = "Oasis Contracting LLC",
            Purpose = "Building maintenance contract, first instalment",
        };

        [Theory]
        [InlineData("ar")]
        [InlineData("en")]
        public void ListingScreenshots(string ui)
        {
            var l = ui == "ar" ? Arabic : English;
            using (var s = new AppSession(Settings(l), seed: dir => Seed(dir, l)))
            {
                Maximize(s.Window);
                var number = s.Get("NumberInput").AsTextBox();

                // 1. Convert: a few earlier conversions in the history, then the amount on screen.
                foreach (var (amount, unit) in l.History)
                {
                    var before = HistoryCount(s);
                    Convert(s, number, amount, unit);
                    s.Get("CopyButton").AsButton().Invoke();
                    AppSession.WaitUntil(() => HistoryCount(s) == before + 1, "history entry for " + amount);
                }
                number.Text = "";
                s.Get("PresetOnly").AsButton().Invoke(); // «فقط … لا غير» / "… Only"
                Convert(s, number, l.Main.Amount, l.Main.Unit);
                var count = HistoryCount(s);
                AppSession.WaitUntil(() => HistoryCount(s) == count + 1, "the amount on screen in the history", 6000);
                number.Focus();
                ScrollToTop(s, "SingleScroll", s.Window);
                Shot(s.Window, "store-" + ui + "-1-convert");

                // 2. Print: a receipt voucher with the company name and logo.
                s.Get("PrintButton").Click();
                var dialog = s.GetTopLevel("PrintWindow");
                Maximize(dialog);
                s.Get("PrintTemplateReceipt", dialog).Patterns.SelectionItem.Pattern.Select();
                var logoLoaded = dialog.FindFirstDescendant(s.Cf.ByAutomationId("PrintLogoRemove"));
                Console.WriteLine("Logo in the print dialog: " + (logoLoaded != null && !logoLoaded.IsOffscreen) + "; logo file exists: "
                    + File.Exists(Path.Combine(s.DataDir, "logo.png")));
                s.Get("PrintParty", dialog).AsTextBox().Text = l.Party;
                s.Get("PrintPurpose", dialog).AsTextBox().Text = l.Purpose;
                AppSession.WaitUntil(() => (dialog.FindFirstDescendant(s.Cf.ByAutomationId("PreviewWords"))?.Name ?? "").Length > 10, "words on the voucher");
                s.Get("PrintAmount", dialog).Focus();
                ScrollToTop(s, "PrintFieldsScroll", dialog);
                Thread.Sleep(800);
                Shot(dialog, "store-" + ui + "-2-print");
                s.Get("PrintClose", dialog).AsButton().Invoke();
                AppSession.WaitUntil(() => !s.App.GetAllTopLevelWindows(s.Automation).Any(w => SafeId(w) == "PrintWindow"), "print dialog closed");

                // 3. Excel: a column copied from a spreadsheet (header row and formatted numbers), pasted.
                s.Get("NavBulk").Click();
                AppTests.PickUnit(s, l.ColumnUnit, l.ColumnUnit);
                TestClipboard.SetText(string.Join("\r\n", l.Column) + "\r\n");
                s.Get("PasteButton").Click();
                var rows = (l.Column.Length - 1).ToString();
                AppSession.WaitUntil(() => s.Get("CountAll").Name == rows, rows + " rows");
                AppSession.WaitUntil(() => s.Get("CountErr").Name == "0", "no errors");
                Shot(s.Window, "store-" + ui + "-3-excel");

                // 4. Settings, from the top.
                s.Get("NavSettings").Click();
                Thread.Sleep(500);
                ScrollToTop(s, "SettingsScroll", s.Window);
                Shot(s.Window, "store-" + ui + "-4-settings");
            }
        }

        /// <summary>The test settings: maximised, formatting options open.</summary>
        private static string Settings(Listing l)
        {
            var json = AppSession.DefaultSettings
                .Replace("\"windowMaximized\": false", "\"windowMaximized\": true")
                .Replace("\"optionsExpanded\": false", "\"optionsExpanded\": true")
                .Replace("\"uiLanguage\": \"ar\"", "\"uiLanguage\": \"" + l.Ui + "\"")
                .Replace("\"unit\": \"USD\"", "\"unit\": \"" + l.Main.Unit + "\"");
            // English listing: English words too (the output language is independent of the interface).
            if (l.Ui == "en") json = json.Replace("\"language\": \"ar\"", "\"language\": \"en\"");
            return json;
        }

        /// <summary>The voucher header: the company name and a logo, a PNG made from the app icon.</summary>
        private static void Seed(string dir, Listing l)
        {
            var logo = Path.Combine(dir, "logo.png");
            var ico = Path.Combine(AppSession.RepoWindowsDir(), "InstaWords.App", "Assets", "InstaWords.ico");
            using (var icon = new Icon(ico, 256, 256))
            using (var bitmap = icon.ToBitmap())
                bitmap.Save(logo, System.Drawing.Imaging.ImageFormat.Png);
            Console.WriteLine("Logo " + logo + ": " + new FileInfo(logo).Length + " bytes");
            var settings = Path.Combine(dir, "settings.json");
            var json = File.ReadAllText(settings, Encoding.UTF8).TrimEnd();
            var print = ",\n  \"print\": { \"lastTemplate\": \"receipt\", \"companyName\": \"" + l.Company + "\", \"logoPath\": \""
                + logo.Replace("\\", "\\\\") + "\", \"nextNumber\": \"1042\" }\n}";
            File.WriteAllText(settings, json.Substring(0, json.Length - 1).TrimEnd() + print, new UTF8Encoding(false));
        }

        private static void Convert(AppSession s, TextBox number, string amount, string unit)
        {
            // Empty first: a unit change under the previous amount must not add that amount to the history.
            number.Text = "";
            AppTests.PickUnit(s, unit, unit);
            number.Enter(amount);
            var wanted = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);
            AppSession.WaitUntil(() => decimal.TryParse(s.Find("CleanedText")?.Name ?? "", System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var used) && used == wanted, "number used " + amount);
            AppSession.WaitUntil(() => s.ResultText.Length > 10, "words for " + amount);
        }

        private static int HistoryCount(AppSession s) =>
            s.Get("HistoryList").FindAllChildren(s.Cf.ByControlType(ControlType.ListItem)).Length;

        private static void Maximize(Window w)
        {
            var pattern = w.Patterns.Window.Pattern;
            if (pattern.WindowVisualState.Value != WindowVisualState.Maximized) pattern.SetWindowVisualState(WindowVisualState.Maximized);
            AppSession.WaitUntil(() => pattern.WindowVisualState.Value == WindowVisualState.Maximized, "maximised window");
            w.Focus();
            Thread.Sleep(500);
        }

        private static void ScrollToTop(AppSession s, string scrollViewerId, AutomationElement root)
        {
            var scroll = s.Get(scrollViewerId, root).Patterns.Scroll.PatternOrDefault;
            if (scroll != null && scroll.VerticallyScrollable.Value) scroll.SetScrollPercent(-1, 0);
        }

        /// <summary>The work area of the window's screen, which a maximised window covers exactly.</summary>
        private static void Shot(Window w, string name)
        {
            var area = System.Windows.Forms.Screen.FromHandle(w.Properties.NativeWindowHandle.Value).WorkingArea;
            Assert.Equal(WindowVisualState.Maximized, w.Patterns.Window.Pattern.WindowVisualState.Value);
            // Park the pointer on the empty middle of the title bar: no hover highlight, no tooltip.
            Mouse.Position = new Point(area.Left + area.Width / 2, area.Top + 12);
            Thread.Sleep(700);
            Capture.Rectangle(new Rectangle(area.X, area.Y, area.Width, area.Height))
                .ToFile(Path.Combine(AppSession.ArtifactsDir, name + ".png"));
            Console.WriteLine("Store screenshot " + name + ": " + area.Width + "x" + area.Height);
        }

        private static string SafeId(AutomationElement e)
        {
            try { return e.AutomationId; } catch { return null; }
        }
    }
}
