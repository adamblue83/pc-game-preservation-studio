namespace PcGamePreservationStudio.Core.Models;

public sealed record ArchiveBuildRequest
{
    public required GameLibraryEntry Game { get; init; }
    public required string DestinationFolder { get; init; }
    public IReadOnlyList<string> IncludedSaveLocationPaths { get; init; } = [];
    public string? Notes { get; init; }

    /// <summary>
    /// Individual offline installer files (e.g. confirmed GOG installer groups) copied into
    /// Installers\ instead of Game\. When set and <see cref="GameLibraryEntry.InstallPath"/> is
    /// empty, the archive contains only these files — there is no installed-game directory to copy.
    /// </summary>
    public IReadOnlyList<string> InstallerFilePaths { get; init; } = [];

    /// <summary>Folder-only by default. A disc-based value splits the built archive into DISC_01\, DISC_02\, ... folders.</summary>
    public MediaType MediaType { get; init; } = MediaType.FolderOnly;

    /// <summary>Required when <see cref="MediaType"/> is <see cref="Models.MediaType.Custom"/>, <see cref="Models.MediaType.Usb"/>, or <see cref="Models.MediaType.ExternalDrive"/>.</summary>
    public long? CustomCapacityBytes { get; init; }

    public long? SafetyMarginOverrideBytes { get; init; }

    /// <summary>User-configured oscdimg.exe path, used only when <see cref="MediaType"/> is <see cref="Models.MediaType.IsoOnly"/>.</summary>
    public string? OscdimgPathOverride { get; init; }
}
