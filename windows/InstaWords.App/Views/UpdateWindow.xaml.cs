using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Windows;
using InstaWords.App.Services;

namespace InstaWords.App.Views
{
    /// <summary>«يتوفر إصدار جديد»: release notes, then download + verify + silent install (or the release page for portable copies).</summary>
    public partial class UpdateWindow : Window
    {
        private readonly GitHubRelease _release;
        private readonly UpdateService _service;
        private readonly bool _installed;
        private CancellationTokenSource _cts;

        public UpdateWindow(UpdateService service, GitHubRelease release)
        {
            InitializeComponent();
            _service = service;
            _release = release;
            _installed = UpdateService.IsInstalled;
            UpdateLogic.TryParseVersion(release.TagName, out var v, out _);
            Headline.Text = "يتوفر إصدار جديد " + UpdateLogic.Display(v);
            Subline.Text = "لديك الإصدار " + UpdateService.CurrentVersionText + (string.IsNullOrEmpty(release.Name) ? "" : " · " + release.Name);
            var notes = UpdateLogic.PlainNotes(release.Body);
            Notes.Text = notes.Length > 0 ? notes : "لا توجد ملاحظات لهذا الإصدار.";
            if (!_installed)
            {
                PortableNote.Visibility = Visibility.Visible;
                UpdateNow.Content = "فتح صفحة التنزيل";
            }
        }

        private async void OnUpdateNowClick(object sender, RoutedEventArgs e)
        {
            if (!_installed)
            {
                UpdateService.OpenReleasePage(_release);
                Close();
                return;
            }
            ErrorBox.Visibility = Visibility.Collapsed;
            ProgressPanel.Visibility = Visibility.Visible;
            UpdateNow.IsEnabled = false;
            SkipButton.IsEnabled = false;
            Later.Content = "إلغاء";
            _cts = new CancellationTokenSource();
            var progress = new Progress<double>(p =>
            {
                Progress.Value = p;
                ProgressPercent.Text = Math.Round(p * 100).ToString(CultureInfo.InvariantCulture) + "%";
            });
            try
            {
                var asset = UpdateLogic.SelectSetupAsset(_release.Assets);
                var path = await UpdateService.DownloadInstallerAsync(asset, progress, _cts.Token);
                ProgressText.Text = "تم التحقق من الملف. جارٍ التثبيت…";
                UpdateService.RunInstallerAndExit(path);
            }
            catch (Exception ex) when (ex is OperationCanceledException && _cts.IsCancellationRequested)
            {
                ResetAfterFailure(null);
            }
            catch (Exception ex)
            {
                ResetAfterFailure(UpdateService.Describe(ex));
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void ResetAfterFailure(string error)
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
            UpdateNow.IsEnabled = true;
            SkipButton.IsEnabled = true;
            Later.Content = "لاحقاً";
            if (error != null)
            {
                ErrorText.Text = error;
                ErrorBox.Visibility = Visibility.Visible;
            }
        }

        private void OnLaterClick(object sender, RoutedEventArgs e)
        {
            if (_cts != null) { _cts.Cancel(); return; }
            Close();
        }

        private void OnSkipClick(object sender, RoutedEventArgs e)
        {
            _service.Skip(_release);
            App.Current.AppWindow?.SetUpdateBadge(null);
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _cts?.Cancel();
            base.OnClosing(e);
        }
    }
}
