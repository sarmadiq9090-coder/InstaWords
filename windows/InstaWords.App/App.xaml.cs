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

            Settings = JsonStore.Load<AppSettings>(AppPaths.SettingsFile) ?? new AppSettings();
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
                    ToastWindow.ShowMessage(false, "تعذّر تفعيل اختصار الحافظة", Hotkey.LastError)), DispatcherPriority.ApplicationIdle);
            }
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
                Notify(false, "تعذّر الوصول إلى الحافظة", "برنامج آخر يستخدم الحافظة الآن. حاول مرة أخرى.");
                return;
            }
            var values = Csv.PastedValues(text ?? "");
            if (values.Count == 0)
            {
                Notify(false, "لا يوجد رقم في الحافظة", "انسخ رقماً (مثل 1250.75) ثم اضغط " + CurrentHotkeyText + ".");
                return;
            }

            var options = State.ToOptions();
            if (values.Count == 1)
            {
                var r = Conversion.Run(values[0], options);
                if (!r.Ok)
                {
                    LastErrorCode = r.ErrorCode;
                    Notify(false, "تعذّر تحويل «" + Shorten(values[0], 40) + "»", r.ErrorMessage);
                    return;
                }
                if (!ClipboardService.TrySetText(r.Text))
                {
                    Notify(false, "تعذّرت الكتابة في الحافظة", "حاول مرة أخرى.");
                    return;
                }
                History.Add(values[0], r.Cleaned, options.Unit, options.Language, r.Text);
                Notify(true, "نُسخ المبلغ بالحروف · ‎" + Conversion.Grouped(r.Cleaned) + "‎", r.Text, Catalog.FlowFor(options.Language));
                return;
            }

            // A column copied from Excel: convert every line, keep the lines aligned.
            if (values.Count > 10000)
            {
                Notify(false, "القائمة طويلة جداً", "استعمل تبويب «دفعة» لأكثر من 10,000 رقم.");
                return;
            }
            var results = values.Select(v => Conversion.Run(v, options)).ToList();
            var firstError = results.FindIndex(r => !r.Ok);
            if (firstError >= 0)
            {
                Notify(false, "تعذّر تحويل السطر " + (firstError + 1) + ": «" + Shorten(values[firstError], 30) + "»", results[firstError].ErrorMessage);
                return;
            }
            if (!ClipboardService.TrySetText(string.Join("\r\n", results.Select(r => r.Text))))
            {
                Notify(false, "تعذّرت الكتابة في الحافظة", "حاول مرة أخرى.");
                return;
            }
            Notify(true, "نُسخ " + Conversion.FormatCount(results.Count) + " مبلغاً بالحروف", results[0].Text + "\n…", Catalog.FlowFor(options.Language));
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

        protected override void OnExit(ExitEventArgs e)
        {
            _tray?.Dispose();
            Hotkey?.Dispose();
            base.OnExit(e);
        }

        private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Warn("Unhandled: " + e.Exception);
            e.Handled = true;
            try
            {
                ToastWindow.ShowMessage(false, "حدث خطأ غير متوقع", e.Exception.Message);
            }
            catch
            {
                // last resort: never crash on the error path
            }
        }
    }
}
