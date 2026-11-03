using System.Windows.Controls;
using InstaWords.App.Models;

namespace InstaWords.App.Views
{
    public partial class LanguagePicker : UserControl
    {
        public LanguagePicker()
        {
            InitializeComponent();
            List.ItemsSource = Catalog.Languages;
        }

        public ListBox ListBox => List;
    }
}
