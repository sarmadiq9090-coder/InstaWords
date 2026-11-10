using System.Windows;

namespace InstaWords.App.Views
{
    /// <summary>
    /// «هل يساعدك InstaWords؟»: asked once in the Store copy (App.NoteCopied has already saved that it was asked,
    /// so «ليس الآن», Esc and closing the window all mean «never again»). «قيّم في المتجر» closes this dialog,
    /// then MainWindow opens the Store's own rating dialog (StoreRating).
    /// </summary>
    public partial class RatingWindow : Window
    {
        public RatingWindow()
        {
            InitializeComponent();
        }

        /// <summary>The user chose «قيّم في المتجر».</summary>
        public bool WantsToRate { get; private set; }

        private void OnRateClick(object sender, RoutedEventArgs e)
        {
            WantsToRate = true;
            Close();
        }

        private void OnNotNowClick(object sender, RoutedEventArgs e) => Close();
    }
}
