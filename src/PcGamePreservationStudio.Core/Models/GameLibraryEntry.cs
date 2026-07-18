namespace PcGamePreservationStudio.Core.Models;

/// <summary>A game detected from a platform provider (or a manually-added local folder source).</summary>
public sealed record GameLibraryEntry
{
    public required Guid Id { get; init; }
    public required GamePlatform Platform { get; init; }
    public required string Title { get; init; }
    public required string InstallPath { get; init; }
    public long? SizeOnDiskBytes { get; init; }
    public string? PlatformAppId { get; init; }
    public string? ManifestPath { get; init; }
    public string? LibraryPath { get; init; }
    public DateTimeOffset LastDetectedUtc { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<string> ExecutableCandidates { get; init; } = [];
}
