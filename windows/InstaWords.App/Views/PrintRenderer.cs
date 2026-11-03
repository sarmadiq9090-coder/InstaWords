using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using InstaWords.App.Services;

namespace InstaWords.App.Views
{
    /// <summary>What to draw: the template, its paper and calibration, and one form per page.</summary>
    public sealed class PrintJob
    {
        public PrintTemplate Template { get; set; }
        /// <summary>Cheques: paper size, font size and offsets.</summary>
        public ChequeProfile Profile { get; set; }
        /// <summary>Vouchers and strips: the paper preset.</summary>
        public PaperPreset Paper { get; set; }
        public IList<PrintForm> Pages { get; set; } = new List<PrintForm>();
        /// <summary>The tafqit output language is right-to-left (Arabic): words and cheque layout.</summary>
        public bool WordsRightToLeft { get; set; } = true;
        /// <summary>The words' language, for text shaping (ar, en, fr).</summary>
        public string WordsLanguage { get; set; } = "ar";
        /// <summary>Overlay the millimetre grid and the field boxes (calibration).</summary>
        public bool ShowGrid { get; set; }
        public string LogoPath { get; set; }

        public double WidthMm => Template == PrintTemplate.Cheque ? Profile.PaperWidthMm : Paper.WidthMm;
        public double HeightMm => Template == PrintTemplate.Cheque ? Profile.PaperHeightMm : Paper.HeightMm;
    }

    /// <summary>
    /// Draws print pages as plain WPF visuals: the same element tree is shown in the preview and
    /// put on a FixedPage for the printer, so what you see is what prints. Coordinates are physical
    /// (left-to-right canvas, millimetres converted to DIPs); text runs in its own direction.
    /// </summary>
    public static class PrintRenderer
    {
        private static readonly Brush Ink = Frozen(Color.FromRgb(0x14, 0x20, 0x0F));
        private static readonly Brush Muted = Frozen(Color.FromRgb(0x4A, 0x5A, 0x42));
        private static readonly Brush Guide = Frozen(Color.FromRgb(0xB7, 0xC7, 0xA6));
        private static readonly Brush GuideText = Frozen(Color.FromRgb(0x8A, 0x98, 0x82));
        private static readonly Brush Accent = Frozen(Color.FromRgb(0x0F, 0x76, 0x6E));
        private static readonly Brush AccentSoft = Frozen(Color.FromArgb(0x14, 0x0F, 0x76, 0x6E));
        private static readonly Brush GridMinor = Frozen(Color.FromRgb(0xE4, 0xEA, 0xF0));
        private static readonly Brush GridMid = Frozen(Color.FromRgb(0xBF, 0xCB, 0xD6));
        private static readonly Brush GridMajor = Frozen(Color.FromRgb(0x6F, 0x82, 0x95));

        private static Brush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        private static FontFamily Font => (FontFamily)Application.Current.FindResource("AppFont");

        private static double D(double mm) => Mm.ToDip(mm);

        // ---------- Pages ----------

        /// <summary>One page for one form. <paramref name="preview"/> adds faint guides that do not print.</summary>
        public static FrameworkElement RenderPage(PrintJob job, PrintForm form, bool preview)
        {
            FrameworkElement page;
            switch (job.Template)
            {
                case PrintTemplate.Cheque: page = Cheque(job, form, preview); break;
                case PrintTemplate.Strip: page = Strip(job, form); break;
                default: page = Voucher(job, form); break;
            }
            if (!job.ShowGrid) return page;
            var host = new Grid { Width = page.Width, Height = page.Height };
            host.Children.Add(page);
            host.Children.Add(CalibrationOverlay(job, labels: true));
            return host;
        }

        /// <summary>«طباعة تجريبية»: the grid, the field boxes and sample values at their calibrated positions.</summary>
        public static FrameworkElement RenderCalibration(PrintJob job, PrintForm sample)
        {
            var host = new Grid { Width = D(job.WidthMm), Height = D(job.HeightMm), Background = Brushes.White };
            host.Children.Add(CalibrationOverlay(job, labels: true));
            if (job.Template == PrintTemplate.Cheque) host.Children.Add(Cheque(job, sample, preview: false, transparent: true));
            return host;
        }

