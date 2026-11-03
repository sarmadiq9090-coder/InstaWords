using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace InstaWords.App.Views
{
    /// <summary>Attached properties the control templates in Themes/Controls.xaml read.</summary>
    public static class Ui
    {
        public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.RegisterAttached(
            "CornerRadius", typeof(CornerRadius), typeof(Ui),
            new FrameworkPropertyMetadata(new CornerRadius(10), FrameworkPropertyMetadataOptions.Inherits));
        public static CornerRadius GetCornerRadius(DependencyObject o) => (CornerRadius)o.GetValue(CornerRadiusProperty);
        public static void SetCornerRadius(DependencyObject o, CornerRadius v) => o.SetValue(CornerRadiusProperty, v);

        /// <summary>Grey hint text shown while a TextBox is empty.</summary>
        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.RegisterAttached(
            "Placeholder", typeof(string), typeof(Ui), new PropertyMetadata(null));
        public static string GetPlaceholder(DependencyObject o) => (string)o.GetValue(PlaceholderProperty);
        public static void SetPlaceholder(DependencyObject o, string v) => o.SetValue(PlaceholderProperty, v);

        /// <summary>Line icon shown before a button's content.</summary>
        public static readonly DependencyProperty IconProperty = DependencyProperty.RegisterAttached(
            "Icon", typeof(Geometry), typeof(Ui), new PropertyMetadata(null));
        public static Geometry GetIcon(DependencyObject o) => (Geometry)o.GetValue(IconProperty);
        public static void SetIcon(DependencyObject o, Geometry v) => o.SetValue(IconProperty, v);

        /// <summary>
        /// The button's icon implies a reading direction (text lines, a list): it mirrors with the
        /// interface (right-to-left in Arabic). Other icons keep their drawn orientation.
        /// </summary>
        public static readonly DependencyProperty MirrorIconProperty = DependencyProperty.RegisterAttached(
            "MirrorIcon", typeof(bool), typeof(Ui), new PropertyMetadata(false));
        public static bool GetMirrorIcon(DependencyObject o) => (bool)o.GetValue(MirrorIconProperty);
        public static void SetMirrorIcon(DependencyObject o, bool v) => o.SetValue(MirrorIconProperty, v);

        /// <summary>Error state for inputs: red border and ring.</summary>
        public static readonly DependencyProperty HasErrorProperty = DependencyProperty.RegisterAttached(
            "HasError", typeof(bool), typeof(Ui), new PropertyMetadata(false));
        public static bool GetHasError(DependencyObject o) => (bool)o.GetValue(HasErrorProperty);
        public static void SetHasError(DependencyObject o, bool v) => o.SetValue(HasErrorProperty, v);
    }

    /// <summary>
    /// A 24×24 line icon (stroked geometry) that scales with its size. It keeps its drawn orientation in
    /// right-to-left layouts unless its FlowDirection is set (icons that imply a reading direction).
    /// </summary>
    public sealed class Icon : Control
    {
        static Icon()
        {
            FocusableProperty.OverrideMetadata(typeof(Icon), new FrameworkPropertyMetadata(false));
            IsTabStopProperty.OverrideMetadata(typeof(Icon), new FrameworkPropertyMetadata(false));
        }

        public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
            "Data", typeof(Geometry), typeof(Icon), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public Geometry Data { get => (Geometry)GetValue(DataProperty); set => SetValue(DataProperty, value); }

        public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
            "StrokeThickness", typeof(double), typeof(Icon), new FrameworkPropertyMetadata(2.0));
        public double StrokeThickness { get => (double)GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    }
}
