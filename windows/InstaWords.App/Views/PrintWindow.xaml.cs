using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using InstaWords.App.Models;
using InstaWords.App.Services;

namespace InstaWords.App.Views
{
    /// <summary>
    /// «طباعة»: cheque, receipt voucher, payment voucher or a words strip, with a live preview that is
    /// the very element tree sent to the printer. Cheques are calibrated per profile (paper size and
    /// per-field offsets in mm, checked with a test print of a millimetre grid). Printing goes through
    /// the Windows print dialog, so «Microsoft Print to PDF» saves a PDF.
    /// </summary>
    public partial class PrintWindow : Window
    {
        /// <summary>The preview draws at most this many pages (printing always includes all of them).</summary>
        public const int MaxPreviewPages = 30;
        /// <summary>At most this many rows from the bulk tab in one go.</summary>
        public const int MaxPages = 500;

        private readonly List<string> _amounts;
        private readonly bool _bulk;
        private readonly Dictionary<PrintField, TextBox[]> _offsetBoxes = new Dictionary<PrintField, TextBox[]>();
        private PrintTemplate _template;
        private bool _ready;
        private bool _renderQueued;
        private bool _showGrid;
        private double? _zoom; // null: fit the width
        private double _pageWidthDip = 1;
        private int _validPages;

        public PrintWindow(IList<string> amounts)
        {
            InitializeComponent();
            _amounts = (amounts ?? new List<string>()).Take(MaxPages).ToList();
            if (_amounts.Count == 0) _amounts.Add("");
            _bulk = _amounts.Count > 1;

            var print = Print;
            print.Normalize(Loc.T("Print.DefaultProfile"));
            DataContext = App.Current.State; // unit and language pickers (shared with the main window)

            AmountBox.Text = _amounts[0];
            AmountBox.Visibility = _bulk ? Visibility.Collapsed : Visibility.Visible;
            BulkInfo.Visibility = _bulk ? Visibility.Visible : Visibility.Collapsed;
            BulkInfoText.Text = Loc.F("Print.BulkInfo", Conversion.FormatCount(_amounts.Count));
            DateBox.Text = DateTime.Today.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            NumberBox.Text = print.NextNumber;
            BuildOffsetRows();

            App.Current.State.Changed += OnStateChanged;
            VoucherHeaderEditor.HeaderChanged += OnHeaderChanged;
            Closed += (s, e) =>
            {
                App.Current.State.Changed -= OnStateChanged;
                VoucherHeaderEditor.HeaderChanged -= OnHeaderChanged;
                App.Current.SaveSettingsSoon();
            };
            InputBindings.Add(new KeyBinding(new RelayCommand(Print_), Key.P, ModifierKeys.Control));

            _ready = true;
            SelectTemplate(print.Template);
            Loaded += (s, e) =>
            {
                if (_bulk) PrintNow.Focus();
                else
                {
                    AmountBox.Focus();
                    AmountBox.CaretIndex = AmountBox.Text.Length;
                }
            };
        }

        private static PrintSettings Print => App.Current.Settings.Print;
        private ChequeProfile Profile => Print.CurrentProfile;

        // ---------- Template ----------

        private void SelectTemplate(PrintTemplate t)
        {
            var radio = t == PrintTemplate.Receipt ? TplReceipt : t == PrintTemplate.Payment ? TplPayment : t == PrintTemplate.Strip ? TplStrip : TplCheque;
            if (radio.IsChecked == true) ApplyTemplate(t);
            else radio.IsChecked = true; // → OnTemplateChecked
        }

        private void OnTemplateChecked(object sender, RoutedEventArgs e)
        {
            if (!_ready) return;
            if (Enum.TryParse((string)((FrameworkElement)sender).Tag, out PrintTemplate t)) ApplyTemplate(t);
        }

