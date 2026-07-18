using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PcGamePreservationStudio.App.Services;
using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;
using PcGamePreservationStudio.Platforms.Gog;

namespace PcGamePreservationStudio.App.ViewModels;

public sealed partial class GogInstallerArchiveViewModel(
    IArchiveBuilder archiveBuilder,
    IArchiveVerificationService verificationService,
    IDiscCapacityService discCapacityService,
    IArchiveCatalogRepository catalogRepository,
    ISettingsService settingsService,
    INavigationService navigationService,
    ILogger<GogInstallerArchiveViewModel> logger) : ViewModelBase
{
    [ObservableProperty]
    private string? _sourceFolder;

    [ObservableProperty]
    private string? _gameTitle;

    [ObservableProperty]
    private bool _isScanning;

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

    [ObservableProperty]
    private MediaTypeOption _selectedMediaType = MediaTypeOptionsCatalog.Default;

    [ObservableProperty]
    private string? _safetyMarginMbText;

    [ObservableProperty]
    private MediaCapacityPlan? _capacityPreview;

    public IReadOnlyList<MediaTypeOption> MediaTypeOptions => MediaTypeOptionsCatalog.All;

    public ObservableCollection<GogInstallerGroupOptionViewModel> DetectedGroups { get; } = [];

    partial void OnSelectedMediaTypeChanged(MediaTypeOption value)
    {
        RefreshDefaultSafetyMargin();
        RecomputeCapacityPreview();
    }

    partial void OnSafetyMarginMbTextChanged(string? value) => RecomputeCapacityPreview();

    [RelayCommand]
    private void GoToLibrary() => navigationService.NavigateTo(NavigationSection.Library);

    [RelayCommand]
    private void BrowseSourceFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Select the folder containing your GOG offline installer files" };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        SourceFolder = dialog.FolderName;
        ScanSourceFolder();
    }

    private void ScanSourceFolder()
    {
        if (string.IsNullOrWhiteSpace(SourceFolder))
        {
            return;
        }

        IsScanning = true;
        ErrorMessage = null;
        DetectedGroups.Clear();

        try
        {
            foreach (var group in GogOfflineInstallerGrouper.GroupInstallers(SourceFolder))
            {
                DetectedGroups.Add(new GogInstallerGroupOptionViewModel(group));
            }

            if (DetectedGroups.Count == 0)
            {
                ErrorMessage = "No installer files were found in that folder.";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to scan GOG installer folder {Folder}", SourceFolder);
            ErrorMessage = "Something went wrong while scanning that folder.";
        }
        finally
        {
            IsScanning = false;
        }

        RecomputeCapacityPreview();
        BuildCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void BrowseDestination()
    {
        var dialog = new OpenFolderDialog { Title = "Select where to create the archive" };
        if (dialog.ShowDialog() == true)
        {
            DestinationFolder = dialog.FolderName;
        }
    }

    private bool CanBuild() =>
        !string.IsNullOrWhiteSpace(GameTitle)
        && !string.IsNullOrWhiteSpace(DestinationFolder)
        && DetectedGroups.Any(g => g.IsIncluded)
        && !IsBuilding;

    [RelayCommand(CanExecute = nameof(CanBuild))]
    private async Task BuildAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(GameTitle) || string.IsNullOrWhiteSpace(DestinationFolder))
        {
            return;
        }

        var gameTitle = GameTitle;
        var destinationFolder = DestinationFolder;
        var mediaType = SelectedMediaType.MediaType;
        var installerFilePaths = DetectedGroups
            .Where(g => g.IsIncluded)
            .SelectMany(g => g.Group.Files.Select(f => f.FilePath))
            .ToList();

        IsBuilding = true;
        ErrorMessage = null;
        BuildResult = null;
        VerificationResult = null;
        ProgressText = "Starting…";

        try
        {
            var game = new GameLibraryEntry
            {
                Id = Guid.NewGuid(),
                Platform = GamePlatform.Gog,
                Title = gameTitle,
                InstallPath = string.Empty,
            };

            var settings = await settingsService.LoadAsync(cancellationToken);

            var request = new ArchiveBuildRequest
            {
                Game = game,
                DestinationFolder = destinationFolder,
                InstallerFilePaths = installerFilePaths,
                MediaType = mediaType,
                SafetyMarginOverrideBytes = TryParseSafetyMarginBytes(),
                OscdimgPathOverride = settings.OscdimgPathOverride,
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
                if (BuildResult.IsoPath is not null)
                {
                    // The staged folder was consumed into the ISO; folder-based verification no
                    // longer applies here (ISO-level verification is a later phase).
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
                    GameTitle = gameTitle,
                    Platform = GamePlatform.Gog,
                    CreatedUtc = DateTimeOffset.UtcNow,
                    DestinationType = ResolveDestinationType(BuildResult, mediaType),
                    Status = ResolveStatus(BuildResult, VerificationResult),
                    ArchiveLocation = BuildResult.IsoPath ?? BuildResult.ArchiveFolderPath,
                    Notes = BuildResult.IsoPath is not null
                        ? "GOG offline installer archive. ISO created."
                        : $"GOG offline installer archive. Verification: {VerificationResult?.Outcome}; discs: {BuildResult.DiscCount}",
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
            logger.LogError(ex, "GOG installer archive build failed for {GameTitle}", gameTitle);
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
        var estimatedBytes = DetectedGroups.Where(g => g.IsIncluded).Sum(g => g.Group.TotalBytes);
        CapacityPreview = discCapacityService.Plan(estimatedBytes, SelectedMediaType.MediaType, safetyMarginOverrideBytes: TryParseSafetyMarginBytes());
    }

    private long? TryParseSafetyMarginBytes() =>
        double.TryParse(SafetyMarginMbText, out var megabytes) && megabytes >= 0 ? (long)(megabytes * 1024 * 1024) : null;

    private static ArchiveDestinationType ResolveDestinationType(ArchiveBuildResult result, MediaType requestedMediaType)
    {
        if (result.IsoPath is not null)
        {
            return ArchiveDestinationType.Iso;
        }

        return requestedMediaType is MediaType.FolderOnly or MediaType.IsoOnly
            ? ArchiveDestinationType.Folder
            : ArchiveDestinationType.OpticalDisc;
    }

    private static ArchiveStatus ResolveStatus(ArchiveBuildResult result, VerificationResult? verificationResult)
    {
        if (result.IsoPath is not null)
        {
            return ArchiveStatus.Completed;
        }

        return verificationResult?.Outcome is VerificationOutcome.Verified or VerificationOutcome.VerifiedWithWarnings
            ? ArchiveStatus.Completed
            : ArchiveStatus.Failed;
    }

    partial void OnGameTitleChanged(string? value) => BuildCommand.NotifyCanExecuteChanged();

    partial void OnDestinationFolderChanged(string? value) => BuildCommand.NotifyCanExecuteChanged();

    partial void OnIsBuildingChanged(bool value) => BuildCommand.NotifyCanExecuteChanged();
}
