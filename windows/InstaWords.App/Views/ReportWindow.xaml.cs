using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using InstaWords.App.Services;

namespace InstaWords.App.Views
{
    /// <summary>
    /// «الإبلاغ عن مشكلة أو اقتراح». First option: reach FastKiwi without GitHub (a mailto: message when
    /// <see cref="SupportContact.Email"/> is set, otherwise copy the report and open the support page; Telegram
    /// when set). Then a GitHub issue (needs an account) and «نسخ التقرير». Never sends anything itself.
    /// </summary>
    public partial class ReportWindow : Window
    {
        private readonly IList<KeyValuePair<string, string>> _tech;
        private bool _ready;

        public ReportWindow()
        {
            InitializeComponent();
            _tech = SystemInfo.Collect();
            if (SupportContact.ConfiguredEmail != null)
            {
                PrimaryAction.SetResourceReference(ContentControl.ContentProperty, "Report.SendEmail");
                Hint.SetResourceReference(TextBlock.TextProperty, "Report.HintEmail");
            }
            if (SupportContact.ConfiguredTelegram != null) TelegramButton.Visibility = Visibility.Visible;
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
            DescriptionLabel.Text = Loc.T(bug ? "Report.BugDescription" : "Report.IdeaDescription");
            DetailsLabel.Text = Loc.T(bug ? "Report.Steps" : "Report.Why");
            Preview.Text = ReportBuilder.BuildText(Input);
            Status.Text = "";
        }

        private bool CheckDescription()
        {
            if (!string.IsNullOrWhiteSpace(DescriptionBox.Text)) return true;
            Show(false, Loc.T("Report.NeedDescription"));
            DescriptionBox.Focus();
            return false;
        }

        private void Show(bool ok, string text)
        {
            Status.Foreground = (System.Windows.Media.Brush)FindResource(ok ? "OkBrush" : "DangerBrush");
            Status.Text = text;
        }

        /// <summary>Email when configured, otherwise (or when Windows has no mail app) copy + the support page.</summary>
        private void OnPrimaryClick(object sender, RoutedEventArgs e)
        {
            if (!CheckDescription()) return;
            var input = Input;
            var mail = ReportBuilder.BuildMailto(input, SupportContact.Email);
            if (mail != null)
            {
                // A shortened body: the full report goes to the clipboard first, to paste into the message.
                var copied = mail.Truncated && ClipboardService.TrySetText(ReportBuilder.BuildText(input));
                if (Links.TryOpenMail(mail.Url))
                {
                    Show(true, Loc.T(!mail.Truncated ? "Report.MailOpened" : copied ? "Report.MailOpenedTruncated" : "Report.MailOpenedTruncatedNoCopy"));
                    return;
                }
            }
            var ok = ClipboardService.TrySetText(ReportBuilder.BuildText(input));
            Links.Open(SupportContact.SupportPage(Loc.Language));
            Show(ok, Loc.T(ok ? "Report.PageOpened" : "Report.PageOpenedNoCopy"));
        }

        private void OnTelegramClick(object sender, RoutedEventArgs e)
        {
            var url = SupportContact.ConfiguredTelegram;
            if (url == null || !CheckDescription()) return;
            var ok = ClipboardService.TrySetText(ReportBuilder.BuildText(Input));
            Links.Open(url);
            Show(ok, Loc.T(ok ? "Report.TelegramOpened" : "Report.PageOpenedNoCopy"));
        }

        private void OnOpenGitHubClick(object sender, RoutedEventArgs e)
        {
            if (!CheckDescription()) return;
            Links.Open(ReportBuilder.BuildIssueUrl(Input));
            Show(true, Loc.T("Report.Opened"));
        }

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            var ok = ClipboardService.TrySetText(ReportBuilder.BuildText(Input));
            Show(ok, Loc.T(ok ? "Report.Copied" : "Report.CopyFailed"));
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
