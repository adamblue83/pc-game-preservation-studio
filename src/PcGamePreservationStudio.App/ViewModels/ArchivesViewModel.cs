using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PcGamePreservationStudio.App.Services;
using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.App.ViewModels;

public sealed partial class ArchivesViewModel : ViewModelBase
{
    private readonly IArchiveCatalogRepository _repository;
    private readonly INavigationService _navigationService;
    private readonly ILogger<ArchivesViewModel> _logger;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public ObservableCollection<ArchiveCatalogEntry> Archives { get; } = [];

    public ArchivesViewModel(IArchiveCatalogRepository repository, INavigationService navigationService, ILogger<ArchivesViewModel> logger)
    {
        _repository = repository;
        _navigationService = navigationService;
        _logger = logger;

        _ = RefreshAsync();
    }

    [RelayCommand]
    private void Restore(ArchiveCatalogEntry entry) => _navigationService.NavigateToRestore(entry.ArchiveLocation);

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var entries = await _repository.GetAllAsync();
            Archives.Clear();
            foreach (var entry in entries)
            {
                Archives.Add(entry);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load archive catalog");
            ErrorMessage = "Something went wrong while loading the archive catalog.";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
