namespace PcGamePreservationStudio.Core.Models;

/// <summary>
/// UI-facing aggregate for the game detail page. DRM/save/build fields stay optional
/// until the analysis services that populate them (Phase 3+) exist.
/// </summary>
public sealed record GameDetail
{
    public required GameLibraryEntry Entry { get; init; }
    public string? BuildId { get; init; }
    public DateTimeOffset? LastUpdatedUtc { get; init; }
    public string? Notes { get; init; }
}
