using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.App.ViewModels;

public sealed partial class VerifyDiscViewModel : ViewModelBase
{
    private readonly IDiscBurner _discBurner;
    private readonly IArchiveVerificationService _verificationService;
    private readonly ILogger<VerifyDiscViewModel> _logger;

    [ObservableProperty]
    private bool _isRefreshingDrives;

    [ObservableProperty]
    private bool _isVerifying;

    [ObservableProperty]
    private OpticalDriveInfo? _selectedDrive;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private VerificationResult? _verificationResult;

    public ObservableCollection<OpticalDriveInfo> Drives { get; } = [];

    public VerifyDiscViewModel(IDiscBurner discBurner, IArchiveVerificationService verificationService, ILogger<VerifyDiscViewModel> logger)
    {
        _discBurner = discBurner;
        _verificationService = verificationService;
        _logger = logger;

        _ = RefreshDrivesAsync();
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
                ErrorMessage = "No optical drives were detected. Connect a drive and click Refresh.";
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

    private bool CanVerify() => SelectedDrive is not null && SelectedDrive.IsMediaPresent && !IsVerifying;

    [RelayCommand(CanExecute = nameof(CanVerify))]
    private async Task VerifyAsync(CancellationToken cancellationToken)
    {
        if (SelectedDrive is null)
        {
            return;
        }

        var drive = SelectedDrive;

        IsVerifying = true;
        ErrorMessage = null;
        VerificationResult = null;

        try
        {
            var driveRoot = drive.DriveLetter.EndsWith(':') ? drive.DriveLetter + "\\" : drive.DriveLetter;
            VerificationResult = await _verificationService.VerifyAsync(driveRoot, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Verification was cancelled.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Verification failed for drive {DriveId}", drive.DriveId);
            ErrorMessage = "Something went wrong while verifying this disc.";
        }
        finally
        {
            IsVerifying = false;
        }
    }

    partial void OnSelectedDriveChanged(OpticalDriveInfo? value) => VerifyCommand.NotifyCanExecuteChanged();

    partial void OnIsVerifyingChanged(bool value) => VerifyCommand.NotifyCanExecuteChanged();
}
