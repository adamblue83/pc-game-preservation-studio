using System.Globalization;
using System.Windows.Data;
using PcGamePreservationStudio.App.Utilities;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.App.Converters;

public sealed class DrmAnalysisConfidenceToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DrmAnalysisConfidence confidence ? DrmAnalysisFormatter.FormatConfidence(confidence) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
