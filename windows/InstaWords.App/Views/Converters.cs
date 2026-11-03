using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace InstaWords.App.Views
{
    /// <summary>RadioButton ↔ string option: checked when the value equals the parameter.</summary>
    public sealed class EqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            string.Equals(value as string, parameter as string, StringComparison.Ordinal);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is bool b && b ? parameter : Binding.DoNothing;
    }

    public sealed class BoolToVisibleConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            (value is bool b && b) != Invert ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
