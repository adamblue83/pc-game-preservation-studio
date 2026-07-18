using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcGamePreservationStudio.Core.Abstractions;

namespace PcGamePreservationStudio.App.ViewModels;

public sealed partial class FirstRunViewModel(ISettingsService settingsService) : ViewModelBase
{
    [ObservableProperty]
    private bool _skipPlatformDetection;

    [ObservableProperty]
    private bool _isSaving;

    public event Action? Completed;

    [RelayCommand]
    private async Task ContinueAsync()
    {
        IsSaving = true;
        try
        {
            var existing = await settingsService.LoadAsync();
            await settingsService.SaveAsync(existing with
            {
                HasCompletedFirstRun = true,
                SkipPlatformDetection = SkipPlatformDetection,
            });
        }
        finally
        {
            IsSaving = false;
            Completed?.Invoke();
        }
    }
}
