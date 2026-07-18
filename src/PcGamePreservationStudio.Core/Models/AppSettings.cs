namespace PcGamePreservationStudio.Core.Models;

public sealed record AppSettings
{
    public string? SteamInstallPathOverride { get; init; }
    public string? DefaultArchiveOutputFolder { get; init; }
    public string? OscdimgPathOverride { get; init; }
    public bool HasCompletedFirstRun { get; init; }
    public bool SkipPlatformDetection { get; init; }
}
