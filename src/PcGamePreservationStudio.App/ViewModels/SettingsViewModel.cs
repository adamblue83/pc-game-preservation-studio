using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.IO;
using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.App.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SettingsViewModel> _logger;
    private bool _hasCompletedFirstRun;

    [ObservableProperty]
    private string? _steamInstallPathOverride;

    [ObservableProperty]
    private string? _defaultArchiveOutputFolder;

    [ObservableProperty]
    private string? _oscdimgPathOverride;

    [ObservableProperty]
    private bool _skipPlatformDetection;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string? _statusMessage;

    public SettingsViewModel(ISettingsService settingsService, ILogger<SettingsViewModel> logger)
    {
        _settingsService = settingsService;
        _logger = logger;

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var settings = await _settingsService.LoadAsync();
        SteamInstallPathOverride = settings.SteamInstallPathOverride;
        DefaultArchiveOutputFolder = settings.DefaultArchiveOutputFolder;
        OscdimgPathOverride = settings.OscdimgPathOverride;
        SkipPlatformDetection = settings.SkipPlatformDetection;
        _hasCompletedFirstRun = settings.HasCompletedFirstRun;
    }

    [RelayCommand]
    private void BrowseSteamPath()
    {
        var dialog = new OpenFolderDialog { Title = "Select your Steam installation folder" };
        if (dialog.ShowDialog() == true)
        {
            SteamInstallPathOverride = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void BrowseOutputFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Select the default archive output folder" };
        if (dialog.ShowDialog() == true)
        {
            DefaultArchiveOutputFolder = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void BrowseOscdimgPath()
    {
        var dialog = new OpenFileDialog { Title = "Select oscdimg.exe", Filter = "oscdimg.exe|oscdimg.exe|Executable files (*.exe)|*.exe" };
        if (dialog.ShowDialog() == true)
        {
            OscdimgPathOverride = dialog.FileName;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsSaving = true;
        StatusMessage = null;
        try
        {
            await _settingsService.SaveAsync(new AppSettings
            {
                SteamInstallPathOverride = SteamInstallPathOverride,
                DefaultArchiveOutputFolder = DefaultArchiveOutputFolder,
                OscdimgPathOverride = OscdimgPathOverride,
                SkipPlatformDetection = SkipPlatformDetection,
                HasCompletedFirstRun = _hasCompletedFirstRun,
            });
            StatusMessage = "Settings saved.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Failed to save settings");
            StatusMessage = "Something went wrong while saving settings.";
        }
        finally
        {
            IsSaving = false;
        }
    }
}
