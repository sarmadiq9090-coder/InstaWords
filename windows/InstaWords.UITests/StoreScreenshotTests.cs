using System.Drawing;
using System.IO;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using Xunit;

namespace InstaWords.UITests
{
    /// <summary>
    /// Microsoft Store listing screenshots (docs/distribution/microsoft-store.md): the main window at
    /// 1366×768, the smallest desktop size Partner Center accepts, in the Arabic and the English interface
    /// (store-ar-*.png, store-en-*.png). The print preview needs a taller window: 1440×900.
    /// CI sets a 1920×1080 screen first; on a smaller screen the window is clamped and the files are smaller.
    /// </summary>
    public class StoreScreenshotTests
    {
        private static string Settings(string ui, int width, int height)
        {
            var json = AppSession.DefaultSettings
                .Replace("\"windowWidth\": 1040, \"windowHeight\": 800", "\"windowWidth\": " + width + ", \"windowHeight\": " + height)
                .Replace("\"uiLanguage\": \"ar\"", "\"uiLanguage\": \"" + ui + "\"");
            // English listing: English words too (the output language is independent of the interface).
            return ui == "en" ? json.Replace("\"language\": \"ar\"", "\"language\": \"en\"") : json;
        }

        [Theory]
        [InlineData("ar")]
        [InlineData("en")]
        public void ListingScreenshots(string ui)
        {
            using (var s = new AppSession(Settings(ui, 1366, 768)))
            {
                var number = s.Get("NumberInput").AsTextBox();
                number.Focus();
                number.Enter("1250.75");
                AppSession.WaitUntil(() => s.ResultText.Length > 10, "result");
                s.Get("OptionsExpander").Patterns.ExpandCollapse.Pattern.Expand();
                s.Screenshot(s.Window, "store-" + ui + "-1-convert");

                s.Get("NavBulk").Click();
                TestClipboard.SetText("1250.75\r\n٣٠٠٠\r\n15,000\r\n0.5\r\n99.99\r\n1000000\r\n");
                s.Get("PasteButton").Click();
                AppSession.WaitUntil(() => s.Get("CountAll").Name == "6", "6 rows");
                s.Screenshot(s.Window, "store-" + ui + "-2-bulk");

                s.Get("NavSettings").Click();
                Thread.Sleep(500);
                s.Screenshot(s.Window, "store-" + ui + "-3-settings");
                Report(s, ui, "1366x768");
            }

            using (var s = new AppSession(Settings(ui, 1440, 900)))
            {
                var number = s.Get("NumberInput").AsTextBox();
                number.Focus();
                number.Enter("1250.75");
                AppSession.WaitUntil(() => s.ResultText.Length > 10, "result");
                s.Get("PrintButton").Click();
                var dialog = s.GetTopLevel("PrintWindow");
                s.Get("PrintTemplateReceipt", dialog).Patterns.SelectionItem.Pattern.Select();
                Thread.Sleep(800);
                // The dialog over the main window, as one 1440×900 picture.
                var r = s.Window.BoundingRectangle;
                Capture.Rectangle(new Rectangle(r.X, r.Y, r.Width, r.Height))
                    .ToFile(Path.Combine(AppSession.ArtifactsDir, "store-" + ui + "-4-print.png"));
                s.Get("PrintClose", dialog).AsButton().Invoke();
            }
        }

        private static void Report(AppSession s, string ui, string wanted)
        {
            var r = s.Window.BoundingRectangle;
            // Not an assertion: a small CI screen must not fail the UI tests, only these pictures.
            System.Console.WriteLine("Store screenshots (" + ui + "): window " + r.Width + "x" + r.Height + ", wanted " + wanted);
        }
    }
}
