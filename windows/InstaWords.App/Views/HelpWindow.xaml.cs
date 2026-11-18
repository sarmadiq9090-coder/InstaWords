using System.Windows;
using InstaWords.App.Services;

namespace InstaWords.App.Views
{
    /// <summary>
    /// «كيف أستخدمه؟» / How to use: number formats (the comma rule), the clipboard shortcut, the bulk
    /// tab, cheque calibration and the Excel add-in. Opened with F1 or the ? button in the header.
    /// </summary>
    public partial class HelpWindow : Window
    {
        public const string ExcelPage = Links.Website + "excel.html";

        public HelpWindow()
        {
            InitializeComponent();
            var hotkey = App.Current.CurrentHotkeyText;
            HotkeyText.Text = Loc.F("Help.Hotkey.Body", hotkey);
            ExcelExperimental.Text = Loc.F("Help.Excel.Experimental", hotkey);
        }

        /// <summary>Shows the card over <paramref name="owner"/> (or centred on the screen).</summary>
        public static void ShowFor(Window owner)
        {
            var help = new HelpWindow { Owner = owner != null && owner.IsVisible ? owner : null };
            if (help.Owner == null) help.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            help.ShowDialog();
        }

        private void OnExcelPageClick(object sender, RoutedEventArgs e) => Links.Open(ExcelPage);

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