        private static Canvas NewCanvas(PrintJob job, bool transparent = false) => new Canvas
        {
            Width = D(job.WidthMm),
            Height = D(job.HeightMm),
            Background = transparent ? Brushes.Transparent : Brushes.White,
            FlowDirection = FlowDirection.LeftToRight,
            ClipToBounds = true,
        };

        // ---------- Cheque ----------

        private static FrameworkElement Cheque(PrintJob job, PrintForm form, bool preview, bool transparent = false)
        {
            var p = job.Profile;
            var rtl = job.WordsRightToLeft;
            var c = NewCanvas(job, transparent);
            var frame = p.PrintFrame;
            var guides = frame || preview;
            var guideBrush = frame ? Muted : GuideText;
            var lineBrush = frame ? Muted : Guide;
            var labelFlow = UiLanguage.Flow;

            if (guides)
            {
                // The cheque's edge (only a guide unless «print frame» is on).
                var edge = new Rectangle
                {
                    Width = D(p.PaperWidthMm - 2),
                    Height = D(p.PaperHeightMm - 2),
                    RadiusX = D(2),
                    RadiusY = D(2),
                    Stroke = frame ? Muted : Guide,
                    StrokeThickness = D(0.25),
                    StrokeDashArray = frame ? null : new DoubleCollection { 6, 4 },
                };
                Place(c, edge, D(1), D(1));
            }

            var size = Mm.PtToDip(p.FontSizePt);
            foreach (var item in PrintTemplates.Map(form))
            {
                switch (item.Field)
                {
                    case PrintField.Date:
                    {
                        var box = ChequeLayout.Box(p, PrintField.Date, rtl);
                        if (guides) Caption(c, box, Loc.T(item.LabelKey), guideBrush, labelFlow, lineBrush);
                        Text(c, box, item.Value, size, FontWeights.SemiBold, FlowDirection.LeftToRight, TextAlignment.Center);
                        break;
                    }
                    case PrintField.Party:
                    {
                        var box = ChequeLayout.Box(p, PrintField.Party, rtl);
                        if (guides) Caption(c, box, Loc.T(item.LabelKey), guideBrush, labelFlow, lineBrush);
                        var flow = TextFlow(item.Value, rtl);
                        Text(c, box, item.Value, size, FontWeights.SemiBold, flow, TextAlignment.Left);
                        break;
                    }
                    case PrintField.Words:
                    {
                        var lines = ChequeLayout.WordLines(p, rtl);
                        if (guides) Caption(c, lines[0], Loc.T(item.LabelKey), guideBrush, labelFlow, lineBrush);
                        if (guides) Underline(c, lines[1], lineBrush);
                        WordsLines(c, lines, item.Value, size, rtl, job.WordsLanguage);
                        break;
                    }
                    case PrintField.Figures:
                    {
                        var box = ChequeLayout.Box(p, PrintField.Figures, rtl);
                        if (guides)
                        {
                            var r = new Rectangle
                            {
                                Width = D(box.Width), Height = D(box.Height), RadiusX = D(1.2), RadiusY = D(1.2),
                                Stroke = lineBrush, StrokeThickness = D(0.3), Fill = frame ? null : AccentSoft,
                            };
                            Place(c, r, D(box.X), D(box.Y));
                        }
                        Text(c, box, item.Value, size * 1.1, FontWeights.Bold, FlowDirection.LeftToRight, TextAlignment.Center, center: true,
                            automationId: "PreviewFigures");
                        break;
                    }
                    case PrintField.Signature:
                    {
                        var box = ChequeLayout.Box(p, PrintField.Signature, rtl);
                        var line = new Line { X1 = D(box.X), X2 = D(box.Right), Y1 = D(box.Bottom - 3.5), Y2 = D(box.Bottom - 3.5), Stroke = frame ? Ink : lineBrush, StrokeThickness = D(0.3) };
                        c.Children.Add(line);
                        var label = Label(Loc.T(item.LabelKey), Mm.PtToDip(7), guides ? guideBrush : Muted, labelFlow);
                        label.Width = D(box.Width);
                        label.TextAlignment = TextAlignment.Center;
                        Place(c, label, D(box.X), D(box.Bottom - 3.1));
                        break;
                    }
                }
            }
            return c;
        }

