using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using InstaWords.App.Models;
using InstaWords.App.Services;

namespace InstaWords.App.Views
{
    /// <summary>«تفقيط رقم»: live conversion of one amount, copy, formatting options and history.</summary>
    public partial class SinglePage : UserControl
    {
        private readonly DispatcherTimer _historyTimer;
        private readonly DispatcherTimer _copiedTimer;
        private ConversionResult _last;
        private string _lastInput = "";

        public SinglePage()
        {
            InitializeComponent();
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this)) return;

            var app = App.Current;
            DataContext = app.State;
            app.State.Changed += (s, e) => { Convert(); UpdateOptionsSummary(); };

            HistoryList.ItemsSource = app.History.Items;
            app.History.Items.CollectionChanged += OnHistoryChanged;
            OnHistoryChanged(null, null);

            OptionsExpander.IsExpanded = app.Settings.OptionsExpanded;
            UpdateOptionsSummary();

            // A result the user pauses on for a moment is remembered, without flooding the list while typing.
            _historyTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            _historyTimer.Tick += (s, e) => { _historyTimer.Stop(); Remember(); };
            _copiedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.8) };
            _copiedTimer.Tick += (s, e) => { _copiedTimer.Stop(); ResetCopyButton(); };

            KeyDown += OnPageKeyDown;
            Loc.Changed += (s, e) => { Convert(); UpdateOptionsSummary(); };
            SizeChanged += (s, e) => ApplyLayout(e.NewSize.Width);
            Convert();
        }

        public void FocusInput()
        {
            NumberBox.Focus();
            Keyboard.Focus(NumberBox);
            NumberBox.SelectAll();
        }

        private void ApplyLayout(double width)
        {
            var wide = width >= 900;
            HistoryCard.Visibility = wide ? Visibility.Visible : Visibility.Collapsed;
            HistoryColumn.Width = wide ? new GridLength(width >= 1180 ? 360 : 300) : new GridLength(0);
            GapColumn.Width = new GridLength(wide ? 20 : 0);
        }

        private void OnNumberChanged(object sender, TextChangedEventArgs e)
        {
            ClearButton.Visibility = NumberBox.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            Convert();
        }

        private void Convert()
        {
            var input = NumberBox.Text ?? "";
            CopyStatus.Text = "";
            ResetCopyButton();
            _historyTimer.Stop();

            if (input.Trim().Length == 0)
            {
                _last = null;
                ShowState(empty: true);
                return;
            }

            var r = Conversion.Run(input, App.Current.State.ToOptions());
            _last = r;
            _lastInput = input;
            if (r.Ok)
            {
                ResultText.Text = r.Text;
                CleanedText.Text = Conversion.Grouped(r.Cleaned);
                ShowState(empty: false, ok: true);
                _historyTimer.Start();
            }
            else
            {
                ErrorText.Text = r.ErrorMessage ?? Loc.T("Err.Generic");
                App.Current.LastErrorCode = r.ErrorCode;
                ShowState(empty: false, ok: false);
            }
        }

        private void ShowState(bool empty, bool ok = false)
        {
            EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            ResultText.Visibility = !empty && ok ? Visibility.Visible : Visibility.Collapsed;
            ErrorBox.Visibility = !empty && !ok ? Visibility.Visible : Visibility.Collapsed;
            CleanedPill.Visibility = !empty && ok && !string.IsNullOrEmpty(CleanedText.Text) ? Visibility.Visible : Visibility.Collapsed;
            CopyButton.IsEnabled = !empty && ok;
            Ui.SetHasError(NumberBox, !empty && !ok);
            if (empty) ResultText.Text = "";
        }

        private void Remember()
        {
            if (_last == null || !_last.Ok) return;
            var o = App.Current.State;
            App.Current.History.Add(_lastInput, _last.Cleaned, o.Unit, o.Language, _last.Text);
        }

        public void CopyResult()
        {
            if (_last == null || !_last.Ok) return;
            if (ClipboardService.TrySetText(_last.Text))
            {
                _historyTimer.Stop();
                Remember();
                CopyButton.SetResourceReference(ContentProperty, "Single.Copied");
                Ui.SetIcon(CopyButton, (Geometry)FindResource("IconCheck"));
                CopyStatus.Foreground = (Brush)FindResource("OkBrush");
                CopyStatus.Text = Loc.T("Single.CopiedStatus");
                _copiedTimer.Stop();
                _copiedTimer.Start();
            }
            else
            {
                CopyStatus.Foreground = (Brush)FindResource("DangerBrush");
                CopyStatus.Text = Loc.T("Single.CopyFailed");
            }
        }

        private void ResetCopyButton()
        {
            CopyButton.SetResourceReference(ContentProperty, "Single.Copy");
            Ui.SetIcon(CopyButton, (Geometry)FindResource("IconCopy"));
        }

        public void ClearInput()
        {
            NumberBox.Clear();
            FocusInput();
        }

        private void OnPageKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Handled) return;
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                CopyResult();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
            {
                ClearInput();
                e.Handled = true;
            }
        }

        private void OnNumberKeyDown(object sender, KeyEventArgs e)
        {
            // Plain Enter in the number box: remember the result right away.
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                _historyTimer.Stop();
                Remember();
                e.Handled = true;
            }
        }

        private void OnCopyClick(object sender, RoutedEventArgs e) => CopyResult();
        private void OnPrintClick(object sender, RoutedEventArgs e) => Print();

        /// <summary>«طباعة» (Ctrl+P): the print dialog with the amount typed here (or empty).</summary>
        public void Print()
        {
            _historyTimer.Stop();
            Remember();
            App.Current.AppWindow?.ShowPrint(new[] { NumberBox.Text ?? "" });
        }
        private void OnClearClick(object sender, RoutedEventArgs e) => ClearInput();

        private void OnSampleClick(object sender, RoutedEventArgs e)
        {
            NumberBox.Text = (string)((FrameworkElement)sender).Tag;
            NumberBox.CaretIndex = NumberBox.Text.Length;
            NumberBox.Focus();
        }

        private void OnOptionsToggled(object sender, RoutedEventArgs e)
        {
            App.Current.Settings.OptionsExpanded = OptionsExpander.IsExpanded;
            App.Current.SaveSettingsSoon();
            UpdateOptionsSummary();
        }

        private void UpdateOptionsSummary()
        {
            var s = App.Current.State;
            var parts = new System.Collections.Generic.List<string>();
            if (s.SupportsHundredsForm) parts.Add(s.HundredsForm);
            parts.Add(Loc.T(s.Decimals == "error" ? "Options.Summary.Error" : "Options.Summary.Round"));
            var affix = (s.Prefix.Trim() + " … " + s.Suffix.Trim()).Trim();
            if (s.Prefix.Trim().Length > 0 || s.Suffix.Trim().Length > 0) parts.Add(affix.Length > 40 ? affix.Substring(0, 40) + "…" : affix);
            OptionsSummary.Text = OptionsExpander.IsExpanded ? "" : string.Join(" · ", parts);
        }

        private void OnHistoryChanged(object sender, NotifyCollectionChangedEventArgs e) =>
            HistoryEmpty.Visibility = App.Current.History.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        private void OnHistoryClick(object sender, MouseButtonEventArgs e)
        {
            var item = ItemsControl.ContainerFromElement(HistoryList, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item?.DataContext is HistoryEntry entry) Reuse(entry);
        }

        private void OnHistoryKeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.Enter || e.Key == Key.Space) && HistoryList.SelectedItem is HistoryEntry entry)
            {
                Reuse(entry);
                e.Handled = true;
            }
        }

        private void Reuse(HistoryEntry entry)
        {
            var s = App.Current.State;
            if (!string.IsNullOrEmpty(entry.Language)) s.Language = entry.Language;
            if (!string.IsNullOrEmpty(entry.Unit)) s.Unit = entry.Unit;
            NumberBox.Text = entry.Input ?? entry.Cleaned ?? "";
            NumberBox.CaretIndex = NumberBox.Text.Length;
            NumberBox.Focus();
        }

        private void OnClearHistoryClick(object sender, RoutedEventArgs e) => App.Current.History.Clear();
    }
}
