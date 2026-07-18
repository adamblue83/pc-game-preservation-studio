namespace PcGamePreservationStudio.App.ViewModels;

/// <summary>Placeholder for nav sections whose backing services (Phase 3+) aren't implemented yet.</summary>
public sealed class ComingSoonViewModel(string featureName, string explanation) : ViewModelBase
{
    public string FeatureName { get; } = featureName;

    public string Explanation { get; } = explanation;
}
