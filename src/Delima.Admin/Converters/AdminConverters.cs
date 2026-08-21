using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Delima.Admin.Models;

namespace Delima.Admin.Converters;

public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is bool flag && flag;
        if (Invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class EqualityToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool equals = string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);
        if (Invert) equals = !equals;
        return equals ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class NullOrEmptyToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool empty = value == null || string.IsNullOrWhiteSpace(value.ToString());
        if (Invert) empty = !empty;
        return empty ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(color);
            }
            catch
            {
                return Brushes.Transparent;
            }
        }
        return Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class StepNumberDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is StepStatus status)
        {
            if (status == StepStatus.Done) return "✓";
        }
        return value?.ToString() ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class StepStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is StepStatus status)
        {
            return status switch
            {
                StepStatus.Done => new SolidColorBrush(Color.FromRgb(0x05, 0x68, 0x39)), // Green
                StepStatus.InProgress => new SolidColorBrush(Color.FromRgb(0x05, 0x68, 0x39)),
                StepStatus.Attention => new SolidColorBrush(Color.FromRgb(0xF7, 0x94, 0x1D)), // Orange
                StepStatus.Locked => new SolidColorBrush(Color.FromRgb(0xC7, 0xC3, 0xB4)),
                _ => new SolidColorBrush(Color.FromRgb(0x8A, 0x86, 0x76))
            };
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class StepStatusToTextBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is StepStatus status)
        {
            return status switch
            {
                StepStatus.Done => Brushes.White,
                StepStatus.InProgress => new SolidColorBrush(Color.FromRgb(0x05, 0x68, 0x39)),
                StepStatus.Attention => new SolidColorBrush(Color.FromRgb(0x1F, 0x24, 0x21)),
                StepStatus.Locked => new SolidColorBrush(Color.FromRgb(0xC7, 0xC3, 0xB4)),
                _ => new SolidColorBrush(Color.FromRgb(0x8A, 0x86, 0x76))
            };
        }
        return Brushes.Black;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class StringEqualityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (value is true && parameter != null) ? parameter.ToString()! : Binding.DoNothing;
    }
}
