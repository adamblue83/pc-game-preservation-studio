using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PcGamePreservationStudio.App.Converters;

/// <summary>Visible when the bound string is non-null/non-whitespace; Collapsed otherwise.</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
