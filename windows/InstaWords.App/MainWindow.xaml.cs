using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using InstaWords.App.Models;
using InstaWords.App.Services;
using InstaWords.App.Views;

namespace InstaWords.App
{
    public partial class MainWindow : Window
    {
        public const int TabSingle = 0;
        public const int TabBulk = 1;
        public const int TabSettings = 2;

        public MainWindow()
        {
            InitializeComponent();
            RestorePlacement(App.Current.Settings);
            Nav.SelectedIndex = TabSingle;

            InputBindings.Add(new KeyBinding(new RelayCommand(() => SelectTab(TabSingle)), Key.D1, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(new RelayCommand(() => SelectTab(TabBulk)), Key.D2, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(new RelayCommand(() => SelectTab(TabSettings)), Key.D3, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(new RelayCommand(() => SelectTab(TabSingle)), Key.NumPad1, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(new RelayCommand(() => SelectTab(TabBulk)), Key.NumPad2, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(new RelayCommand(() => SelectTab(TabSettings)), Key.NumPad3, ModifierKeys.Control));
            // F1: «كيف أستخدمه؟» (About has its own button).
            InputBindings.Add(new KeyBinding(new RelayCommand(ShowHelp), Key.F1, ModifierKeys.None));
            // Ctrl+P: print the current amount, or the bulk tab's selected rows.
            InputBindings.Add(new KeyBinding(new RelayCommand(PrintCurrent), Key.P, ModifierKeys.Control));

            // Ctrl+V anywhere on the bulk tab (outside a text box) pastes a column.
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste,
                (s, e) => BulkPage.PasteFromClipboard(),
                (s, e) => { e.CanExecute = BulkPage.IsVisible; e.Handled = BulkPage.IsVisible; }));

            StateChanged += (s, e) =>
            {
                if (WindowState == WindowState.Minimized && App.Current.Settings.MinimizeToTray) HideToTray();
            };
            Loaded += (s, e) => SinglePage.FocusInput();
            Loc.Changed += OnLanguageChanged;
            Closed += (s, e) => Loc.Changed -= OnLanguageChanged;
        }

        public int CurrentTab => Nav.SelectedIndex;

        public void SelectTab(int index)
        {
            Nav.SelectedIndex = index;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (index == TabSingle) SinglePage.FocusInput();
                else if (index == TabBulk) BulkPage.FocusGrid();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void OnNavChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (Nav.SelectedIndex < 0) { Nav.SelectedIndex = TabSingle; return; }
            if (SinglePage == null) return;
            SinglePage.Visibility = Nav.SelectedIndex == TabSingle ? Visibility.Visible : Visibility.Collapsed;
            BulkPage.Visibility = Nav.SelectedIndex == TabBulk ? Visibility.Visible : Visibility.Collapsed;
            SettingsPage.Visibility = Nav.SelectedIndex == TabSettings ? Visibility.Visible : Visibility.Collapsed;
            if (Nav.SelectedIndex == TabSettings) SettingsPage.Refresh();
        }

        public void ShowFromTray()
        {
            if (!IsVisible) Show();
            if (WindowState == WindowState.Minimized) WindowState = App.Current.Settings.WindowMaximized ? WindowState.Maximized : WindowState.Normal;
            Activate();
            // Bring to front even when another program has the focus.
            Topmost = true;
            Topmost = false;
            Focus();
        }

        public void ShowAbout()
        {
            var about = new AboutWindow { Owner = IsVisible ? this : null };
            if (about.Owner == null) about.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            about.ShowDialog();
        }

        public void ShowHelp() => HelpWindow.ShowFor(this);

        public void ShowReport()
        {
            var report = new ReportWindow { Owner = IsVisible ? this : null };
            if (report.Owner == null) report.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            report.ShowDialog();
        }

        private void OnAboutClick(object sender, RoutedEventArgs e) => ShowAbout();
        private void OnHelpClick(object sender, RoutedEventArgs e) => ShowHelp();
        private void OnReportClick(object sender, RoutedEventArgs e) => ShowReport();
        private void OnUpdateBadgeClick(object sender, RoutedEventArgs e) => App.Current.Updates.ShowAvailableUpdate(this);

        /// <summary>Shows the «تحديث متاح» chip in the header.</summary>
        public void SetUpdateBadge(string version)
        {
            UpdateBadge.Visibility = string.IsNullOrEmpty(version) ? Visibility.Collapsed : Visibility.Visible;
            _updateVersion = version;
            if (string.IsNullOrEmpty(version)) UpdateBadge.SetResourceReference(ContentControl.ContentProperty, "Main.UpdateAvailable");
            else UpdateBadge.Content = Loc.F("Main.UpdateAvailableVersion", version);
        }

        private string _updateVersion;

        /// <summary>The interface language changed: text set from code follows.</summary>
        private void OnLanguageChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_updateVersion)) SetUpdateBadge(_updateVersion);
        }

