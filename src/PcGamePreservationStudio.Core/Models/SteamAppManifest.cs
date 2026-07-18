namespace PcGamePreservationStudio.Core.Models;

/// <summary>Fields parsed from a Steam appmanifest_&lt;appid&gt;.acf file.</summary>
public sealed record SteamAppManifest
{
    public required string AppId { get; init; }
    public required string Name { get; init; }
    public required string InstallDir { get; init; }
    public long? SizeOnDiskBytes { get; init; }
    public string? BuildId { get; init; }
    public string? StateFlags { get; init; }
    public required string LibraryPath { get; init; }
    public required string ManifestPath { get; init; }
}
