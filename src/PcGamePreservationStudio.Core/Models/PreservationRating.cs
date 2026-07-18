namespace PcGamePreservationStudio.Core.Models;

/// <summary>An understandable preservation rating. Never a scientific guarantee — always shown with reasons.</summary>
public enum PreservationRating
{
    Excellent,
    Good,
    Limited,
    Unknown,
}

public sealed record PreservationAssessment
{
    public required PreservationRating Rating { get; init; }
    public required IReadOnlyList<string> Reasons { get; init; }
}
