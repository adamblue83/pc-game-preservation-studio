namespace PcGamePreservationStudio.Platforms.Gog;

public sealed record GogInstallerFile(string FilePath, long SizeBytes);

public enum GogInstallerGroupKind
{
    BaseGame,
    Dlc,
    Patch,
    Soundtrack,
    Manual,
    Extra,
}

/// <summary>
/// A set of installer files believed to belong together (e.g. a setup .exe and its numbered .bin
/// parts), surfaced for the user to confirm — grouping is a heuristic, never assumed to be correct.
/// </summary>
public sealed record GogInstallerGroup
{
    public required string GroupKey { get; init; }
    public required GogInstallerGroupKind Kind { get; init; }
    public required IReadOnlyList<GogInstallerFile> Files { get; init; }

    public long TotalBytes => Files.Sum(f => f.SizeBytes);
}
