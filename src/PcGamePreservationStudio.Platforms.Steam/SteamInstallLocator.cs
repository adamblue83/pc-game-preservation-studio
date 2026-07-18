using Microsoft.Win32;

namespace PcGamePreservationStudio.Platforms.Steam;

/// <summary>Locates the Steam installation directory without requiring the user to browse for it.</summary>
public static class SteamInstallLocator
{
    private static readonly string[] DefaultPaths =
    [
        @"C:\Program Files (x86)\Steam",
        @"C:\Program Files\Steam",
    ];

    public static string? Locate(string? userOverride = null)
    {
        if (!string.IsNullOrWhiteSpace(userOverride) && Directory.Exists(userOverride))
        {
            return userOverride;
        }

        var fromRegistry = LocateFromRegistry();
        if (fromRegistry is not null && Directory.Exists(fromRegistry))
        {
            return fromRegistry;
        }

        return DefaultPaths.FirstOrDefault(Directory.Exists);
    }

    private static string? LocateFromRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            return key?.GetValue("SteamPath") as string ?? key?.GetValue("InstallPath") as string;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return null;
        }
    }
}
