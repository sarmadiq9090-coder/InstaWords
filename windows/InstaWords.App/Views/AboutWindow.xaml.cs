using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using InstaWords.App.Services;

namespace InstaWords.App.Views
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            VersionText.Text = Loc.F("About.Version", UpdateService.CurrentVersionText);
            UpdateStatus.Text = App.Current.Updates.StatusText;
            AddShortcut(App.Current.CurrentHotkeyText, Loc.T("About.KeyHotkey"));
            AddShortcut("Ctrl+Enter", Loc.T("About.KeyCopy"));
            AddShortcut("Ctrl+P", Loc.T("About.KeyPrint"));
            AddShortcut("Esc", Loc.T("About.KeyClear"));
            AddShortcut("Ctrl+1 / 2 / 3", Loc.T("About.KeyTabs"));
            AddShortcut("Ctrl+V", Loc.T("About.KeyPaste"));
            AddShortcut("F1", Loc.T("About.KeyAbout"));
        }

        private void AddShortcut(string keys, string what)
        {
            var row = Shortcuts.RowDefinitions.Count;
            Shortcuts.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var kbd = new Border { Style = (Style)FindResource("KbdText"), Child = new TextBlock { Text = keys }, Margin = new Thickness(0, 3, 12, 3), HorizontalAlignment = HorizontalAlignment.Left };
            var text = new TextBlock { Text = what, VerticalAlignment = VerticalAlignment.Center, FontSize = 13.5 };
            Grid.SetRow(kbd, row);
            Grid.SetRow(text, row);
            Grid.SetColumn(text, 1);
            Shortcuts.Children.Add(kbd);
            Shortcuts.Children.Add(text);
        }

        private void OnLink(object sender, RequestNavigateEventArgs e)
        {
            Links.Open(e.Uri.AbsoluteUri);
            e.Handled = true;
        }

        private async void OnCheckClick(object sender, RoutedEventArgs e)
        {
            UpdateStatus.Text = Loc.T("Update.Checking");
            await App.Current.Updates.CheckAsync(interactive: true, owner: this);
            UpdateStatus.Text = App.Current.Updates.StatusText;
        }

        private void OnReportClick(object sender, RoutedEventArgs e)
        {
            Close();
            App.Current.AppWindow?.ShowReport();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
