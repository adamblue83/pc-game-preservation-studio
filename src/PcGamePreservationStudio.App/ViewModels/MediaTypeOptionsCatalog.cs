using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.App.ViewModels;

public static class MediaTypeOptionsCatalog
{
    public static readonly IReadOnlyList<MediaTypeOption> All =
    [
        new(MediaType.FolderOnly, "Folder Only"),
        new(MediaType.IsoOnly, "ISO Image (.iso)"),
        new(MediaType.Cd700, "CD-R (700 MB)"),
        new(MediaType.Dvd5, "DVD-5 (4.7 GB)"),
        new(MediaType.Dvd9, "DVD-9 (8.5 GB)"),
        new(MediaType.Bd25, "BD-25 (25 GB)"),
        new(MediaType.Bd50, "BD-50 (50 GB)"),
        new(MediaType.Bd100, "BD-100 (100 GB)"),
        new(MediaType.Bd128, "BD-128 (128 GB)"),
    ];

    public static MediaTypeOption Default => All[0];
}
