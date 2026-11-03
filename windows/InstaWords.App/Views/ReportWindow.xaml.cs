using System.Collections.Generic;
using System.Windows;
using InstaWords.App.Services;

namespace InstaWords.App.Views
{
    /// <summary>«الإبلاغ عن مشكلة أو اقتراح»: builds a GitHub issue link or a text to copy. Never sends anything itself.</summary>
    public partial class ReportWindow : Window
    {
        private readonly IList<KeyValuePair<string, string>> _tech;
        private bool _ready;

        public ReportWindow()
        {
            InitializeComponent();
            _tech = SystemInfo.Collect();
            _ready = true;
            Refresh();
            Loaded += (s, e) => TitleBox.Focus();
        }

        private ReportInput Input => new ReportInput
        {
            IsBug = TypeBug.IsChecked == true,
            Title = TitleBox.Text,
            Description = DescriptionBox.Text,
            Details = DetailsBox.Text,
            IncludeTechnicalInfo = IncludeTech.IsChecked == true,
            TechnicalInfo = _tech,
        };

        private void OnChanged(object sender, RoutedEventArgs e) => Refresh();

        private void Refresh()
        {
            if (!_ready) return;
            var bug = TypeBug.IsChecked == true;
            DescriptionLabel.Text = bug ? "وصف المشكلة" : "الاقتراح";
            DetailsLabel.Text = bug ? "خطوات إعادة المشكلة (اختياري)" : "لماذا تحتاجه؟ (اختياري)";
            Preview.Text = ReportBuilder.BuildText(Input);
            Status.Text = "";
        }

        private void OnOpenGitHubClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(DescriptionBox.Text))
            {
                Status.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
                Status.Text = "اكتب وصفاً قصيراً أولاً.";
                DescriptionBox.Focus();
                return;
            }
            Links.Open(ReportBuilder.BuildIssueUrl(Input));
            Status.Foreground = (System.Windows.Media.Brush)FindResource("OkBrush");
            Status.Text = "فُتحت صفحة البلاغ في المتصفح. راجعها ثم اضغط «Submit».";
        }

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            var ok = ClipboardService.TrySetText(ReportBuilder.BuildText(Input));
            Status.Foreground = (System.Windows.Media.Brush)FindResource(ok ? "OkBrush" : "DangerBrush");
            Status.Text = ok ? "نُسخ التقرير. الصقه في بريد أو رسالة أو في GitHub." : "تعذّر النسخ.";
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
