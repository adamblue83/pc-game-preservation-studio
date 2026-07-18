namespace PcGamePreservationStudio.Core.Models;

/// <summary>A single Steam library location, parsed from libraryfolders.vdf.</summary>
public sealed record SteamLibraryFolder
{
    public required string Path { get; init; }
    public long? FreeSpaceBytes { get; init; }
}