        /// <summary>A small caption above a box, at its start side, with the box's base line.</summary>
        private static void Caption(Canvas c, RectMm box, string text, Brush brush, FlowDirection flow, Brush lineBrush)
        {
            var label = Label(text, Mm.PtToDip(6.5), brush, flow);
            label.Width = D(box.Width);
            label.TextAlignment = TextAlignment.Left; // start of its own direction
            Place(c, label, D(box.X), D(box.Y) - Mm.PtToDip(6.5) * 1.25);
            Underline(c, box, lineBrush);
        }

        private static void Underline(Canvas c, RectMm box, Brush brush) =>
            c.Children.Add(new Line
            {
                X1 = D(box.X), X2 = D(box.Right), Y1 = D(box.Bottom), Y2 = D(box.Bottom),
                Stroke = brush, StrokeThickness = D(0.2), StrokeDashArray = new DoubleCollection { 1, 2 },
            });

        /// <summary>The amount in words over its lines: largest size that fits, then the dashed filler.</summary>
        private static void WordsLines(Canvas c, RectMm[] lines, string words, double size, bool rtl, string language)
        {
            var flow = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            var tf = new Typeface(Font, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            var culture = CultureFor(language);
            var widths = lines.Select(l => D(l.Width)).ToList();
            var fit = WordWrap.Fit(words, widths, (t, s) => Measure(t, s, flow, tf, culture), size, Math.Max(Mm.PtToDip(7), size * 0.6), 0.5);
            for (var i = 0; i < lines.Length; i++)
            {
                var text = i < fit.Lines.Count ? fit.Lines[i] : "";
                var box = lines[i];
                if (text.Length > 0) Text(c, box, text, fit.FontSize, FontWeights.SemiBold, flow, TextAlignment.Left, automationId: "PreviewWords", culture: culture);
                var textMm = text.Length == 0 ? 0 : Mm.FromDip(Measure(text, fit.FontSize, flow, tf, culture));
                var filler = WordWrap.FillerFor(box.X, box.Width, textMm, rtl, gap: 1.5, minWidth: 3);
                if (filler == null) continue;
                var y = D(box.Bottom) - fit.FontSize * 0.55;
                c.Children.Add(new Line
                {
                    X1 = D(filler.Value.Start), X2 = D(filler.Value.End), Y1 = y, Y2 = y,
                    Stroke = Ink, StrokeThickness = D(0.35), StrokeDashArray = new DoubleCollection { 4, 2.5 },
                    Tag = "filler",
                });
            }
        }

        // ---------- Strip ----------

        private static FrameworkElement Strip(PrintJob job, PrintForm form)
        {
            var c = NewCanvas(job);
            var rtl = job.WordsRightToLeft;
            const double margin = 15;
            var width = job.WidthMm - 2 * margin;
            const double lineH = 9;
            const double figW = 42;
            var box = new RectMm(margin, margin, width, lineH * 2 + 8);
            var frame = new Rectangle
            {
                Width = D(box.Width), Height = D(box.Height), RadiusX = D(2), RadiusY = D(2),
                Stroke = Muted, StrokeThickness = D(0.3),
            };
            Place(c, frame, D(box.X), D(box.Y));
            // Two lines of words; the figures sit at the end of the second line.
            var l1 = new RectMm(margin + 4, margin + 3, width - 8, lineH);
            var l2 = new RectMm(margin + 4, margin + 3 + lineH, width - 8 - figW - 3, lineH);
            var fig = new RectMm(margin + width - 4 - figW, margin + 3 + lineH + 1, figW, lineH - 2);
            if (rtl)
            {
                l1 = l1.MirrorX(job.WidthMm);
                l2 = l2.MirrorX(job.WidthMm);
                fig = fig.MirrorX(job.WidthMm);
            }
            WordsLines(c, new[] { l1, l2 }, form.Words, Mm.PtToDip(13), rtl, job.WordsLanguage);
            var r = new Rectangle { Width = D(fig.Width), Height = D(fig.Height), RadiusX = D(1.2), RadiusY = D(1.2), Stroke = Muted, StrokeThickness = D(0.3) };
            Place(c, r, D(fig.X), D(fig.Y));
            Text(c, fig, PrintTemplates.Figures(form.Cleaned), Mm.PtToDip(12), FontWeights.Bold, FlowDirection.LeftToRight, TextAlignment.Center, center: true,
                automationId: "PreviewFigures");
            return c;
        }

        // ---------- Receipt / payment voucher ----------

        private static FrameworkElement Voucher(PrintJob job, PrintForm form)
        {
            var w = D(job.WidthMm);
            var h = D(job.HeightMm);
            var small = job.HeightMm < 160; // A5 landscape: tighter
            var root = new Border
            {
                Width = w,
                Height = h,
                Background = Brushes.White,
                Padding = new Thickness(D(small ? 10 : 14)),
                FlowDirection = UiLanguage.Flow,
            };
            TextOptions.SetTextFormattingMode(root, TextFormattingMode.Ideal);
            var frame = new Border
            {
                BorderBrush = Muted,
                BorderThickness = new Thickness(D(0.35)),
                CornerRadius = new CornerRadius(D(2.5)),
                Padding = new Thickness(D(small ? 6 : 9), D(small ? 5 : 8), D(small ? 6 : 9), D(small ? 5 : 8)),
            };
            root.Child = frame;
            var stack = new DockPanel { LastChildFill = true };
            frame.Child = stack;
            var items = PrintTemplates.Map(form);
            string V(PrintField f) => items.FirstOrDefault(i => i.Field == f)?.Value ?? "";
            string K(PrintField f) => items.FirstOrDefault(i => i.Field == f)?.LabelKey;

            var body = Mm.PtToDip(small ? 11 : 12.5);

            // Header: logo and company name at the start, the title in the middle.
            var header = new Grid { Margin = new Thickness(0, 0, 0, D(small ? 3 : 5)) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var logo = LoadLogo(job.LogoPath);
            if (logo != null)
                brand.Children.Add(new Image { Source = logo, MaxHeight = D(small ? 14 : 18), MaxWidth = D(40), Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, D(3), 0), FlowDirection = FlowDirection.LeftToRight });
            var company = V(PrintField.Company);
            if (company.Length > 0)
                brand.Children.Add(new TextBlock { Text = company, FontFamily = Font, FontSize = Mm.PtToDip(small ? 12 : 14), FontWeight = FontWeights.Bold, Foreground = Ink, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = D(70) });
            header.Children.Add(brand);
            var title = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            title.Children.Add(new TextBlock
            {
                Text = Loc.T(V(PrintField.Title)), FontFamily = Font, FontSize = Mm.PtToDip(small ? 17 : 20), FontWeight = FontWeights.Bold,
                Foreground = Accent, HorizontalAlignment = HorizontalAlignment.Center,
            });
            title.Children.Add(new Border { Height = D(0.6), Background = Accent, Margin = new Thickness(D(4), D(0.8), D(4), 0), CornerRadius = new CornerRadius(D(0.3)) });
            AutomationProps(title.Children[0], "PreviewTitle");
            Grid.SetColumn(title, 1);
            header.Children.Add(title);
            DockPanel.SetDock(header, Dock.Top);
            stack.Children.Add(header);

            // Number · amount box · date.
            var info = new Grid { Margin = new Thickness(0, D(small ? 2 : 4), 0, D(small ? 3 : 6)) };
            info.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            info.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            info.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var number = Field(Loc.T(K(PrintField.Number)), V(PrintField.Number), body, FlowDirection.LeftToRight);
            number.HorizontalAlignment = HorizontalAlignment.Left;
            number.VerticalAlignment = VerticalAlignment.Center;
            info.Children.Add(number);
            var amountBox = new Border
            {
                BorderBrush = Ink, BorderThickness = new Thickness(D(0.4)), CornerRadius = new CornerRadius(D(1.5)),
                Background = AccentSoft, Padding = new Thickness(D(4), D(1.5), D(4), D(1.5)), MinWidth = D(48),
            };
            var amountStack = new StackPanel();
            amountStack.Children.Add(new TextBlock { Text = Loc.T(K(PrintField.Figures)), FontFamily = Font, FontSize = Mm.PtToDip(7.5), Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center });
            var fig = new TextBlock { Text = V(PrintField.Figures), FontFamily = Font, FontSize = Mm.PtToDip(small ? 13 : 15), FontWeight = FontWeights.Bold, Foreground = Ink, HorizontalAlignment = HorizontalAlignment.Center, FlowDirection = FlowDirection.LeftToRight };
            AutomationProps(fig, "PreviewFigures");
            amountStack.Children.Add(fig);
            amountBox.Child = amountStack;
            Grid.SetColumn(amountBox, 1);
            info.Children.Add(amountBox);
            var date = Field(Loc.T(K(PrintField.Date)), V(PrintField.Date), body, FlowDirection.LeftToRight);
            date.HorizontalAlignment = HorizontalAlignment.Right;
            date.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(date, 2);
            info.Children.Add(date);
            DockPanel.SetDock(info, Dock.Top);
            stack.Children.Add(info);

            // Signatures at the foot.
            var signs = SignatureKeysFor(items);
            var foot = new UniformGrid { Rows = 1, Columns = Math.Max(1, signs.Count), Margin = new Thickness(0, D(small ? 3 : 6), 0, 0) };
            foreach (var key in signs)
            {
                var cell = new StackPanel { Margin = new Thickness(D(3), 0, D(3), 0) };
                cell.Children.Add(new TextBlock { Text = Loc.T(key), FontFamily = Font, FontSize = Mm.PtToDip(9.5), FontWeight = FontWeights.SemiBold, Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center });
                cell.Children.Add(new Border { Height = D(small ? 8 : 12), BorderBrush = Muted, BorderThickness = new Thickness(0, 0, 0, D(0.3)), Margin = new Thickness(D(2), 0, D(2), 0) });
                foot.Children.Add(cell);
            }
            DockPanel.SetDock(foot, Dock.Bottom);
            stack.Children.Add(foot);

            // The lines: party, the sum in words, purpose.
            var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            lines.Children.Add(Line(Loc.T(K(PrintField.Party)), V(PrintField.Party), body, TextFlow(V(PrintField.Party), Loc.IsRightToLeft), null));
            lines.Children.Add(Line(Loc.T(K(PrintField.Words)), V(PrintField.Words), body, job.WordsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight, "PreviewWords", bold: true, culture: CultureFor(job.WordsLanguage)));
            lines.Children.Add(Line(Loc.T(K(PrintField.Purpose)), V(PrintField.Purpose), body, TextFlow(V(PrintField.Purpose), Loc.IsRightToLeft), null));
            stack.Children.Add(lines);
            return root;
        }

        private static IList<string> SignatureKeysFor(IEnumerable<PrintItem> items) =>
            items.Where(i => i.Field == PrintField.Signature).Select(i => i.LabelKey).ToList();

        private static FrameworkElement Field(string label, string value, double size, FlowDirection valueFlow)
        {
            var t = new TextBlock { FontFamily = Font, FontSize = size, Foreground = Ink };
            t.Inlines.Add(new Run(label + " ") { FontWeight = FontWeights.SemiBold, Foreground = Muted });
            t.Inlines.Add(new Run(string.IsNullOrWhiteSpace(value) ? "………………" : value) { FontWeight = FontWeights.Bold, FlowDirection = valueFlow });
            return t;
        }

        /// <summary>«label: value» over a dotted line that runs to the end (like a printed form).</summary>
        private static FrameworkElement Line(string label, string value, double size, FlowDirection valueFlow, string automationId,
            bool bold = false, CultureInfo culture = null)
        {
            var row = new DockPanel { Margin = new Thickness(0, D(1.6), 0, D(1.6)), LastChildFill = true };
            var l = new TextBlock { Text = label, FontFamily = Font, FontSize = size, FontWeight = FontWeights.SemiBold, Foreground = Muted, Margin = new Thickness(0, 0, D(2), 0), VerticalAlignment = VerticalAlignment.Bottom };
            DockPanel.SetDock(l, Dock.Left);
            row.Children.Add(l);
            var valueBox = new Border { BorderBrush = Muted, BorderThickness = new Thickness(0, 0, 0, D(0.25)), Padding = new Thickness(D(1), 0, D(1), D(0.4)) };
            var v = new TextBlock
            {
                Text = value ?? "", FontFamily = Font, FontSize = size * (bold ? 1.05 : 1), FontWeight = bold ? FontWeights.Bold : FontWeights.SemiBold,
                Foreground = Ink, TextWrapping = TextWrapping.Wrap, FlowDirection = valueFlow, TextAlignment = TextAlignment.Left,
                LineHeight = size * 1.55, MinHeight = size * 1.3,
            };
            if (culture != null) v.Language = XmlLanguage.GetLanguage(culture.IetfLanguageTag);
            if (automationId != null) AutomationProps(v, automationId);
            valueBox.Child = v;
            row.Children.Add(valueBox);
            return row;
        }

        // ---------- Calibration grid ----------

        private static FrameworkElement CalibrationOverlay(PrintJob job, bool labels)
        {
            var w = job.WidthMm;
            var h = job.HeightMm;
            var c = new Canvas { Width = D(w), Height = D(h), FlowDirection = FlowDirection.LeftToRight, IsHitTestVisible = false, ClipToBounds = true };
            for (var x = 0; x <= w; x++)
            {
                var major = x % 10 == 0;
                var brush = major ? GridMajor : x % 5 == 0 ? GridMid : GridMinor;
                c.Children.Add(new Line { X1 = D(x), X2 = D(x), Y1 = 0, Y2 = D(h), Stroke = brush, StrokeThickness = D(major ? 0.2 : 0.1) });
                if (major && x > 0 && x < w - 3)
                    Place(c, Label((x / 10).ToString(CultureInfo.InvariantCulture), Mm.PtToDip(5.5), GridMajor, FlowDirection.LeftToRight), D(x + 0.5), D(0.3));
            }
            for (var y = 0; y <= h; y++)
            {
                var major = y % 10 == 0;
                var brush = major ? GridMajor : y % 5 == 0 ? GridMid : GridMinor;
                c.Children.Add(new Line { X1 = 0, X2 = D(w), Y1 = D(y), Y2 = D(y), Stroke = brush, StrokeThickness = D(major ? 0.2 : 0.1) });
                if (major && y > 0 && y < h - 3)
                    Place(c, Label((y / 10).ToString(CultureInfo.InvariantCulture), Mm.PtToDip(5.5), GridMajor, FlowDirection.LeftToRight), D(0.5), D(y + 0.3));
            }
            if (job.Template != PrintTemplate.Cheque || !labels) return c;

            // The calibrated boxes, with their names, so a test print laid over a cheque shows what to move.
            var rtl = job.WordsRightToLeft;
            void Box(RectMm r, string name)
            {
                Place(c, new Rectangle { Width = D(r.Width), Height = D(r.Height), Stroke = Accent, StrokeThickness = D(0.3), StrokeDashArray = new DoubleCollection { 3, 2 }, Fill = AccentSoft }, D(r.X), D(r.Y));
                var t = Label(name, Mm.PtToDip(6), Accent, UiLanguage.Flow);
                t.Width = D(r.Width);
                t.TextAlignment = TextAlignment.Right; // end side: the values sit at the start
                Place(c, t, D(r.X), D(r.Y) + D(0.3));
            }
            var p = job.Profile;
            Box(ChequeLayout.Box(p, PrintField.Date, rtl), Loc.T("Print.Field.Date"));
            Box(ChequeLayout.Box(p, PrintField.Party, rtl), Loc.T("Print.Field.Party"));
            Box(ChequeLayout.Box(p, PrintField.Figures, rtl), Loc.T("Print.Field.Figures"));
            foreach (var l in ChequeLayout.WordLines(p, rtl)) Box(l, Loc.T("Print.Field.Words"));
            if (p.ShowSignature) Box(ChequeLayout.Box(p, PrintField.Signature, rtl), Loc.T("Print.Field.Signature"));
            return c;
        }

        // ---------- Helpers ----------

        private static void Place(Canvas c, UIElement e, double left, double top)
        {
            Canvas.SetLeft(e, left);
            Canvas.SetTop(e, top);
            c.Children.Add(e);
        }

        private static TextBlock Label(string text, double size, Brush brush, FlowDirection flow) => new TextBlock
        {
            Text = text ?? "",
            FontFamily = Font,
            FontSize = size,
            Foreground = brush,
            FlowDirection = flow,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        /// <summary>A value in a box, sitting on the box's bottom line (or centred).</summary>
        private static void Text(Canvas c, RectMm box, string text, double size, FontWeight weight, FlowDirection flow, TextAlignment align,
            bool center = false, string automationId = null, CultureInfo culture = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            var t = new TextBlock
            {
                Text = text,
                FontFamily = Font,
                FontSize = size,
                FontWeight = weight,
                Foreground = Ink,
                FlowDirection = flow,
                TextAlignment = align,
                Width = D(box.Width),
                TextWrapping = TextWrapping.NoWrap,
            };
            if (culture != null) t.Language = XmlLanguage.GetLanguage(culture.IetfLanguageTag);
            TextOptions.SetTextFormattingMode(t, TextFormattingMode.Ideal);
            if (automationId != null) AutomationProps(t, automationId);
            var lineHeight = size * 1.32;
            var top = center ? D(box.Y) + (D(box.Height) - lineHeight) / 2 : D(box.Bottom) - lineHeight + size * 0.08;
            Place(c, t, D(box.X), top);
        }

        private static void AutomationProps(UIElement e, string id) => System.Windows.Automation.AutomationProperties.SetAutomationId(e, id);

        private static double Measure(string text, double size, FlowDirection flow, Typeface tf, CultureInfo culture)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var ft = new FormattedText(text, culture, flow, tf, size, Brushes.Black, 1.0);
            return ft.WidthIncludingTrailingWhitespace;
        }

        private static CultureInfo CultureFor(string language)
        {
            try { return CultureInfo.GetCultureInfo(string.IsNullOrEmpty(language) ? "ar" : language); }
            catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; }
        }

