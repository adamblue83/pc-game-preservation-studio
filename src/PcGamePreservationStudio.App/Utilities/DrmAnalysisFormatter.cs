using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.App.Utilities;

public static class DrmAnalysisFormatter
{
    public static string FormatLabel(DrmAnalysisLabel label) => label switch
    {
        DrmAnalysisLabel.DrmFreeConfirmedBySourceType => "DRM-free (confirmed by source)",
        DrmAnalysisLabel.LikelyDrmFree => "Likely DRM-free",
        DrmAnalysisLabel.PlatformClientLikelyRequired => "Platform client likely required",
        DrmAnalysisLabel.ThirdPartyLauncherLikelyRequired => "Third-party launcher likely required",
        DrmAnalysisLabel.OnlineActivationMayBeRequired => "Online activation may be required",
        DrmAnalysisLabel.ManualOfflineTestRecommended => "Manual offline test recommended",
        DrmAnalysisLabel.Unknown => "Unknown",
        _ => label.ToString(),
    };

    public static string FormatConfidence(DrmAnalysisConfidence confidence) => confidence switch
    {
        DrmAnalysisConfidence.Low => "Low confidence",
        DrmAnalysisConfidence.Medium => "Medium confidence",
        DrmAnalysisConfidence.High => "High confidence",
        _ => confidence.ToString(),
    };
}
