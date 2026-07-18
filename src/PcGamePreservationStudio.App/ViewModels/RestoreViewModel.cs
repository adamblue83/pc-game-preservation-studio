using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.App.ViewModels;

public sealed partial class RestoreViewModel : ViewModelBase
{
    private readonly IRestoreService _restoreService;
    private readonly ILogger<RestoreViewModel> _logger;

    [ObservableProperty]
    private string? _archiveSourcePath;

    [ObservableProperty]
    private bool _isLoadingPreflight;

    [ObservableProperty]
    private RestorePreflightResult? _preflight;

    [ObservableProperty]
    private string? _installDestinationPath;

    [ObservableProperty]
    private bool _overwriteExistingFiles;

    [ObservableProperty]
    private bool _isRestoring;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _progressText;

    [ObservableProperty]
    private RestoreResult? _restoreResult;

    public ObservableCollection<RestoreSaveLocationOptionViewModel> SaveLocationOptions { get; } = [];

    public RestoreViewModel(IRestoreService restoreService, ILogger<RestoreViewModel> logger)
    {
        _restoreService = restoreService;
        _logger = logger;
    }

    public void LoadWithArchivePath(string archiveSourcePath)
    {
        ArchiveSourcePath = archiveSourcePath;
        _ = RunPreflightAsync();
    }

    [RelayCommand]
    private void BrowseArchiveSource()
    {
        var dialog = new OpenFolderDialog { Title = "Select the archive folder to restore" };
        if (dialog.ShowDialog() == true)
        {
            ArchiveSourcePath = dialog.FolderName;
            _ = RunPreflightAsync();
        }
    }

    [RelayCommand]
    private void BrowseInstallDestination()
    {
        var dialog = new OpenFolderDialog { Title = "Select where to restore game files" };
        if (dialog.ShowDialog() == true)
        {
            InstallDestinationPath = dialog.FolderName;
        }
    }

    private async Task RunPreflightAsync()
    {
        if (string.IsNullOrWhiteSpace(ArchiveSourcePath))
        {
            return;
        }

        IsLoadingPreflight = true;
        ErrorMessage = null;
        Preflight = null;
        RestoreResult = null;
        SaveLocationOptions.Clear();

        try
        {
            var result = await _restoreService.PreflightAsync(ArchiveSourcePath);
            Preflight = result;

            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage ?? "This archive could not be read.";
            }
            else
            {
                foreach (var saveLocation in result.SaveLocations)
                {
                    SaveLocationOptions.Add(new RestoreSaveLocationOptionViewModel(saveLocation));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preflight archive {Path}", ArchiveSourcePath);
            ErrorMessage = "Something went wrong while reading this archive.";
        }
        finally
        {
            IsLoadingPreflight = false;
            RestoreCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanRestore() =>
        Preflight is { Success: true }
        && !IsRestoring
        && (!(Preflight.HasGameFiles || Preflight.HasInstallerFiles) || !string.IsNullOrWhiteSpace(InstallDestinationPath));

    [RelayCommand(CanExecute = nameof(CanRestore))]
    private async Task RestoreAsync(CancellationToken cancellationToken)
    {
        if (Preflight is not { Success: true } || string.IsNullOrWhiteSpace(ArchiveSourcePath))
        {
            return;
        }

        var archiveSourcePath = ArchiveSourcePath;

        IsRestoring = true;
        ErrorMessage = null;
        RestoreResult = null;
        ProgressText = "Starting…";

        try
        {
            var request = new RestoreRequest
            {
                ArchiveSourcePath = archiveSourcePath,
                InstallDestinationPath = InstallDestinationPath,
                SaveLocationsToRestore = SaveLocationOptions
                    .Where(s => s.IsIncluded)
                    .Select(s => new RestoreSaveLocationSelection { ArchiveFolderName = s.ArchiveFolderName, RestoreToPath = s.RestoreToPath })
                    .ToList(),
                OverwriteExistingFiles = OverwriteExistingFiles,
            };

            var progress = new Progress<RestoreProgress>(p =>
            {
                ProgressText = p.Stage switch
                {
                    RestoreStage.Verifying => "Verifying archive…",
                    RestoreStage.CopyingGameFiles => $"Restoring game files… {p.CurrentItem}",
                    RestoreStage.CopyingInstallerFiles => $"Restoring installer files… {p.CurrentItem}",
                    RestoreStage.CopyingPlatformFiles => $"Restoring platform files… {p.CurrentItem}",
                    RestoreStage.CopyingSaves => $"Restoring saves… {p.CurrentItem}",
                    RestoreStage.Completed => "Restore complete",
                    _ => ProgressText,
                };
            });

            RestoreResult = await _restoreService.RestoreAsync(request, progress, cancellationToken);
            if (!RestoreResult.Success)
            {
                ErrorMessage = RestoreResult.ErrorMessage ?? "The archive could not be restored.";
            }

            ProgressText = null;
        }
        catch (OperationCanceledException)
        {
            ProgressText = null;
            ErrorMessage = "Restore was cancelled.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore failed for {Path}", archiveSourcePath);
            ErrorMessage = "Something went wrong while restoring this archive.";
            ProgressText = null;
        }
        finally
        {
            IsRestoring = false;
        }
    }

    partial void OnInstallDestinationPathChanged(string? value) => RestoreCommand.NotifyCanExecuteChanged();

    partial void OnIsRestoringChanged(bool value) => RestoreCommand.NotifyCanExecuteChanged();
}
