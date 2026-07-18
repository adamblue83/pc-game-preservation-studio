using System.Globalization;
using System.Windows.Data;

namespace PcGamePreservationStudio.App.Converters;

public sealed class FitsOnSingleMediumToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "fits on one disc" : "will need more than one disc (exact count is calculated when you archive)";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
