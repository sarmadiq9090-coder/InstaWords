using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using InstaWords.App.Services;

namespace InstaWords.App.Views
{
    public partial class SettingsPage : UserControl
    {
        private HotkeyGesture _pending;

        public SettingsPage()
        {
            InitializeComponent();
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this)) return;
            DataContext = App.Current.State;
            DataPath.Text = AppPaths.DataDir;
            Refresh();
            Loc.Changed += (s, e) => Refresh();
        }

        private void ShowUiLanguage()
        {
            var setting = App.Current.Settings.UiLanguage;
            UiLangAr.IsChecked = setting == Loc.Arabic;
            UiLangEn.IsChecked = setting == Loc.English;
            UiLangAuto.IsChecked = setting != Loc.Arabic && setting != Loc.English;
        }

        private void OnUiLanguageClick(object sender, RoutedEventArgs e)
        {
            var setting = (string)((FrameworkElement)sender).Tag;
            App.Current.SetUiLanguage(setting);
            Refresh(); // after the hotkey was registered again, so its status is in the new language too
        }

        /// <summary>Re-reads the settings (the Run key may have been changed by the installer).</summary>
        public void Refresh()
        {
            var s = App.Current.Settings;
            HotkeyEnabled.IsChecked = s.HotkeyEnabled;
            ShowToast.IsChecked = s.ShowToast;
            CloseToTray.IsChecked = s.CloseToTray;
            MinimizeToTray.IsChecked = s.MinimizeToTray;
            AutoUpdate.IsChecked = s.AutoCheckUpdates;
            StartWithWindows.IsChecked = StartupService.IsEnabled;
            _pending = null;
            HotkeyBox.Text = App.Current.CurrentHotkeyText;
            SaveHotkeyButton.IsEnabled = false;
            ShowHotkeyStatus();
            UpdateStatus.Text = App.Current.Updates.StatusText;
            ShowUiLanguage();
        }

        private void ShowHotkeyStatus(string error = null)
        {
            var app = App.Current;
            var s = app.Settings;
            if (error == null && !s.HotkeyEnabled) SetStatus(Loc.T("Settings.HotkeyDisabled"), kind: 0);
            else if (error == null && app.Hotkey.IsRegistered) SetStatus(Loc.F("Settings.HotkeyWorks", app.CurrentHotkeyText), kind: 1);
            else SetStatus(error ?? app.Hotkey.LastError ?? Loc.T("Settings.HotkeyFailed"), kind: 2);
        }

        /// <summary>kind: 0 neutral, 1 ok, 2 error.</summary>
        private void SetStatus(string text, int kind)
        {
            HotkeyStatus.Text = text;
            var fg = (Brush)FindResource(kind == 2 ? "DangerBrush" : kind == 1 ? "OkBrush" : "MutedBrush");
            HotkeyStatus.Foreground = fg;
            HotkeyStatusIcon.Foreground = fg;
            HotkeyStatusIcon.Data = (Geometry)FindResource(kind == 2 ? "IconAlert" : kind == 1 ? "IconCheck" : "IconInfo");
            HotkeyStatusBox.Background = (Brush)FindResource(kind == 2 ? "DangerSoftBrush" : kind == 1 ? "BrandSofterBrush" : "BgSoftBrush");
        }

        // ---------- Hotkey capture ----------

        private void OnHotkeyFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // While choosing, the current hotkey must not fire (it would swallow the key press).
            App.Current.Hotkey.Unregister();
            SetStatus(Loc.T("Settings.HotkeyPressNow"), kind: 0);
        }

        private void OnHotkeyBlur(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (App.Current.IsExiting) return;
            App.Current.ApplyHotkeySettings();
            if (_pending == null) ShowHotkeyStatus();
            else SetStatus(Loc.F("Settings.HotkeyPressSave", _pending), kind: 0);
        }

        private void OnHotkeyKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            var mods = Keyboard.Modifiers;
            if (key == Key.Tab && (mods & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) == 0) return; // keep Tab navigation
            e.Handled = true;
            if (HotkeyGesture.IsModifierKey(key))
            {
                HotkeyBox.Text = new HotkeyGesture(mods, Key.None).ToString().Replace("None", "…");
                return;
            }
            var g = new HotkeyGesture(mods, key);
            HotkeyBox.Text = g.ToString();
            if (!g.IsValid)
            {
                _pending = null;
                SaveHotkeyButton.IsEnabled = false;
                SetStatus(Loc.T("Settings.HotkeyInvalid"), kind: 2);
                return;
            }
            _pending = g;
            SaveHotkeyButton.IsEnabled = true;
            SetStatus(Loc.F("Settings.HotkeyPressSave", g), kind: 0);
        }

        private void OnSaveHotkeyClick(object sender, RoutedEventArgs e) => SaveHotkey(_pending);

        private void OnDefaultHotkeyClick(object sender, RoutedEventArgs e) => SaveHotkey(HotkeyGesture.Default);

        private void SaveHotkey(HotkeyGesture g)
        {
            if (g == null) return;
            if (App.Current.TryChangeHotkey(g, out var error))
            {
                _pending = null;
                HotkeyEnabled.IsChecked = true;
                HotkeyBox.Text = g.ToString();
                SaveHotkeyButton.IsEnabled = false;
                ShowHotkeyStatus();
            }
            else
            {
                SaveHotkeyButton.IsEnabled = true;
                ShowHotkeyStatus(error);
            }
        }

        private void OnHotkeyEnabledClick(object sender, RoutedEventArgs e)
        {
            App.Current.Settings.HotkeyEnabled = HotkeyEnabled.IsChecked == true;
            App.Current.ApplyHotkeySettings();
            App.Current.SaveSettingsSoon();
            ShowHotkeyStatus();
        }

        private void OnTryClick(object sender, RoutedEventArgs e) => App.Current.ConvertClipboard();

        // ---------- Other settings ----------

        private void OnShowToastClick(object sender, RoutedEventArgs e)
        {
            App.Current.Settings.ShowToast = ShowToast.IsChecked == true;
            App.Current.SaveSettingsSoon();
        }

        private void OnTrayClick(object sender, RoutedEventArgs e)
        {
            App.Current.Settings.CloseToTray = CloseToTray.IsChecked == true;
            App.Current.Settings.MinimizeToTray = MinimizeToTray.IsChecked == true;
            App.Current.SaveSettingsSoon();
        }

        private void OnAutoUpdateClick(object sender, RoutedEventArgs e)
        {
            App.Current.Settings.AutoCheckUpdates = AutoUpdate.IsChecked == true;
            App.Current.SaveSettingsSoon();
        }

        private async void OnCheckUpdateClick(object sender, RoutedEventArgs e)
        {
            UpdateStatus.Text = Loc.T("Update.Checking");
            await App.Current.Updates.CheckAsync(interactive: true, owner: Window.GetWindow(this));
            UpdateStatus.Text = App.Current.Updates.StatusText;
        }

        private void OnStartWithWindowsClick(object sender, RoutedEventArgs e)
        {
            var want = StartWithWindows.IsChecked == true;
            if (!StartupService.SetEnabled(want)) StartWithWindows.IsChecked = !want;
            StartWithWindows.IsChecked = StartupService.IsEnabled;
        }

        private void OnOpenFolderClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.DataDir);
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + AppPaths.DataDir + "\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ToastWindow.ShowMessage(false, Loc.T("Settings.OpenFolderFailed"), ex.Message);
            }
        }

        private void OnClearHistoryClick(object sender, RoutedEventArgs e) => App.Current.History.Clear();
    }
}
