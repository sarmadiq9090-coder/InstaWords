using System.Windows;
using System.Windows.Controls;
using InstaWords.App.Models;

namespace InstaWords.App.Views
{
    /// <summary>«خيارات الصياغة». Each radio pair sits in its own panel, so every copy of this control groups independently.</summary>
    public partial class FormattingOptions : UserControl
    {
        public FormattingOptions() => InitializeComponent();

        private void OnPresetClick(object sender, RoutedEventArgs e) => (DataContext as ConversionState)?.ApplyPreset();
        private void OnNoneClick(object sender, RoutedEventArgs e) => (DataContext as ConversionState)?.ClearAffixes();
    }
}