        /// <summary>Free text (a payee, a purpose) runs right-to-left when it starts with Arabic letters.</summary>
        private static FlowDirection TextFlow(string text, bool fallbackRtl)
        {
            foreach (var ch in text ?? "")
            {
                if (ch >= '֐' && ch <= 'ࣿ' || ch >= 'יִ' && ch <= 'ﻼ') return FlowDirection.RightToLeft;
                if (char.IsLetter(ch)) return FlowDirection.LeftToRight;
            }
            return fallbackRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        }

        /// <summary>The voucher logo (decoded once, file not kept open); null when missing or unreadable.</summary>
        public static BitmapSource LoadLogo(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                var bytes = File.ReadAllBytes(path);
                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                img.StreamSource = new MemoryStream(bytes);
                img.DecodePixelHeight = 300;
                img.EndInit();
                img.Freeze();
                return img;
            }
            catch (Exception ex)
            {
                Log.Warn("Logo: " + ex.Message);
                return null;
            }
        }

        // ---------- Printing ----------

        /// <summary>Puts rendered pages on FixedPages of the paper size, ready for PrintDialog.PrintDocument.</summary>
        public static FixedDocument BuildDocument(IEnumerable<FrameworkElement> pages, double widthMm, double heightMm)
        {
            var size = new Size(D(widthMm), D(heightMm));
            var doc = new FixedDocument();
            doc.DocumentPaginator.PageSize = size;
            foreach (var element in pages)
            {
                var fp = new FixedPage { Width = size.Width, Height = size.Height, Background = Brushes.White };
                fp.Children.Add(element);
                fp.Measure(size);
                fp.Arrange(new Rect(size));
                fp.UpdateLayout();
                var pc = new PageContent();
                ((IAddChild)pc).AddChild(fp);
                doc.Pages.Add(pc);
            }
            return doc;
        }
    }
}
