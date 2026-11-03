using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace InstaWords.UITests
{
    /// <summary>
    /// One running InstaWords.exe with its own settings folder (INSTAWORDS_DATA_DIR), so tests never
    /// touch a real profile, start from known settings, and never call the network.
    /// </summary>
    public sealed class AppSession : IDisposable
    {
        public const string DefaultSettings = @"{
  ""schema"": 1, ""language"": ""ar"", ""unit"": ""USD"", ""hundredsForm"": ""مائة"", ""decimals"": ""round"",
  ""prefix"": """", ""suffix"": """", ""hotkey"": ""Ctrl+Alt+T"", ""hotkeyEnabled"": true,
  ""closeToTray"": false, ""minimizeToTray"": false, ""showToast"": true, ""trayHintShown"": true,
  ""autoCheckUpdates"": false, ""optionsExpanded"": false,
  ""windowLeft"": 10, ""windowTop"": 10, ""windowWidth"": 1000, ""windowHeight"": 700, ""windowMaximized"": false
}";

        public AppSession(string settingsJson = null, string updateFeedJson = null)
        {
            DataDir = Path.Combine(Path.GetTempPath(), "InstaWords-uitest-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(Path.Combine(DataDir, "settings.json"), settingsJson ?? DefaultSettings, new UTF8Encoding(false));

            var psi = new ProcessStartInfo(AppPath) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(AppPath) };
            psi.Environment["INSTAWORDS_DATA_DIR"] = DataDir;
            if (updateFeedJson != null)
            {
                var feed = Path.Combine(DataDir, "feed.json");
                File.WriteAllText(feed, updateFeedJson, new UTF8Encoding(false));
                psi.Environment["INSTAWORDS_UPDATE_FEED"] = feed;
            }
            Automation = new UIA3Automation();
            App = Application.Launch(psi);
            Window = App.GetMainWindow(Automation, TimeSpan.FromSeconds(45));
            Assert.NotNull(Window);
            Window.Focus();
            WaitUntil(() => Find("NumberInput") != null, "main window content");
        }

        public string DataDir { get; }
        public UIA3Automation Automation { get; }
        public Application App { get; }
        public Window Window { get; private set; }
        public ConditionFactory Cf => Automation.ConditionFactory;

        public static string AppPath
        {
            get
            {
                var env = Environment.GetEnvironmentVariable("INSTAWORDS_APP_PATH");
                if (!string.IsNullOrEmpty(env)) return Path.GetFullPath(env);
                var windows = RepoWindowsDir();
                foreach (var config in new[] { "Release", "Debug" })
                {
                    var p = Path.Combine(windows, "InstaWords.App", "bin", config, "net48", "InstaWords.exe");
                    if (File.Exists(p)) return p;
                }
                throw new FileNotFoundException("Build the app first: dotnet build windows/InstaWords.App -c Release");
            }
        }

        public static string ArtifactsDir
        {
            get
            {
                var env = Environment.GetEnvironmentVariable("INSTAWORDS_ARTIFACTS");
                var dir = !string.IsNullOrEmpty(env) ? env : Path.Combine(AppContext.BaseDirectory, "screenshots");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        private static string RepoWindowsDir()
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "InstaWords.sln"))) d = d.Parent;
            return d?.FullName ?? throw new DirectoryNotFoundException("windows/InstaWords.sln not found above " + AppContext.BaseDirectory);
        }

        /// <summary>
        /// The element with this AutomationId, preferring an on-screen one: the three tabs share controls
        /// (unit and language pickers), and the hidden tabs' copies may still be in the automation tree.
        /// </summary>
        public AutomationElement Find(string automationId, AutomationElement root = null)
        {
            var all = (root ?? Window).FindAllDescendants(Cf.ByAutomationId(automationId));
            return all.FirstOrDefault(e => !IsOffscreen(e)) ?? all.FirstOrDefault();
        }

        private static bool IsOffscreen(AutomationElement e)
        {
            try { return e.IsOffscreen; } catch { return true; }
        }

        public AutomationElement Get(string automationId, AutomationElement root = null, int timeoutMs = 10000)
        {
            AutomationElement el = null;
            WaitUntil(() => (el = Find(automationId, root)) != null, automationId, timeoutMs);
            return el;
        }

        /// <summary>A top-level window of the app (dialogs, the toast), by AutomationId.</summary>
        public Window GetTopLevel(string automationId, int timeoutMs = 10000)
        {
            Window w = null;
            WaitUntil(() =>
            {
                w = App.GetAllTopLevelWindows(Automation).FirstOrDefault(x => SafeId(x) == automationId)
                    ?? Window.ModalWindows.FirstOrDefault(x => SafeId(x) == automationId)
                    ?? Automation.GetDesktop().FindFirstChild(Cf.ByAutomationId(automationId))?.AsWindow();
                return w != null;
            }, automationId, timeoutMs);
            return w;
        }

        private static string SafeId(AutomationElement e)
        {
            try { return e.AutomationId; } catch { return null; }
        }

        public string ResultText => Find("ResultText")?.AsTextBox().Text ?? "";

        public static void WaitUntil(Func<bool> condition, string what, int timeoutMs = 10000)
        {
            var sw = Stopwatch.StartNew();
            Exception last = null;
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    if (condition()) return;
                }
                catch (Exception ex)
                {
                    last = ex;
                }
                Thread.Sleep(100);
            }
            throw new TimeoutException("Timed out waiting for " + what + (last != null ? ": " + last.Message : ""));
        }

        public void Screenshot(AutomationElement element, string name)
        {
            Thread.Sleep(350); // let fades and focus rings settle
            Capture.Element(element).ToFile(Path.Combine(ArtifactsDir, name + ".png"));
        }

        public void Dispose()
        {
            try
            {
                if (!App.HasExited)
                {
                    App.Close();
                    using (var p = Process.GetProcessById(App.ProcessId)) p.WaitForExit(5000);
                }
            }
            catch { /* ignore */ }
            try { if (!App.HasExited) App.Kill(); } catch { /* ignore */ }
            App.Dispose();
            Automation.Dispose();
            try { Directory.Delete(DataDir, recursive: true); } catch { /* ignore */ }
        }
    }

    /// <summary>The Windows clipboard from the test process (needs an STA thread).</summary>
    public static class TestClipboard
    {
        public static void SetText(string text) => Sta(() => System.Windows.Forms.Clipboard.SetText(text));

        public static string GetText()
        {
            string text = null;
            Sta(() => text = System.Windows.Forms.Clipboard.ContainsText() ? System.Windows.Forms.Clipboard.GetText() : null);
            return text;
        }

        private static void Sta(Action action)
        {
            Exception error = null;
            var t = new Thread(() =>
            {
                for (var i = 0; i < 10; i++)
                {
                    try { action(); error = null; return; }
                    catch (Exception ex) { error = ex; Thread.Sleep(100); }
                }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
            if (error != null) throw error;
        }
    }
}
