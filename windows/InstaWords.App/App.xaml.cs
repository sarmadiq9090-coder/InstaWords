using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using InstaWords.App.Models;
using InstaWords.App.Services;
using InstaWords.App.Views;

namespace InstaWords.App
{
    public partial class App : Application
    {
        private DispatcherTimer _saveTimer;
        private TrayService _tray;
        private bool _exiting;

        public static new App Current => (App)Application.Current;

        /// <summary>Started with --tray (Windows startup): stay in the notification area.</summary>
        public bool StartHidden { get; set; }

        public AppSettings Settings { get; private set; }
        public ConversionState State { get; } = new ConversionState();
        public HistoryStore History { get; } = new HistoryStore();
        public HotkeyService Hotkey { get; private set; }
        public UpdateService Updates { get; } = new UpdateService();
        /// <summary>Code of the last conversion error (for problem reports; never the amount itself).</summary>
        public string LastErrorCode { get; set; }
        public bool IsExiting => _exiting;
        public MainWindow AppWindow => MainWindow as MainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += OnUnhandledException;

            var loaded = JsonStore.Load<AppSettings>(AppPaths.SettingsFile);
            Settings = loaded ?? new AppSettings();
            // «لغة الواجهة»: on the very first run, the language picked in the installer's wizard, else the
            // Windows display language. Settings from a version without the option (always Arabic) stay Arabic.
            if (string.IsNullOrEmpty(Settings.UiLanguage))
                Settings.UiLanguage = loaded != null ? Loc.Arabic : UiLanguage.InstallerChoice() ?? Loc.Auto;
            Settings.UiLanguage = Loc.NormalizeSetting(Settings.UiLanguage);
            Settings.Print = Settings.Print ?? new PrintSettings();
            UiLanguage.Apply(Settings.UiLanguage);
            Loc.Changed += (s, a) => Catalog.RaiseLanguageChanged();
            State.Load(Settings);
            State.Changed += (s, a) => SaveSettingsSoon();
            History.Load();

            _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _saveTimer.Tick += (s, a) => SaveSettings();

            _tray = new TrayService();
            _tray.OpenRequested += (s, a) => ShowMainWindow();
            _tray.ConvertRequested += (s, a) => ConvertClipboard();
            _tray.SettingsRequested += (s, a) => ShowMainWindow(InstaWords.App.MainWindow.TabSettings);
            _tray.ExitRequested += (s, a) => ExitApp();
            _tray.UpdateRequested += async (s, a) =>
            {
                ShowMainWindow();
                await Updates.CheckAsync(interactive: true, owner: AppWindow);
            };
            _tray.ReportRequested += (s, a) =>
            {
                ShowMainWindow();
                AppWindow?.ShowReport();
            };

            Hotkey = new HotkeyService();
            Hotkey.Pressed += (s, a) => ConvertClipboard();
            var hotkeyOk = ApplyHotkeySettings();

            var window = new MainWindow();
            MainWindow = window;
            if (!StartHidden) window.Show();
            Updates.CheckOnStartup();

            if (!hotkeyOk && Settings.HotkeyEnabled)
            {
                // Tell the user right away, then let them pick another combination in the settings.
                Dispatcher.BeginInvoke(new Action(() =>
                    ToastWindow.ShowMessage(false, Loc.T("Hotkey.FailedTitle"), Hotkey.LastError)), DispatcherPriority.ApplicationIdle);
            }
        }

        /// <summary>«لغة الواجهة»: "ar", "en" or "auto". Applies live and is saved.</summary>
        public void SetUiLanguage(string setting)
        {
            Settings.UiLanguage = Loc.NormalizeSetting(setting);
            UiLanguage.Apply(Settings.UiLanguage);
            _tray?.ApplyLanguage();
            ApplyHotkeySettings();
            SaveSettingsSoon();
        }

        /// <summary>Registers the hotkey from the settings. Returns false when it is enabled but could not be registered.</summary>
        public bool ApplyHotkeySettings()
        {
            Hotkey.Unregister();
            var gesture = HotkeyGesture.Parse(Settings.Hotkey) ?? HotkeyGesture.Default;
            _tray?.SetHotkeyText(Settings.HotkeyEnabled ? gesture.ToString() : "");
            if (!Settings.HotkeyEnabled) return true;
            return Hotkey.Register(gesture);
        }

        /// <summary>Tries a new hotkey; keeps the old one when the new one is taken.</summary>
        public bool TryChangeHotkey(HotkeyGesture gesture, out string error)
        {
            error = null;
            var previous = Settings.Hotkey;
            Settings.Hotkey = gesture.ToString();
            Settings.HotkeyEnabled = true;
            if (ApplyHotkeySettings())
            {
                SaveSettingsSoon();
                return true;
            }
            error = Hotkey.LastError;
            Settings.Hotkey = previous;
            ApplyHotkeySettings();
            return false;
        }

        public void ShowMainWindow(int? tab = null)
        {
            var w = AppWindow;
            if (w == null) return;
            w.ShowFromTray();
            if (tab.HasValue) w.SelectTab(tab.Value);
        }

