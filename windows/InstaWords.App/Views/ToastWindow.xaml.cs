using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using InstaWords.App.Services;

namespace InstaWords.App.Views
{
    /// <summary>
    /// Small topmost notification in the bottom corner of the screen. It never takes the focus,
    /// so the user keeps typing in their own program, and fades out by itself.
    /// </summary>
    public partial class ToastWindow : Window
    {
        private static ToastWindow _current;
        private readonly DispatcherTimer _timer;

        public ToastWindow()
        {
            InitializeComponent();
            _timer = new DispatcherTimer();
            _timer.Tick += (s, e) => FadeOut();
            MouseEnter += (s, e) => _timer.Stop();
            MouseLeave += (s, e) => _timer.Start();
            MouseLeftButtonUp += (s, e) => FadeOut();
            SourceInitialized += (s, e) =>
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                var ex = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
                NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, ex | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW);
            };
            SizeChanged += (s, e) => PlaceInCorner();
        }

        public static void ShowMessage(bool ok, string title, string body, FlowDirection? bodyFlow = null)
        {
            var t = _current;
            if (t == null || t.IsClosedOrClosing)
            {
                t = _current = new ToastWindow();
                t.Closed += (s, e) => { if (_current == s) _current = null; };
            }
            t.SetContent(ok, title, body, bodyFlow);
        }

        public static void CloseAll() => _current?.Close();

        private bool IsClosedOrClosing { get; set; }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            IsClosedOrClosing = true;
            base.OnClosing(e);
        }

        private void SetContent(bool ok, string title, string body, FlowDirection? bodyFlow)
        {
            TitleText.Text = title ?? "";
            BodyText.Text = body ?? "";
            BodyText.FlowDirection = bodyFlow ?? FlowDirection.RightToLeft;
            BodyText.TextAlignment = TextAlignment.Left; // start of the paragraph in its own direction
            BodyText.Visibility = string.IsNullOrEmpty(body) ? Visibility.Collapsed : Visibility.Visible;
            AutomationPropertiesHelper(ok);
            var hotkey = App.Current?.CurrentHotkeyText;
            HintText.Text = ok ? "الصقه حيث تشاء (Ctrl+V) · InstaWords" : "InstaWords · " + (hotkey ?? "");

            if (ok)
            {
                Accent.Background = (Brush)FindResource("BrandGradientBrush");
                IconBadge.Background = (Brush)FindResource("BrandSoftBrush");
                BadgeIcon.Data = (Geometry)FindResource("IconCheck");
                BadgeIcon.Foreground = (Brush)FindResource("BrandTextBrush");
            }
            else
            {
                Accent.Background = (Brush)FindResource("DangerBrush");
                IconBadge.Background = (Brush)FindResource("DangerSoftBrush");
                BadgeIcon.Data = (Geometry)FindResource("IconAlert");
                BadgeIcon.Foreground = (Brush)FindResource("DangerBrush");
            }

            _timer.Stop();
            _timer.Interval = TimeSpan.FromSeconds(ok ? 4.5 : 7);
            if (!IsVisible) Show();
            PlaceInCorner();
            BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(160)));
            _timer.Start();
        }

        private void AutomationPropertiesHelper(bool ok) =>
            System.Windows.Automation.AutomationProperties.SetName(this, (ok ? "نجاح: " : "خطأ: ") + TitleText.Text);

        private void PlaceInCorner()
        {
            // Bottom corner of the primary work area (bottom-right on a standard taskbar).
            var area = SystemParameters.WorkArea;
            Left = area.Right - ActualWidth - 8;
            Top = area.Bottom - ActualHeight - 8;
        }

        private void FadeOut()
        {
            _timer.Stop();
            var anim = new DoubleAnimation(0, TimeSpan.FromMilliseconds(260));
            anim.Completed += (s, e) =>
            {
                if (Opacity < 0.01) Hide();
            };
            BeginAnimation(OpacityProperty, anim);
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => FadeOut();

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            e.Handled = true;
        }
    }
}
