namespace PcGamePreservationStudio.Core.Models;

/// <summary>Result of converting a staged archive folder into a UDF ISO image via <see cref="Abstractions.IIsoBuilder"/>.</summary>
public sealed record IsoBuildResult
{
    public required bool Success { get; init; }
    public string? IsoPath { get; init; }
    public long IsoSizeBytes { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>Whether a backend capable of building an ISO (e.g. oscdimg.exe) was found on this machine.</summary>
public sealed record IsoBackendAvailability
{
    public required bool IsAvailable { get; init; }
    public string? ExecutablePath { get; init; }
    public required string StatusMessage { get; init; }
}
