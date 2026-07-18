using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PcGamePreservationStudio.App.Services;
using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.App.ViewModels;

public sealed partial class LibraryViewModel : ViewModelBase
{
    private readonly IGameDetectionService _detectionService;
    private readonly ILocalFolderSourceRepository _localFolderRepository;
    private readonly INavigationService _navigationService;
    private readonly ILogger<LibraryViewModel> _logger;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public IReadOnlyList<PlatformFilterOption> PlatformFilterOptions { get; } =
    [
        new(null, "All platforms"),
        new(GamePlatform.Steam, "Steam"),
        new(GamePlatform.Gog, "GOG"),
        new(GamePlatform.LocalFolder, "Local Folder"),
    ];

    [ObservableProperty]
    private PlatformFilterOption _selectedPlatformFilter;

    public ObservableCollection<GameListItemViewModel> AllGames { get; } = [];

    public ICollectionView GamesView { get; }

    public LibraryViewModel(
        IGameDetectionService detectionService,
        ILocalFolderSourceRepository localFolderRepository,
        INavigationService navigationService,
        ILogger<LibraryViewModel> logger)
    {
        _detectionService = detectionService;
        _localFolderRepository = localFolderRepository;
        _navigationService = navigationService;
        _logger = logger;
        _selectedPlatformFilter = PlatformFilterOptions[0];

        GamesView = CollectionViewSource.GetDefaultView(AllGames);
        GamesView.Filter = FilterGame;

        _ = RefreshAsync();
    }

    partial void OnSearchTextChanged(string value) => GamesView.Refresh();

    partial void OnSelectedPlatformFilterChanged(PlatformFilterOption value) => GamesView.Refresh();

    private bool FilterGame(object obj)
    {
        if (obj is not GameListItemViewModel item)
        {
            return false;
        }

        if (SelectedPlatformFilter.Platform is { } platform && item.Entry.Platform != platform)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(SearchText)
               || item.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var games = await _detectionService.GetAllGamesAsync();

            AllGames.Clear();
            foreach (var game in games.OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase))
            {
                AllGames.Add(new GameListItemViewModel(game));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load game library");
            ErrorMessage = "Something went wrong while detecting games. Check the log for details.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SelectGame(GameListItemViewModel? item)
    {
        if (item is not null)
        {
            _navigationService.NavigateToGameDetail(item.Id);
        }
    }

    [RelayCommand]
    private async Task AddLocalFolderAsync()
    {
        var dialog = new OpenFolderDialog { Title = "Select a game folder to add" };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var folderPath = dialog.FolderName;
        var displayName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar));

        try
        {
            await _localFolderRepository.AddAsync(new LocalFolderSource
            {
                Id = Guid.NewGuid(),
                Path = folderPath,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? folderPath : displayName,
                Kind = LocalFolderSourceKind.InstalledGame,
            });

            await RefreshAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Failed to add local folder source {Path}", folderPath);
            ErrorMessage = "Something went wrong while adding that folder.";
        }
    }

    [RelayCommand]
    private void AddGogOfflineInstaller() => _navigationService.NavigateToGogInstallerArchive();
}
