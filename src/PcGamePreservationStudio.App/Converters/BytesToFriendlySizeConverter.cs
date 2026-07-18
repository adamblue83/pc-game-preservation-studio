using System.Globalization;
using System.Windows.Data;
using PcGamePreservationStudio.App.Utilities;

namespace PcGamePreservationStudio.App.Converters;

public sealed class BytesToFriendlySizeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ByteSizeFormatter.Format(value as long?);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
