namespace PcGamePreservationStudio.Core.Models;

/// <summary>Subset of Metadata\archive_manifest.json relevant to restoring — see docs/ARCHIVE_FORMAT.md.</summary>
public sealed record ArchiveManifestInfo
{
    public required string Title { get; init; }
    public required string Platform { get; init; }
    public string? AppId { get; init; }
    public DateTimeOffset ArchiveCreatedUtc { get; init; }
    public string? SourceInstallPath { get; init; }
    public long ArchiveSizeBytes { get; init; }
    public string? RequiredLauncher { get; init; }
    public string? RequiredAccountNotice { get; init; }
    public string? PreservationRating { get; init; }
    public IReadOnlyList<string> PreservationReasons { get; init; } = [];
}

/// <summary>One entry from Metadata\save_locations.json, naming the Saves\&lt;ArchiveFolderName&gt;\ folder it came from.</summary>
public sealed record RestoreSaveLocationInfo
{
    public required string ArchiveFolderName { get; init; }
    public required string OriginalFullPath { get; init; }
    public required string Kind { get; init; }
    public required string Confidence { get; init; }
    public string? DetectionReason { get; init; }
}

/// <summary>Fast, read-only look at an archive before restoring — reads manifest/save-location metadata but never hashes files.</summary>
public sealed record RestorePreflightResult
{
    public required bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public ArchiveManifestInfo? Manifest { get; init; }
    public IReadOnlyList<RestoreSaveLocationInfo> SaveLocations { get; init; } = [];
    public bool HasGameFiles { get; init; }
    public bool HasInstallerFiles { get; init; }
    public bool HasPlatformFiles { get; init; }
}

/// <summary>Where to restore one recorded save location to — defaults to <see cref="RestoreSaveLocationInfo.OriginalFullPath"/>, but the user may redirect it.</summary>
public sealed record RestoreSaveLocationSelection
{
    public required string ArchiveFolderName { get; init; }
    public required string RestoreToPath { get; init; }
}

public sealed record RestoreRequest
{
    public required string ArchiveSourcePath { get; init; }

    /// <summary>Where Game\, Installers\, and Platform\ contents are copied. Required whenever the archive has any of those.</summary>
    public string? InstallDestinationPath { get; init; }

    public IReadOnlyList<RestoreSaveLocationSelection> SaveLocationsToRestore { get; init; } = [];

    /// <summary>When false (the default), a file that already exists at its destination is left untouched and reported in <see cref="RestoreResult.SkippedExistingFiles"/> rather than overwritten.</summary>
    public bool OverwriteExistingFiles { get; init; }
}

public enum RestoreStage
{
    Verifying,
    CopyingGameFiles,
    CopyingInstallerFiles,
    CopyingPlatformFiles,
    CopyingSaves,
    Completed,
    Failed,
}

public sealed record RestoreProgress
{
    public required RestoreStage Stage { get; init; }
    public string? CurrentItem { get; init; }
}

public sealed record RestoreResult
{
    public required bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    /// <summary>The re-hash-and-compare pass that ran before any file was touched — restoring never proceeds past a <see cref="VerificationOutcome.Failed"/> or <see cref="VerificationOutcome.Incomplete"/> result.</summary>
    public VerificationResult? VerificationResult { get; init; }

    public int FilesRestored { get; init; }
    public IReadOnlyList<string> SkippedExistingFiles { get; init; } = [];

    /// <summary>Follow-up guidance the app can't act on itself — e.g. that a copied Steam manifest still needs to be moved into a Steam library's steamapps\ folder by hand.</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];
}
