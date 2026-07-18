using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.App.ViewModels;

public sealed partial class BurnDiscViewModel : ViewModelBase
{
    private readonly IDiscBurner _discBurner;
    private readonly IArchiveVerificationService _verificationService;
    private readonly ILogger<BurnDiscViewModel> _logger;

    [ObservableProperty]
    private string? _isoPath;

    [ObservableProperty]
    private bool _isRefreshingDrives;

    [ObservableProperty]
    private bool _isBurning;

    [ObservableProperty]
    private OpticalDriveInfo? _selectedDrive;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _progressText;

    [ObservableProperty]
    private DiscBurnResult? _burnResult;

    [ObservableProperty]
    private VerificationResult? _verificationResult;

    public ObservableCollection<OpticalDriveInfo> Drives { get; } = [];

    public BurnDiscViewModel(IDiscBurner discBurner, IArchiveVerificationService verificationService, ILogger<BurnDiscViewModel> logger)
    {
        _discBurner = discBurner;
        _verificationService = verificationService;
        _logger = logger;

        _ = RefreshDrivesAsync();
    }

    [RelayCommand]
    private void BrowseIso()
    {
        var dialog = new OpenFileDialog { Title = "Select an ISO to burn", Filter = "ISO images (*.iso)|*.iso|All files (*.*)|*.*" };
        if (dialog.ShowDialog() == true)
        {
            IsoPath = dialog.FileName;
        }
    }

    [RelayCommand]
    private async Task RefreshDrivesAsync()
    {
        IsRefreshingDrives = true;
        ErrorMessage = null;
        try
        {
            var drives = await _discBurner.GetOpticalDrivesAsync();
            var previouslySelectedId = SelectedDrive?.DriveId;

            Drives.Clear();
            foreach (var drive in drives)
            {
                Drives.Add(drive);
            }

            SelectedDrive = Drives.FirstOrDefault(d => d.DriveId == previouslySelectedId) ?? Drives.FirstOrDefault();

            if (Drives.Count == 0)
            {
                ErrorMessage = "No optical drives were detected. Connect a writable drive and click Refresh.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate optical drives");
            ErrorMessage = "Something went wrong while looking for optical drives.";
        }
        finally
        {
            IsRefreshingDrives = false;
        }
    }

    private bool CanBurn() => !string.IsNullOrWhiteSpace(IsoPath) && SelectedDrive is not null && !IsBurning;

    [RelayCommand(CanExecute = nameof(CanBurn))]
    private async Task BurnAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(IsoPath) || SelectedDrive is null)
        {
            return;
        }

        var isoPath = IsoPath;
        var drive = SelectedDrive;

        IsBurning = true;
        ErrorMessage = null;
        BurnResult = null;
        VerificationResult = null;
        ProgressText = "Burning…";

        try
        {
            var progress = new Progress<double>(p => ProgressText = p >= 1.0 ? "Finalizing…" : "Burning…");
            BurnResult = await _discBurner.BurnIsoAsync(isoPath, drive.DriveId, progress, cancellationToken);

            if (BurnResult.Success)
            {
                ProgressText = "Verifying…";
                var driveRoot = drive.DriveLetter.EndsWith(':') ? drive.DriveLetter + "\\" : drive.DriveLetter;
                VerificationResult = await _verificationService.VerifyAsync(driveRoot, cancellationToken);
                ProgressText = null;
            }
            else
            {
                ErrorMessage = BurnResult.ErrorMessage ?? "The disc could not be burned.";
                ProgressText = null;
            }
        }
        catch (OperationCanceledException)
        {
            ProgressText = null;
            ErrorMessage = "Burning was cancelled.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Burning failed for {IsoPath}", isoPath);
            ErrorMessage = "Something went wrong while burning the disc.";
            ProgressText = null;
        }
        finally
        {
            IsBurning = false;
        }
    }

    partial void OnIsoPathChanged(string? value) => BurnCommand.NotifyCanExecuteChanged();

    partial void OnSelectedDriveChanged(OpticalDriveInfo? value) => BurnCommand.NotifyCanExecuteChanged();

    partial void OnIsBurningChanged(bool value) => BurnCommand.NotifyCanExecuteChanged();
}
