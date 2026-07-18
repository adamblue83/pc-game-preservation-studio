using System.Text.RegularExpressions;

namespace PcGamePreservationStudio.Platforms.Gog;

/// <summary>
/// Groups downloaded GOG offline installer files (setup .exe + numbered .bin parts, separate DLC
/// / patch / soundtrack / manual installers) by shared base filename. Deliberately conservative:
/// files are only merged when they share an exact base name, never fuzzy-matched across different
/// installers — see docs/PLATFORM_SUPPORT.md for why. The caller must let the user confirm the
/// result before archiving.
/// </summary>
public static partial class GogOfflineInstallerGrouper
{
    public static IReadOnlyList<GogInstallerGroup> GroupInstallers(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            return [];
        }

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(folderPath, "*", SearchOption.TopDirectoryOnly).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        var groupedPaths = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var groupKey = DeriveGroupKey(file);
            if (!groupedPaths.TryGetValue(groupKey, out var list))
            {
                list = [];
                groupedPaths[groupKey] = list;
            }

            list.Add(file);
        }

        return groupedPaths
            .Select(kvp => BuildGroup(kvp.Key, kvp.Value))
            .OrderBy(g => g.Kind)
            .ThenBy(g => g.GroupKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string DeriveGroupKey(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var binPartMatch = BinPartPattern().Match(fileName);
        return binPartMatch.Success ? binPartMatch.Groups["base"].Value : Path.GetFileNameWithoutExtension(fileName);
    }

    private static GogInstallerGroup BuildGroup(string groupKey, List<string> filePaths)
    {
        var files = filePaths.Select(f => new GogInstallerFile(f, TryGetLength(f))).ToList();
        return new GogInstallerGroup
        {
            GroupKey = groupKey,
            Kind = ClassifyGroup(groupKey, filePaths),
            Files = files,
        };
    }

    private static long TryGetLength(string filePath)
    {
        try
        {
            return new FileInfo(filePath).Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static GogInstallerGroupKind ClassifyGroup(string groupKey, List<string> filePaths)
    {
        var lowerKey = groupKey.ToLowerInvariant();

        if (lowerKey.Contains("dlc"))
        {
            return GogInstallerGroupKind.Dlc;
        }

        if (lowerKey.Contains("patch") || lowerKey.Contains("update"))
        {
            return GogInstallerGroupKind.Patch;
        }

        if (lowerKey.Contains("soundtrack") || lowerKey.Contains("ost"))
        {
            return GogInstallerGroupKind.Soundtrack;
        }

        if (lowerKey.Contains("manual"))
        {
            return GogInstallerGroupKind.Manual;
        }

        var hasExecutable = filePaths.Any(f => string.Equals(Path.GetExtension(f), ".exe", StringComparison.OrdinalIgnoreCase));
        return hasExecutable ? GogInstallerGroupKind.BaseGame : GogInstallerGroupKind.Extra;
    }

    [GeneratedRegex(@"^(?<base>.+)-\d+\.bin$", RegexOptions.IgnoreCase)]
    private static partial Regex BinPartPattern();
}