        private void ApplyTemplate(PrintTemplate t)
        {
            _template = t;
            Print.Template = t;
            App.Current.SaveSettingsSoon();
            var cheque = t == PrintTemplate.Cheque;
            var voucher = PrintTemplates.IsVoucher(t);

            TemplateHint.Text = Loc.T("Print.Hint." + t);
            HeaderEditor.Visibility = voucher ? Visibility.Visible : Visibility.Collapsed;
            NumberPanel.Visibility = voucher ? Visibility.Visible : Visibility.Collapsed;
            NumberColumn.Width = voucher ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            NumberGap.Width = new GridLength(voucher ? 12 : 0);
            DetailsCard.Visibility = t == PrintTemplate.Strip ? Visibility.Collapsed : Visibility.Visible; // a strip is just the words
            PartyLabel.Text = Loc.T(PrintTemplates.PartyKey(t));
            AutomationPropertiesName(PartyBox, PartyLabel.Text);
            PartyLabel.Visibility = PartyBox.Visibility = t == PrintTemplate.Strip ? Visibility.Collapsed : Visibility.Visible;
            PurposePanel.Visibility = voucher ? Visibility.Visible : Visibility.Collapsed;
            PurposeLabel.Text = Loc.T(t == PrintTemplate.Payment ? "Print.PurposePayment" : "Print.PurposeReceipt");
            AutomationPropertiesName(PurposeBox, PurposeLabel.Text);
            SignatureBox.Visibility = cheque ? Visibility.Visible : Visibility.Collapsed;
            ProfilePanel.Visibility = cheque ? Visibility.Visible : Visibility.Collapsed;
            CalibrationPanel.Visibility = cheque ? Visibility.Visible : Visibility.Collapsed;
            TestPrint.Visibility = cheque ? Visibility.Visible : Visibility.Collapsed;
            SizePanel.Visibility = cheque ? Visibility.Visible : Visibility.Collapsed;

            FillPapers();
            if (cheque) ShowProfile();
            Render();
        }

        private static void AutomationPropertiesName(DependencyObject o, string name) => System.Windows.Automation.AutomationProperties.SetName(o, name);

        // ---------- Paper and profiles ----------

        /// <summary>A paper preset as listed in the paper box (public: WPF binds to its Label).</summary>
        public sealed class PaperChoice
        {
            public PaperChoice(PaperPreset p) => Preset = p;
            public PaperPreset Preset { get; }
            public string Label => Preset.IsCustom
                ? Loc.T(Preset.LabelKey)
                : Loc.T(Preset.LabelKey) + " · " + Mm.Format(Preset.WidthMm) + " × " + Mm.Format(Preset.HeightMm) + " mm";
        }

        private bool _fillingPapers;

        private void FillPapers()
        {
            _fillingPapers = true;
            var choices = Papers.For(_template).Select(p => new PaperChoice(p)).ToList();
            PaperCombo.ItemsSource = choices;
            string id;
            if (_template == PrintTemplate.Cheque) id = Papers.Match(Profile.PaperWidthMm, Profile.PaperHeightMm).Id;
            else id = _template == PrintTemplate.Strip ? Print.StripPaper : Print.VoucherPaper;
            PaperCombo.SelectedItem = choices.FirstOrDefault(c => c.Preset.Id == id) ?? choices[0];
            _fillingPapers = false;
            UpdateSizeBoxes();
        }

        private void OnPaperSelected(object sender, SelectionChangedEventArgs e)
        {
            if (!_ready || _fillingPapers || !(PaperCombo.SelectedItem is PaperChoice c)) return;
            if (_template == PrintTemplate.Cheque)
            {
                if (!c.Preset.IsCustom)
                {
                    Profile.PaperWidthMm = c.Preset.WidthMm;
                    Profile.PaperHeightMm = c.Preset.HeightMm;
                }
            }
            else if (_template == PrintTemplate.Strip) Print.StripPaper = c.Preset.Id;
            else Print.VoucherPaper = c.Preset.Id;
            App.Current.SaveSettingsSoon();
            UpdateSizeBoxes();
            Render();
        }

        private void UpdateSizeBoxes()
        {
            if (_template != PrintTemplate.Cheque) return;
            var custom = (PaperCombo.SelectedItem as PaperChoice)?.Preset.IsCustom == true;
            WidthBox.IsEnabled = HeightBox.IsEnabled = custom;
            WidthBox.Text = Mm.Format(Profile.PaperWidthMm);
            HeightBox.Text = Mm.Format(Profile.PaperHeightMm);
            FontBox.Text = Mm.Format(Profile.FontSizePt);
        }

        private void OnSizeCommitted(object sender, KeyboardFocusChangedEventArgs e) => CommitSizes();

        private void CommitSizes()
        {
            if (_template != PrintTemplate.Cheque) return;
            var p = Profile;
            if (WidthBox.IsEnabled && Mm.TryParse(WidthBox.Text, out var w)) p.PaperWidthMm = Papers.ClampSide(w);
            if (HeightBox.IsEnabled && Mm.TryParse(HeightBox.Text, out var h)) p.PaperHeightMm = Papers.ClampSide(h);
            if (Mm.TryParse(FontBox.Text, out var f)) p.FontSizePt = Math.Max(ChequeProfile.MinFontPt, Math.Min(ChequeProfile.MaxFontPt, f));
            UpdateSizeBoxes();
            App.Current.SaveSettingsSoon();
            Render();
        }

