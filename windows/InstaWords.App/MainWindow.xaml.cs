using System;
using System.ComponentModel;
using System.Windows;
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
            InputBindings.Add(new KeyBinding(new RelayCommand(ShowAbout), Key.F1, ModifierKeys.None));

            // Ctrl+V anywhere on the bulk tab (outside a text box) pastes a column.
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste,
                (s, e) => BulkPage.PasteFromClipboard(),
                (s, e) => { e.CanExecute = BulkPage.IsVisible; e.Handled = BulkPage.IsVisible; }));

            StateChanged += (s, e) =>
            {
                if (WindowState == WindowState.Minimized && App.Current.Settings.MinimizeToTray) HideToTray();
            };
            Loaded += (s, e) => SinglePage.FocusInput();
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

        public void ShowReport()
        {
            var report = new ReportWindow { Owner = IsVisible ? this : null };
            if (report.Owner == null) report.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            report.ShowDialog();
        }

        private void OnAboutClick(object sender, RoutedEventArgs e) => ShowAbout();
        private void OnReportClick(object sender, RoutedEventArgs e) => ShowReport();
        private void OnUpdateBadgeClick(object sender, RoutedEventArgs e) => App.Current.Updates.ShowAvailableUpdate(this);

        /// <summary>Shows the «تحديث متاح» chip in the header.</summary>
        public void SetUpdateBadge(string version)
        {
            UpdateBadge.Visibility = string.IsNullOrEmpty(version) ? Visibility.Collapsed : Visibility.Visible;
            UpdateBadge.Content = "تحديث متاح " + version;
        }

        private void HideToTray()
        {
            Hide();
            var s = App.Current.Settings;
            if (!s.TrayHintShown)
            {
                s.TrayHintShown = true;
                App.Current.SaveSettingsSoon();
                ToastWindow.ShowMessage(true, "InstaWords ما زال يعمل",
                    "تجده في شريط المهام بجانب الساعة، والاختصار " + App.Current.CurrentHotkeyText + " يعمل من أي برنامج.");
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
