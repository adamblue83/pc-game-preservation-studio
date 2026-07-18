using PcGamePreservationStudio.App.ViewModels;

namespace PcGamePreservationStudio.App.Services;

public interface INavigationService
{
    void NavigateTo(NavigationSection section);

    void NavigateToGameDetail(Guid gameId);

    void NavigateToCreateArchive(Guid gameId);

    void NavigateToGogInstallerArchive();

    void NavigateToRestore(string? archiveSourcePath = null);
}