        /// <summary>Ctrl+Alt+T: the number in the clipboard becomes its words, from any program.</summary>
        public void ConvertClipboard()
        {
            if (!ClipboardService.TryGetText(out var text))
            {
                Notify(false, Loc.T("Clip.NoAccessTitle"), Loc.T("Clip.NoAccessBody"));
                return;
            }
            var values = Csv.PastedValues(text ?? "");
            if (values.Count == 0)
            {
                Notify(false, Loc.T("Clip.NoNumberTitle"), Loc.F("Clip.NoNumberBody", CurrentHotkeyText));
                return;
            }

            var options = State.ToOptions();
            if (values.Count == 1)
            {
                var r = Conversion.Run(values[0], options);
                if (!r.Ok)
                {
                    LastErrorCode = r.ErrorCode;
                    Notify(false, Loc.F("Clip.FailedTitle", Shorten(values[0], 40)), r.ErrorMessage);
                    return;
                }
                if (!ClipboardService.TrySetText(r.Text))
                {
                    Notify(false, Loc.T("Clip.WriteFailedTitle"), Loc.T("Clip.TryAgain"));
                    return;
                }
                History.Add(values[0], r.Cleaned, options.Unit, options.Language, r.Text);
                Notify(true, Loc.F("Clip.CopiedTitle", Conversion.Grouped(r.Amount)), r.Text, Catalog.FlowFor(options.Language));
                NoteCopied(inWindow: false);
                return;
            }

            // A column copied from Excel: convert every line, keep the lines aligned.
            if (values.Count > 10000)
            {
                Notify(false, Loc.T("Clip.TooManyTitle"), Loc.T("Clip.TooManyBody"));
                return;
            }
            var results = values.Select(v => Conversion.Run(v, options)).ToList();
            var firstError = results.FindIndex(r => !r.Ok);
            if (firstError >= 0)
            {
                Notify(false, Loc.F("Clip.LineFailedTitle", firstError + 1, Shorten(values[firstError], 30)), results[firstError].ErrorMessage);
                return;
            }
            if (!ClipboardService.TrySetText(string.Join("\r\n", results.Select(r => r.Text))))
            {
                Notify(false, Loc.T("Clip.WriteFailedTitle"), Loc.T("Clip.TryAgain"));
                return;
            }
            Notify(true, Loc.F("Clip.CopiedManyTitle", Conversion.FormatCount(results.Count)), results[0].Text + "\n…", Catalog.FlowFor(options.Language));
            NoteCopied(inWindow: false);
        }

        /// <summary>
        /// An amount in words was copied: «نسخ» / Ctrl+Enter / «نسخ الكل» in the window, or the hotkey.
        /// Store copy only: counts it, and once enough copies were made asks ONCE for a rating
        /// (StoreLogic.RatingDue), never over another program. Marked as asked before the dialog shows,
        /// so either answer, or closing it, means it never asks again.
        /// </summary>
        public void NoteCopied(bool inWindow)
        {
            var kind = AppPackage.Kind;
            if (!StoreLogic.CanRate(kind)) return;
            var count = StoreLogic.CountCopy(kind, Settings.RatingAsked, Settings.CopyCount);
            if (count != Settings.CopyCount)
            {
                Settings.CopyCount = count;
                SaveSettingsSoon();
            }
            var w = AppWindow;
            var active = inWindow && w != null && w.IsVisible && w.IsActive;
            if (!StoreLogic.RatingDue(kind, Settings.RatingAsked, Settings.CopyCount, active)) return;
            Settings.RatingAsked = true;
            SaveSettings();
            // After the «تم النسخ» feedback has drawn.
            Dispatcher.BeginInvoke(new Action(() => w.ShowRatingRequest()), DispatcherPriority.ApplicationIdle);
        }

        public string CurrentHotkeyText => (HotkeyGesture.Parse(Settings.Hotkey) ?? HotkeyGesture.Default).ToString();

        private void Notify(bool ok, string title, string body, FlowDirection? bodyFlow = null)
        {
            if (!Settings.ShowToast && ok) return;
            ToastWindow.ShowMessage(ok, title, body, bodyFlow);
        }

        private static string Shorten(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "…";

        public void SaveSettingsSoon()
        {
            if (_saveTimer == null) return;
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        public void SaveSettings()
        {
            _saveTimer?.Stop();
            State.SaveTo(Settings);
            AppWindow?.CaptureWindowPlacement(Settings);
            JsonStore.Save(AppPaths.SettingsFile, Settings);
        }

        public void ExitApp()
        {
            if (_exiting) return;
            _exiting = true;
            try
            {
                SaveSettings();
                History.Save();
            }
            finally
            {
                ToastWindow.CloseAll();
                _tray?.Dispose();
                _tray = null;
                Hotkey?.Dispose();
                Hotkey = null;
                Shutdown();
            }
        }

        /// <summary>Windows is signing out or shutting down: save and let the windows close.</summary>
        protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
        {
            base.OnSessionEnding(e);
            if (e.Cancel) return;
            _exiting = true;
            SaveSettings();
            History.Save();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _tray?.Dispose();
            Hotkey?.Dispose();
            base.OnExit(e);
        }

        private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Warn("Unhandled: " + e.Exception);
            CrashLog.Write(AppPaths.DataDir, CrashLog.Source.Dispatcher, e.Exception, Program.VersionText());
            // Kept running on purpose (a single failed action should not close the app); the full error is in crash.log.
            e.Handled = true;
            try
            {
                ToastWindow.ShowMessage(false, Loc.T("App.UnexpectedError"), e.Exception.Message);
            }
            catch
            {
                // last resort: never crash on the error path
            }
        }
    }
}
