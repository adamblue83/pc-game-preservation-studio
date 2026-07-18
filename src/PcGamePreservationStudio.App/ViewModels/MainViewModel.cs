using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PcGamePreservationStudio.App.Services;

namespace PcGamePreservationStudio.App.ViewModels;

public sealed partial class MainViewModel(IServiceProvider serviceProvider) : ViewModelBase, INavigationService
{
    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private NavigationSection _selectedSection = NavigationSection.Library;

    public IReadOnlyList<NavigationSection> Sections { get; } =
    [
        NavigationSection.Library,
        NavigationSection.CreateArchive,
        NavigationSection.Archives,
        NavigationSection.BurnDisc,
        NavigationSection.VerifyDisc,
        NavigationSection.Restore,
        NavigationSection.Settings,
        NavigationSection.About,
    ];

    public void Initialize() => NavigateTo(NavigationSection.Library);

    [RelayCommand]
    public void NavigateTo(NavigationSection section)
    {
        SelectedSection = section;
        CurrentPage = section switch
        {
            NavigationSection.Library => serviceProvider.GetRequiredService<LibraryViewModel>(),
            NavigationSection.Archives => serviceProvider.GetRequiredService<ArchivesViewModel>(),
            NavigationSection.Settings => serviceProvider.GetRequiredService<SettingsViewModel>(),
            NavigationSection.About => serviceProvider.GetRequiredService<AboutViewModel>(),
            NavigationSection.CreateArchive => serviceProvider.GetRequiredService<CreateArchiveViewModel>(),
            NavigationSection.BurnDisc => serviceProvider.GetRequiredService<BurnDiscViewModel>(),
            NavigationSection.VerifyDisc => serviceProvider.GetRequiredService<VerifyDiscViewModel>(),
            NavigationSection.Restore => serviceProvider.GetRequiredService<RestoreViewModel>(),
            _ => throw new ArgumentOutOfRangeException(nameof(section)),
        };
    }

    public void NavigateToGameDetail(Guid gameId)
    {
        var detailViewModel = serviceProvider.GetRequiredService<GameDetailViewModel>();
        SelectedSection = NavigationSection.Library;
        CurrentPage = detailViewModel;
        _ = detailViewModel.LoadAsync(gameId);
    }

    public void NavigateToCreateArchive(Guid gameId)
    {
        var createArchiveViewModel = serviceProvider.GetRequiredService<CreateArchiveViewModel>();
        SelectedSection = NavigationSection.CreateArchive;
        CurrentPage = createArchiveViewModel;
        _ = createArchiveViewModel.LoadAsync(gameId);
    }

    public void NavigateToGogInstallerArchive()
    {
        SelectedSection = NavigationSection.CreateArchive;
        CurrentPage = serviceProvider.GetRequiredService<GogInstallerArchiveViewModel>();
    }

    public void NavigateToRestore(string? archiveSourcePath = null)
    {
        var restoreViewModel = serviceProvider.GetRequiredService<RestoreViewModel>();
        SelectedSection = NavigationSection.Restore;
        CurrentPage = restoreViewModel;
        if (!string.IsNullOrWhiteSpace(archiveSourcePath))
        {
            restoreViewModel.LoadWithArchivePath(archiveSourcePath);
        }
    }
}
