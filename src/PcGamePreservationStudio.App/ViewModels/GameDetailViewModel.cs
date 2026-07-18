using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PcGamePreservationStudio.App.Services;
using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.App.ViewModels;

public sealed partial class GameDetailViewModel(
    IGameDetectionService detectionService,
    IDrmAnalysisService drmAnalysisService,
    INavigationService navigationService,
    ILogger<GameDetailViewModel> logger) : ViewModelBase
{
    [ObservableProperty]
    private GameDetail? _detail;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private DrmAnalysisResult? _drmAnalysis;

    [ObservableProperty]
    private bool _isAnalyzing;

    public async Task LoadAsync(Guid gameId)
    {
        IsLoading = true;
        ErrorMessage = null;
        DrmAnalysis = null;
        try
        {
            Detail = await detectionService.GetGameDetailAsync(gameId);
            if (Detail is null)
            {
                ErrorMessage = "This game could not be found. It may have been removed since the library was last refreshed.";
                return;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load game detail for {GameId}", gameId);
            ErrorMessage = "Something went wrong while loading this game's details.";
            return;
        }
        finally
        {
            IsLoading = false;
        }

        IsAnalyzing = true;
        try
        {
            DrmAnalysis = await drmAnalysisService.AnalyzeAsync(Detail.Entry);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run DRM analysis for {GameId}", gameId);
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    [RelayCommand]
    private void Archive()
    {
        if (Detail is not null)
        {
            navigationService.NavigateToCreateArchive(Detail.Entry.Id);
        }
    }
}
