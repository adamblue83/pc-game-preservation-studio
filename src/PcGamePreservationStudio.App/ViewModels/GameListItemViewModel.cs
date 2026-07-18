using PcGamePreservationStudio.App.Utilities;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.App.ViewModels;

public sealed class GameListItemViewModel(GameLibraryEntry entry)
{
    public GameLibraryEntry Entry { get; } = entry;

    public Guid Id => Entry.Id;

    public string Title => Entry.Title;

    public string PlatformLabel => GamePlatformFormatter.Format(Entry.Platform);

    public string SizeLabel => ByteSizeFormatter.Format(Entry.SizeOnDiskBytes);
}
