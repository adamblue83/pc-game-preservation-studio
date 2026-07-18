using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using PcGamePreservationStudio.App.Utilities;
using PcGamePreservationStudio.Platforms.Gog;

namespace PcGamePreservationStudio.App.ViewModels;

public sealed partial class GogInstallerGroupOptionViewModel(GogInstallerGroup group) : ObservableObject
{
    public GogInstallerGroup Group { get; } = group;

    [ObservableProperty]
    private bool _isIncluded = true;

    public string GroupKey => Group.GroupKey;

    public string KindLabel => Group.Kind.ToString();

    public int FileCount => Group.Files.Count;

    public string SizeLabel => ByteSizeFormatter.Format(Group.TotalBytes);

    public string FileNamesSummary => string.Join(", ", Group.Files.Select(f => Path.GetFileName(f.FilePath)));
}
