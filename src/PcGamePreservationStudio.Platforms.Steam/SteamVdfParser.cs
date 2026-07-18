using Gameloop.Vdf;
using Gameloop.Vdf.Linq;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Platforms.Steam;

/// <summary>Parses Steam's KeyValues/VDF text format: libraryfolders.vdf and appmanifest_&lt;appid&gt;.acf.</summary>
public static class SteamVdfParser
{
    public static IReadOnlyList<SteamLibraryFolder> ParseLibraryFolders(string vdfContent)
    {
        var root = VdfConvert.Deserialize(vdfContent);
        if (root.Value is not VObject foldersObject)
        {
            return [];
        }

        var results = new List<SteamLibraryFolder>();
        foreach (var property in foldersObject.Properties())
        {
            if (property.Value is not VObject folderObject)
            {
                continue;
            }

            var path = folderObject["path"]?.ToString();
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            results.Add(new SteamLibraryFolder { Path = path });
        }

        return results;
    }

    public static SteamAppManifest? ParseAppManifest(string acfContent, string libraryPath, string manifestPath)
    {
        var root = VdfConvert.Deserialize(acfContent);
        if (root.Value is not VObject appState)
        {
            return null;
        }

        var appId = appState["appid"]?.ToString();
        var name = appState["name"]?.ToString();
        var installDir = appState["installdir"]?.ToString();

        if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(installDir))
        {
            return null;
        }

        var sizeOnDiskBytes = long.TryParse(appState["SizeOnDisk"]?.ToString(), out var size) ? size : (long?)null;

        return new SteamAppManifest
        {
            AppId = appId,
            Name = name,
            InstallDir = installDir,
            SizeOnDiskBytes = sizeOnDiskBytes,
            BuildId = appState["buildid"]?.ToString(),
            StateFlags = appState["StateFlags"]?.ToString(),
            LibraryPath = libraryPath,
            ManifestPath = manifestPath,
        };
    }
}
