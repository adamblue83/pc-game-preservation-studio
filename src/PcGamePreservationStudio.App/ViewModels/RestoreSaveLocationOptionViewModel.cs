using CommunityToolkit.Mvvm.ComponentModel;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.App.ViewModels;

public sealed partial class RestoreSaveLocationOptionViewModel(RestoreSaveLocationInfo info) : ObservableObject
{
    public RestoreSaveLocationInfo Info { get; } = info;

    [ObservableProperty]
    private bool _isIncluded = true;

    [ObservableProperty]
    private string _restoreToPath = info.OriginalFullPath;

    public string ArchiveFolderName => Info.ArchiveFolderName;

    public string OriginalFullPath => Info.OriginalFullPath;

    public string Kind => Info.Kind;

    public string Confidence => Info.Confidence;
}