        /// <summary>Ctrl+P: the print dialog for the current tab (the amount, or the selected bulk rows).</summary>
        public void PrintCurrent()
        {
            if (CurrentTab == TabBulk) BulkPage.PrintRows();
            else SinglePage.Print();
        }

        /// <summary>Opens «طباعة» for one or more amounts (one page each).</summary>
        public void ShowPrint(System.Collections.Generic.IList<string> amounts)
        {
            var w = new PrintWindow(amounts) { Owner = IsVisible ? this : null };
            if (w.Owner == null) w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            w.ShowDialog();
        }

        private void HideToTray()
        {
            Hide();
            var s = App.Current.Settings;
            if (!s.TrayHintShown)
            {
                s.TrayHintShown = true;
                App.Current.SaveSettingsSoon();
                ToastWindow.ShowMessage(true, Loc.T("Tray.StillRunningTitle"), Loc.F("Tray.StillRunningBody", App.Current.CurrentHotkeyText));
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (App.Current.IsExiting) return;
            App.Current.SaveSettings();
            if (App.Current.Settings.CloseToTray)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }
            e.Cancel = true;
            Dispatcher.BeginInvoke(new Action(App.Current.ExitApp));
        }

        private void RestorePlacement(AppSettings s)
        {
            if (s.WindowWidth >= MinWidth && s.WindowHeight >= MinHeight)
            {
                Width = Math.Min(s.WindowWidth, SystemParameters.VirtualScreenWidth);
                Height = Math.Min(s.WindowHeight, SystemParameters.VirtualScreenHeight);
            }
            if (s.WindowLeft.HasValue && s.WindowTop.HasValue)
            {
                // Only if the title bar would still be on a screen (monitors change).
                var l = s.WindowLeft.Value;
                var t = s.WindowTop.Value;
                var vs = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                    SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
                if (vs.Contains(new Point(l + 80, t + 20)) && vs.Contains(new Point(l + Width - 80, t + 20)))
                {
                    WindowStartupLocation = WindowStartupLocation.Manual;
                    Left = l;
                    Top = t;
                }
            }
            // Never larger than the screen (e.g. 1366×768 laptops).
            var work = SystemParameters.WorkArea;
            Width = Math.Min(Width, work.Width);
            Height = Math.Min(Height, work.Height);
            if (s.WindowMaximized) WindowState = WindowState.Maximized;
        }

        public void CaptureWindowPlacement(AppSettings s)
        {
            if (!IsLoaded) return;
            var r = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
            if (r.IsEmpty || double.IsNaN(r.Left) || r.Width < 100) return;
            s.WindowLeft = Math.Round(r.Left);
            s.WindowTop = Math.Round(r.Top);
            s.WindowWidth = Math.Round(r.Width);
            s.WindowHeight = Math.Round(r.Height);
            s.WindowMaximized = WindowState == WindowState.Maximized;
        }
    }

    /// <summary>Minimal ICommand for key bindings.</summary>
    public sealed class RelayCommand : ICommand
    {
        private readonly Action _run;
        public RelayCommand(Action run) => _run = run;
        public event EventHandler CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => _run();
    }
}
