using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

namespace PrintPilotProxy.App.Converters;

/// <summary>
/// Returns Visible when the string is non-empty, Collapsed otherwise.
/// </summary>
[ValueConversion(typeof(string), typeof(Visibility))]
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns Visible when the bool is true, Collapsed when false.
/// Pass parameter "Invert" to reverse.
/// </summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is bool bv && bv;
        bool invert = parameter as string == "Invert";
        return (b ^ invert) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Inverts a boolean value.
/// </summary>
[ValueConversion(typeof(bool), typeof(bool))]
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;
}

/// <summary>
/// Converts a brush key name (e.g., "SuccessBrush") or hex color string into a Color.
/// </summary>
[ValueConversion(typeof(string), typeof(MediaColor))]
public sealed class BrushKeyToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string keyOrHex && !string.IsNullOrWhiteSpace(keyOrHex))
        {
            if (Application.Current?.Resources.Contains(keyOrHex) == true &&
                Application.Current.Resources[keyOrHex] is SolidColorBrush scb)
            {
                return scb.Color;
            }
            if (Application.Current?.Resources.Contains(keyOrHex) == true &&
                Application.Current.Resources[keyOrHex] is MediaColor c)
            {
                return c;
            }
            try
            {
                var converted = System.Windows.Media.ColorConverter.ConvertFromString(keyOrHex);
                if (converted is MediaColor parsedColor)
                    return parsedColor;
            }
            catch { }
        }
        return Colors.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

