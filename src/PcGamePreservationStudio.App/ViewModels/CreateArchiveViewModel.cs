using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PcGamePreservationStudio.App.Services;
using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.App.ViewModels;

public sealed partial class CreateArchiveViewModel(
    IGameDetectionService detectionService,
    ISaveDetectionService saveDetectionService,
    IArchiveBuilder archiveBuilder,
    IArchiveVerificationService verificationService,
    IDiscCapacityService discCapacityService,
    IArchiveCatalogRepository catalogRepository,
    ISettingsService settingsService,
    INavigationService navigationService,
    ILogger<CreateArchiveViewModel> logger) : ViewModelBase
{
    [ObservableProperty]
    private GameLibraryEntry? _game;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isBuilding;

    [ObservableProperty]
    private string? _destinationFolder;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _progressText;

    [ObservableProperty]
    private ArchiveBuildResult? _buildResult;

    [ObservableProperty]
    private VerificationResult? _verificationResult;

    public IReadOnlyList<MediaTypeOption> MediaTypeOptions => MediaTypeOptionsCatalog.All;

    [ObservableProperty]
    private MediaTypeOption _selectedMediaType = MediaTypeOptionsCatalog.Default;

    [ObservableProperty]
    private string? _safetyMarginMbText;

    [ObservableProperty]
    private MediaCapacityPlan? _capacityPreview;

    public ObservableCollection<SaveLocationOptionViewModel> SaveCandidates { get; } = [];

    private string? _oscdimgPathOverride;

    partial void OnSelectedMediaTypeChanged(MediaTypeOption value)
    {
        RefreshDefaultSafetyMargin();
        RecomputeCapacityPreview();
    }

    partial void OnSafetyMarginMbTextChanged(string? value) => RecomputeCapacityPreview();

    public async Task LoadAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        BuildResult = null;
        VerificationResult = null;

        try
        {
            var detail = await detectionService.GetGameDetailAsync(gameId, cancellationToken);
            Game = detail?.Entry;
            if (Game is null)
            {
                ErrorMessage = "This game could not be found. It may have been removed since the library was last refreshed.";
                return;
            }

            var settings = await settingsService.LoadAsync(cancellationToken);
            DestinationFolder = settings.DefaultArchiveOutputFolder;
            _oscdimgPathOverride = settings.OscdimgPathOverride;

            var candidates = await saveDetectionService.DetectSaveLocationsAsync(Game, cancellationToken);
            SaveCandidates.Clear();
            foreach (var candidate in candidates)
            {
                SaveCandidates.Add(new SaveLocationOptionViewModel(candidate) { IsIncluded = candidate.Confidence == SaveLocationConfidence.High });
            }

            RefreshDefaultSafetyMargin();
            RecomputeCapacityPreview();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to prepare Create Archive for game {GameId}", gameId);
            ErrorMessage = "Something went wrong while preparing this archive.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void GoToLibrary() => navigationService.NavigateTo(NavigationSection.Library);

    [RelayCommand]
    private void BrowseDestination()
    {
        var dialog = new OpenFolderDialog { Title = "Select where to create the archive" };
        if (dialog.ShowDialog() == true)
        {
            DestinationFolder = dialog.FolderName;
        }
    }

    private bool CanBuild() => Game is not null && !string.IsNullOrWhiteSpace(DestinationFolder) && !IsBuilding;

    [RelayCommand(CanExecute = nameof(CanBuild))]
    private async Task BuildAsync(CancellationToken cancellationToken)
    {
        if (Game is null || string.IsNullOrWhiteSpace(DestinationFolder))
        {
            return;
        }

        var game = Game;
        var destinationFolder = DestinationFolder;
        var mediaType = SelectedMediaType.MediaType;

        IsBuilding = true;
        ErrorMessage = null;
        BuildResult = null;
        VerificationResult = null;
        ProgressText = "Starting…";

        try
        {
            var request = new ArchiveBuildRequest
            {
                Game = game,
                DestinationFolder = destinationFolder,
                IncludedSaveLocationPaths = [.. SaveCandidates.Where(c => c.IsIncluded).Select(c => c.FullPath)],
                MediaType = mediaType,
                SafetyMarginOverrideBytes = TryParseSafetyMarginBytes(),
                OscdimgPathOverride = _oscdimgPathOverride,
            };

            var progress = new Progress<ArchiveBuildProgress>(p =>
            {
                ProgressText = p.Stage switch
                {
                    ArchiveBuildStage.CollectingFiles => "Collecting files…",
                    ArchiveBuildStage.CopyingFiles => $"Copying {p.CurrentItem} ({p.FilesProcessed}/{p.TotalFiles})",
                    ArchiveBuildStage.WritingMetadata => "Writing metadata and checksums…",
                    ArchiveBuildStage.BuildingIso => "Building ISO image…",
                    ArchiveBuildStage.Verifying => "Verifying…",
                    ArchiveBuildStage.Completed => "Build complete",
                    _ => ProgressText,
                };
            });

            BuildResult = await archiveBuilder.BuildAsync(request, progress, cancellationToken);

            if (BuildResult.Success)
            {
                if (BuildResult.IsoPath is not null || BuildResult.DiscIsoPaths.Count > 0)
                {
                    // The staged folder (or each DISC_NN\ folder) was consumed into its ISO;
                    // folder-based verification no longer applies here (ISO-level verification
                    // happens later, when the ISO is burned and read back via Verify Disc).
                    VerificationResult = null;
                }
                else
                {
                    ProgressText = "Verifying…";
                    VerificationResult = await verificationService.VerifyAsync(BuildResult.ArchiveFolderPath, cancellationToken);
                }

                await catalogRepository.AddAsync(new ArchiveCatalogEntry
                {
                    Id = Guid.NewGuid(),
                    GameTitle = game.Title,
                    Platform = game.Platform,
                    CreatedUtc = DateTimeOffset.UtcNow,
                    DestinationType = ResolveDestinationType(BuildResult, mediaType),
                    Status = ResolveStatus(BuildResult, VerificationResult),
                    ArchiveLocation = BuildResult.IsoPath ?? BuildResult.ArchiveFolderPath,
                    Notes = BuildNotes(BuildResult, VerificationResult),
                }, cancellationToken);

                ProgressText = null;
            }
            else
            {
                ErrorMessage = BuildResult.ErrorMessage ?? "The archive could not be built.";
                ProgressText = null;
            }
        }
        catch (OperationCanceledException)
        {
            ProgressText = null;
            ErrorMessage = "Archive build was cancelled.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Archive build failed for {Game}", game.Title);
            ErrorMessage = "Something went wrong while building the archive.";
            ProgressText = null;
        }
        finally
        {
            IsBuilding = false;
        }
    }

    private void RefreshDefaultSafetyMargin()
    {
        var defaultPlan = discCapacityService.Plan(0, SelectedMediaType.MediaType);
        SafetyMarginMbText = Math.Round(defaultPlan.SafetyMarginBytes / (1024.0 * 1024)).ToString("0");
    }

    private void RecomputeCapacityPreview()
    {
        if (Game is null)
        {
            CapacityPreview = null;
            return;
        }

        var estimatedArchiveBytes = Game.SizeOnDiskBytes ?? 0;
        CapacityPreview = discCapacityService.Plan(estimatedArchiveBytes, SelectedMediaType.MediaType, safetyMarginOverrideBytes: TryParseSafetyMarginBytes());
    }

    private long? TryParseSafetyMarginBytes() =>
        double.TryParse(SafetyMarginMbText, out var megabytes) && megabytes >= 0 ? (long)(megabytes * 1024 * 1024) : null;

    private static ArchiveDestinationType ResolveDestinationType(ArchiveBuildResult result, MediaType requestedMediaType)
    {
        if (result.IsoPath is not null || result.DiscIsoPaths.Count > 0)
        {
            return ArchiveDestinationType.Iso;
        }

        return requestedMediaType is MediaType.FolderOnly or MediaType.IsoOnly
            ? ArchiveDestinationType.Folder
            : ArchiveDestinationType.OpticalDisc;
    }

    private static ArchiveStatus ResolveStatus(ArchiveBuildResult result, VerificationResult? verificationResult)
    {
        if (result.IsoPath is not null || result.DiscIsoPaths.Count > 0)
        {
            return ArchiveStatus.Completed;
        }

        return verificationResult?.Outcome is VerificationOutcome.Verified or VerificationOutcome.VerifiedWithWarnings
            ? ArchiveStatus.Completed
            : ArchiveStatus.Failed;
    }

    private static string BuildNotes(ArchiveBuildResult result, VerificationResult? verificationResult)
    {
        if (result.IsoPath is not null)
        {
            return "ISO created.";
        }

        if (result.DiscIsoPaths.Count > 0)
        {
            var notes = $"{result.DiscIsoPaths.Count} of {result.DiscCount} disc(s) converted to ISO, ready to burn.";
            return result.IsoBuildWarning is not null ? $"{notes} {result.IsoBuildWarning}" : notes;
        }

        return $"Verification: {verificationResult?.Outcome}; discs: {result.DiscCount}";
    }

    partial void OnDestinationFolderChanged(string? value) => BuildCommand.NotifyCanExecuteChanged();

    partial void OnIsBuildingChanged(bool value) => BuildCommand.NotifyCanExecuteChanged();
}