        private void OnNumberKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            CommitSizes();
            e.Handled = true;
        }

        private bool _fillingProfiles;

        private void ShowProfile()
        {
            _fillingProfiles = true;
            ProfileCombo.ItemsSource = null;
            ProfileCombo.ItemsSource = Print.Profiles;
            ProfileCombo.SelectedItem = Profile;
            _fillingProfiles = false;
            ProfileName.Text = Profile.Name;
            DeleteProfileButton.IsEnabled = Print.Profiles.Count > 1;
            SignatureBox.IsChecked = Profile.ShowSignature;
            FrameBox.IsChecked = Profile.PrintFrame;
            GridBox.IsChecked = _showGrid;
            FillPapers();
            ShowOffsets();
        }

        private void OnProfileSelected(object sender, SelectionChangedEventArgs e)
        {
            if (!_ready || _fillingProfiles || !(ProfileCombo.SelectedItem is ChequeProfile p)) return;
            Print.LastProfileId = p.Id;
            App.Current.SaveSettingsSoon();
            ShowProfile();
            Render();
        }

        private void OnNewProfile(object sender, RoutedEventArgs e)
        {
            var copy = Profile.Clone(Loc.F("Print.ProfileNumbered", Print.Profiles.Count + 1));
            Print.Profiles.Add(copy);
            Print.LastProfileId = copy.Id;
            App.Current.SaveSettingsSoon();
            ShowProfile();
            ProfileName.Focus();
            ProfileName.SelectAll();
            Render();
        }

        private void OnDeleteProfile(object sender, RoutedEventArgs e)
        {
            if (Print.Profiles.Count <= 1) return;
            Print.Profiles.Remove(Profile);
            Print.LastProfileId = Print.Profiles[0].Id;
            App.Current.SaveSettingsSoon();
            ShowProfile();
            Render();
        }

        private void OnProfileNameCommitted(object sender, KeyboardFocusChangedEventArgs e) => CommitProfileName();

