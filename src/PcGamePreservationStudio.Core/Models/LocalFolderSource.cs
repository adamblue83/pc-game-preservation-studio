namespace PcGamePreservationStudio.Core.Models;

public enum LocalFolderSourceKind
{
    InstalledGame,
    OfflineInstaller,
    PortableGame,
    Mod,
    Patch,
    Dlc,
    PersonalProject,
    Other,
}

/// <summary>A user-designated arbitrary folder treated as a manual game source.</summary>
public sealed record LocalFolderSource
{
    public required Guid Id { get; init; }
    public required string Path { get; init; }
    public required string DisplayName { get; init; }
    public required LocalFolderSourceKind Kind { get; init; }
    public string? Version { get; init; }
    public string? Publisher { get; init; }
    public string? Notes { get; init; }
}
