namespace PcGamePreservationStudio.Core.Models;

/// <summary>Result of burning an ISO to optical media via <see cref="Abstractions.IDiscBurner"/> (Phase 7).</summary>
public sealed record DiscBurnResult
{
    public required bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}