        private void OnProfileNameKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            CommitProfileName();
            e.Handled = true;
        }

        private void CommitProfileName()
        {
            var name = (ProfileName.Text ?? "").Trim();
            if (name.Length == 0 || name == Profile.Name) { ProfileName.Text = Profile.Name; return; }
            Profile.Name = name;
            App.Current.SaveSettingsSoon();
            ShowProfile();
        }

        private void OnProfileOptionClick(object sender, RoutedEventArgs e)
        {
            Profile.ShowSignature = SignatureBox.IsChecked == true;
            Profile.PrintFrame = FrameBox.IsChecked == true;
            App.Current.SaveSettingsSoon();
            Render();
        }

        private void OnGridClick(object sender, RoutedEventArgs e)
        {
            if (!_ready || _showGrid == (GridBox.IsChecked == true)) return;
            _showGrid = GridBox.IsChecked == true;
            Render();
        }

        // ---------- Offsets (calibration) ----------

        private void BuildOffsetRows()
        {
            var head = 0;
            OffsetsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddCell(new TextBlock { Text = "X (mm)", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("MutedBrush"), HorizontalAlignment = HorizontalAlignment.Center, ToolTip = Loc.T("Print.OffsetXTip") }, head, 1);
            AddCell(new TextBlock { Text = "Y (mm)", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("MutedBrush"), HorizontalAlignment = HorizontalAlignment.Center, ToolTip = Loc.T("Print.OffsetYTip") }, head, 3);
            foreach (var f in PrintTemplates.Calibrated)
            {
                var row = OffsetsGrid.RowDefinitions.Count;
                OffsetsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var label = new TextBlock
                {
                    Text = Loc.T("Print.Field." + f), VerticalAlignment = VerticalAlignment.Center, FontSize = 13.5,
                    FontWeight = f == PrintField.All ? FontWeights.Bold : FontWeights.Normal, TextTrimming = TextTrimming.CharacterEllipsis,
                };
                AddCell(label, row, 0);
                var x = OffsetBox(f, "X");
                var y = OffsetBox(f, "Y");
                AddCell(x, row, 1);
                AddCell(y, row, 3);
                _offsetBoxes[f] = new[] { x, y };
            }
        }

        private void AddCell(UIElement e, int row, int col)
        {
            Grid.SetRow(e, row);
            Grid.SetColumn(e, col);
            if (e is FrameworkElement fe && row > 0) fe.Margin = new Thickness(0, 3, 0, 3);
            OffsetsGrid.Children.Add(e);
        }

        private TextBox OffsetBox(PrintField f, string axis)
        {
            var box = new TextBox { FlowDirection = FlowDirection.LeftToRight, MinHeight = 34, Padding = new Thickness(8, 4, 8, 4), HorizontalContentAlignment = HorizontalAlignment.Center, Tag = f };
            System.Windows.Automation.AutomationProperties.SetAutomationId(box, "PrintOffset" + f + axis);
            AutomationPropertiesName(box, Loc.T("Print.Field." + f) + " " + axis + " (mm)");
            box.ToolTip = Loc.T("Print.OffsetKeysTip");
            box.LostKeyboardFocus += (s, e) => CommitOffsets(f);
            box.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { CommitOffsets(f); e.Handled = true; }
                else if (e.Key == Key.Up || e.Key == Key.Down)
                {
                    // Arrows nudge by 0.5 mm (Shift: 5 mm).
                    var step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 5 : 0.5;
                    Mm.TryParse(box.Text, out var v);
                    box.Text = Mm.Format(v + (e.Key == Key.Up ? step : -step));
                    CommitOffsets(f);
                    e.Handled = true;
                }
            };
            return box;
        }

        private void ShowOffsets()
        {
            foreach (var kv in _offsetBoxes)
            {
                Profile.GetOffset(kv.Key, out var x, out var y);
                kv.Value[0].Text = Mm.Format(x);
                kv.Value[1].Text = Mm.Format(y);
            }
        }

        private void CommitOffsets(PrintField f)
        {
            if (!_offsetBoxes.TryGetValue(f, out var boxes)) return;
            Profile.GetOffset(f, out var x, out var y);
            if (Mm.TryParse(boxes[0].Text, out var nx)) x = Math.Max(-200, Math.Min(200, nx));
            if (Mm.TryParse(boxes[1].Text, out var ny)) y = Math.Max(-200, Math.Min(200, ny));
            Profile.SetOffset(f, x, y);
            boxes[0].Text = Mm.Format(x);
            boxes[1].Text = Mm.Format(y);
            App.Current.SaveSettingsSoon();
            Render();
        }

        private void OnResetOffsets(object sender, RoutedEventArgs e)
        {
            Profile.ResetOffsets();
            App.Current.SaveSettingsSoon();
            ShowOffsets();
            Render();
        }

        // ---------- Job ----------

        private void OnFieldChanged(object sender, TextChangedEventArgs e) => Render();
        private void OnStateChanged(object sender, EventArgs e) => Render();
        private void OnHeaderChanged(object sender, EventArgs e) => Render();

        /// <summary>The pages to print, from the dialog's fields and the current tafqit options.</summary>
        private PrintJob BuildJob(out string firstWords, out string firstError)
        {
            var state = App.Current.State;
            var options = state.ToOptions();
            var unit = state.UnitItem;
            var unitText = unit == null || unit.Id == "none" ? "" : unit.Code ?? unit.Label;
            var job = new PrintJob
            {
                Template = _template,
                Profile = Profile,
                Paper = Papers.Find(_template, _template == PrintTemplate.Strip ? Print.StripPaper : Print.VoucherPaper),
                WordsRightToLeft = Catalog.FindLanguage(options.Language)?.Info.RightToLeft ?? true,
                WordsLanguage = options.Language,
                ShowGrid = _showGrid,
                LogoPath = Print.LogoPath,
            };
            firstWords = null;
            firstError = null;
            var inputs = _bulk ? _amounts : new List<string> { AmountBox.Text ?? "" };
            var number = (NumberBox.Text ?? "").Trim();
            foreach (var input in inputs)
            {
                var r = input.Trim().Length == 0 ? null : Conversion.Run(input, options);
                if (r != null && !r.Ok)
                {
                    if (firstError == null) firstError = r.ErrorMessage;
                    continue;
                }
                if (r != null && firstWords == null) firstWords = r.Text;
                job.Pages.Add(new PrintForm
                {
                    Template = _template,
                    Cleaned = r?.Cleaned ?? "",
                    Words = r?.Text ?? "",
                    UnitText = unitText,
                    Date = (DateBox.Text ?? "").Trim(),
                    Party = (PartyBox.Text ?? "").Trim(),
                    Purpose = (PurposeBox.Text ?? "").Trim(),
                    Number = number,
                    Company = Print.CompanyName ?? "",
                    ShowSignature = Profile.ShowSignature,
                });
                number = PrintSettings.Increment(number);
            }
            return job;
        }

        /// <summary>Re-draws the preview once the current input has been handled (typing stays smooth).</summary>
        private void Render()
        {
            if (!_ready || _renderQueued) return;
            _renderQueued = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _renderQueued = false;
                RenderNow();
            }), DispatcherPriority.Background);
        }

        private void RenderNow()
        {
            var job = BuildJob(out var words, out var error);
            _validPages = job.Pages.Count(p => p.Cleaned.Length > 0);

            WordsText.Text = words ?? "";
            WordsText.FlowDirection = job.WordsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            WordsText.Visibility = string.IsNullOrEmpty(words) ? Visibility.Collapsed : Visibility.Visible;
            var message = error ?? (string.IsNullOrEmpty(words) ? Loc.T("Print.NoAmount") : null);
            WordsError.Text = message ?? "";
            WordsError.Foreground = (Brush)FindResource(error != null ? "DangerBrush" : "MutedBrush");
            WordsError.Visibility = message == null ? Visibility.Collapsed : Visibility.Visible;
            Ui.SetHasError(AmountBox, error != null && !_bulk);
            PrintNow.IsEnabled = _validPages > 0;

            // A page with empty values still shows the layout while nothing valid is typed.
            if (job.Pages.Count == 0) job.Pages.Add(new PrintForm { Template = _template, Date = DateBox.Text, Party = PartyBox.Text, Company = Print.CompanyName, ShowSignature = Profile.ShowSignature });

            Pages.Children.Clear();
            _pageWidthDip = Mm.ToDip(job.WidthMm);
            var scale = CurrentScale();
            foreach (var form in job.Pages.Take(MaxPreviewPages))
            {
                var page = PrintRenderer.RenderPage(job, form, preview: true);
                var paper = new Border
                {
                    Background = Brushes.White,
                    BorderBrush = (Brush)FindResource("BorderStrongBrush"),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 0, 18),
                    Effect = (System.Windows.Media.Effects.Effect)FindResource("SoftShadow"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Child = page,
                    LayoutTransform = new ScaleTransform(scale, scale),
                };
                Pages.Children.Add(paper);
            }
            if (job.Pages.Count > MaxPreviewPages)
                Pages.Children.Add(new TextBlock
                {
                    Text = Loc.F("Print.MorePages", Conversion.FormatCount(job.Pages.Count - MaxPreviewPages)),
                    Style = (Style)FindResource("HintText"), HorizontalAlignment = HorizontalAlignment.Center, FontSize = 13,
                    FlowDirection = UiLanguage.Flow,
                });
            var count = _validPages;
            PageInfo.Text = count == 1 ? Loc.T("Print.PageOne") : Loc.F("Print.PageMany", Conversion.FormatCount(count));
            ZoomText.Text = Math.Round(scale * 100).ToString(CultureInfo.InvariantCulture) + "%";
        }

        // ---------- Zoom ----------

        private double FitScale()
        {
            var available = PreviewScroll.ActualWidth - 48 - SystemParameters.VerticalScrollBarWidth;
            if (available <= 0 || _pageWidthDip <= 0) return 1;
            return Math.Max(0.25, Math.Min(2.5, available / _pageWidthDip));
        }

        private double CurrentScale() => _zoom ?? FitScale();

        private void SetZoom(double? zoom)
        {
            _zoom = zoom.HasValue ? Math.Max(0.25, Math.Min(4, zoom.Value)) : (double?)null;
            var s = CurrentScale();
            foreach (var paper in Pages.Children.OfType<Border>()) paper.LayoutTransform = new ScaleTransform(s, s);
            ZoomText.Text = Math.Round(s * 100).ToString(CultureInfo.InvariantCulture) + "%";
        }

        private void OnZoomIn(object sender, RoutedEventArgs e) => SetZoom(CurrentScale() * 1.25);
        private void OnZoomOut(object sender, RoutedEventArgs e) => SetZoom(CurrentScale() / 1.25);
        private void OnZoomFit(object sender, RoutedEventArgs e) => SetZoom(null);

        private void OnPreviewSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_zoom == null) SetZoom(null);
        }

        // ---------- Printing ----------

        private void OnPrintClick(object sender, RoutedEventArgs e) => Print_();

        private void Print_()
        {
            CommitPending();
            var job = BuildJob(out _, out _);
            job.ShowGrid = false; // the grid is a preview aid; «طباعة تجريبية» prints it
            var forms = job.Pages.Where(p => p.Cleaned.Length > 0).ToList();
            if (forms.Count == 0)
            {
                SetStatus(Loc.T("Print.NoAmount"), ok: false);
                return;
            }
            var title = "InstaWords · " + Loc.T(PrintTemplates.NameKey(_template));
            if (!Send(job, () => forms.Select(f => PrintRenderer.RenderPage(job, f, preview: false)), title)) return;
            if (PrintTemplates.IsVoucher(_template))
            {
                // Next time starts after the last printed number.
                var next = (NumberBox.Text ?? "").Trim();
                for (var i = 0; i < forms.Count; i++) next = PrintSettings.Increment(next);
                Print.NextNumber = next;
                NumberBox.Text = next;
            }
            App.Current.SaveSettingsSoon();
            SetStatus(forms.Count == 1 ? Loc.T("Print.SentOne") : Loc.F("Print.SentMany", Conversion.FormatCount(forms.Count)), ok: true);
        }

        private void OnTestPrintClick(object sender, RoutedEventArgs e)
        {
            CommitPending();
            var job = BuildJob(out _, out _);
            var sample = job.Pages.FirstOrDefault(p => p.Cleaned.Length > 0) ?? new PrintForm
            {
                Template = PrintTemplate.Cheque,
                Cleaned = "1250.75",
                Words = Conversion.Run("1250.75", App.Current.State.ToOptions()).Text,
                Date = DateBox.Text,
                Party = string.IsNullOrWhiteSpace(PartyBox.Text) ? Loc.T("Print.SamplePayee") : PartyBox.Text,
                ShowSignature = Profile.ShowSignature,
            };
            if (Send(job, () => new[] { PrintRenderer.RenderCalibration(job, sample) }, "InstaWords · " + Loc.T("Print.TestPrint")))
                SetStatus(Loc.T("Print.TestSent"), ok: true);
        }

        /// <summary>Commits a size or offset still being typed (its LostFocus has not fired yet).</summary>
        private void CommitPending()
        {
            if (Keyboard.FocusedElement is TextBox tb)
            {
                if (tb == WidthBox || tb == HeightBox || tb == FontBox) CommitSizes();
                else if (tb == ProfileName) CommitProfileName();
                else if (tb.Tag is PrintField f) CommitOffsets(f);
            }
        }

        /// <summary>The Windows print dialog, then the document. False when cancelled or failed.</summary>
        private bool Send(PrintJob job, Func<IEnumerable<FrameworkElement>> pages, string title)
        {
            try
            {
                var dlg = new PrintDialog { UserPageRangeEnabled = false };
                TrySetPaper(dlg, job);
                if (dlg.ShowDialog() != true) return false;
                var doc = PrintRenderer.BuildDocument(pages(), job.WidthMm, job.HeightMm);
                dlg.PrintDocument(doc.DocumentPaginator, title);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("Print: " + ex);
                SetStatus(Loc.F("Print.Failed", ex.Message), ok: false);
                return false;
            }
        }

        /// <summary>Asks the printer for the page's paper and orientation (drivers may ignore it).</summary>
        private static void TrySetPaper(PrintDialog dlg, PrintJob job)
        {
            try
            {
                var ticket = dlg.PrintTicket;
                var landscape = job.WidthMm > job.HeightMm;
                if (job.Template != PrintTemplate.Cheque)
                {
                    var a4 = Math.Max(job.WidthMm, job.HeightMm) > 250;
                    ticket.PageMediaSize = a4
                        ? new PageMediaSize(PageMediaSizeName.ISOA4, Mm.ToDip(210), Mm.ToDip(297))
                        : new PageMediaSize(PageMediaSizeName.ISOA5, Mm.ToDip(148), Mm.ToDip(210));
                }
                ticket.PageOrientation = landscape && job.Template != PrintTemplate.Cheque ? PageOrientation.Landscape : PageOrientation.Portrait;
            }
            catch (Exception ex)
            {
                Log.Warn("Print ticket: " + ex.Message);
            }
        }

        private void SetStatus(string text, bool ok)
        {
            Status.Text = text;
            Status.Foreground = (Brush)FindResource(ok ? "OkBrush" : "DangerBrush");
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
