using CommunityToolkit.Mvvm.ComponentModel;
using PcGamePreservationStudio.App.Utilities;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.App.ViewModels;

public sealed partial class SaveLocationOptionViewModel(SaveLocationCandidate candidate) : ObservableObject
{
    public SaveLocationCandidate Candidate { get; } = candidate;

    [ObservableProperty]
    private bool _isIncluded;

    public string FullPath => Candidate.FullPath;

    public string ConfidenceLabel => Candidate.Confidence.ToString();

    public string SizeLabel => ByteSizeFormatter.Format(Candidate.EstimatedSizeBytes);

    public string DetectionReason => Candidate.DetectionReason;
}
