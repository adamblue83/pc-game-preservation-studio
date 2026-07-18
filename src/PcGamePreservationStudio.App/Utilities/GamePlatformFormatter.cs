using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.App.Utilities;

public static class GamePlatformFormatter
{
    public static string Format(GamePlatform platform) => platform switch
    {
        GamePlatform.Steam => "Steam",
        GamePlatform.Gog => "GOG",
        GamePlatform.LocalFolder => "Local Folder",
        _ => platform.ToString(),
    };
}
