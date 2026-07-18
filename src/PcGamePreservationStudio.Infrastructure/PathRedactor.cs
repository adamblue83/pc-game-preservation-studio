using System.Text.RegularExpressions;

namespace PcGamePreservationStudio.Infrastructure;

/// <summary>Redacts the Windows username segment from a path so it's safe to write to logs.</summary>
public static partial class PathRedactor
{
    public static string Redact(string path)
    {
        return UserProfilePathPattern().Replace(path, m => $"{m.Groups["prefix"].Value}<redacted>");
    }

    [GeneratedRegex(@"(?<prefix>[A-Za-z]:\\Users\\)[^\\]+", RegexOptions.IgnoreCase)]
    private static partial Regex UserProfilePathPattern();
}
