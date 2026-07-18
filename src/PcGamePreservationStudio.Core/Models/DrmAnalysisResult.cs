namespace PcGamePreservationStudio.Core.Models;

public enum DrmAnalysisLabel
{
    DrmFreeConfirmedBySourceType,
    LikelyDrmFree,
    PlatformClientLikelyRequired,
    ThirdPartyLauncherLikelyRequired,
    OnlineActivationMayBeRequired,
    Unknown,
    ManualOfflineTestRecommended,
}

public enum DrmAnalysisConfidence
{
    Low,
    Medium,
    High,
}

/// <summary>One piece of evidence (e.g. a detected file) backing a DRM analysis finding.</summary>
public sealed record DrmEvidenceFinding
{
    public required string Description { get; init; }
    public required string Explanation { get; init; }
}

/// <summary>
/// Evidence-based DRM/launcher-requirement report. Never asserts certainty.
/// Produced by <c>IDrmAnalysisService</c> (Phase 3, not yet implemented).
/// </summary>
public sealed record DrmAnalysisResult
{
    public required DrmAnalysisLabel Label { get; init; }
    public required DrmAnalysisConfidence Confidence { get; init; }
    public IReadOnlyList<DrmEvidenceFinding> Findings { get; init; } = [];
}
