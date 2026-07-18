using System.Globalization;

namespace PcGamePreservationStudio.App.Utilities;

public static class ByteSizeFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public static string Format(long? bytes)
    {
        if (bytes is not { } value)
        {
            return "Unknown size";
        }

        double size = value;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < Units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size.ToString("0.#", CultureInfo.CurrentCulture)} {Units[unitIndex]}";
    }
}
