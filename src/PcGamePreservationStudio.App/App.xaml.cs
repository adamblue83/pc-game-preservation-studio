using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PcGamePreservationStudio.Analysis;
using PcGamePreservationStudio.App.Services;
using PcGamePreservationStudio.App.ViewModels;
using PcGamePreservationStudio.App.Views;
using PcGamePreservationStudio.Archiving;
using PcGamePreservationStudio.Burning;
using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Infrastructure;
using PcGamePreservationStudio.Media;
using PcGamePreservationStudio.Persistence;
using GogPlatform = PcGamePreservationStudio.Platforms.Gog.GogRegistryGameProvider;
using SteamPlatform = PcGamePreservationStudio.Platforms.Steam.SteamGamePlatformProvider;

namespace PcGamePreservationStudio.App;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Prevent the app from shutting down when the first-run dialog closes, before MainWindow exists.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.AddLogging(builder => builder.AddPcGamePreservationStudioLogging());

                services.AddSingleton<ISettingsService, JsonSettingsService>();
                services.AddSingleton<IArchiveCatalogRepository, SqliteArchiveCatalogRepository>();
                services.AddSingleton<ILocalFolderSourceRepository, JsonLocalFolderSourceRepository>();

                services.AddSingleton<IGamePlatformProvider, SteamPlatform>();
                services.AddSingleton<IGamePlatformProvider, GogPlatform>();
                services.AddSingleton<IGamePlatformProvider, LocalFolderGamePlatformProvider>();
                services.AddSingleton<IGameDetectionService, GameDetectionService>();

                services.AddSingleton<ISaveDetectionService, SaveDetectionService>();
                services.AddSingleton<IDiscCapacityService, DiscCapacityService>();
                services.AddSingleton<IIsoBuilder, OscdimgIsoBuilder>();
                services.AddSingleton<IDiscBurner, Imapi2DiscBurner>();
                services.AddSingleton<IArchiveBuilder, ArchiveBuilder>();
                services.AddSingleton<IArchiveVerificationService, ArchiveVerificationService>();
                services.AddSingleton<IRestoreService, RestoreService>();
                services.AddSingleton<IDrmAnalysisService, DrmAnalysisService>();

                services.AddSingleton<MainViewModel>();
                services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<MainViewModel>());
                services.AddTransient<LibraryViewModel>();
                services.AddTransient<GameDetailViewModel>();
                services.AddTransient<ArchivesViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<AboutViewModel>();
                services.AddTransient<FirstRunViewModel>();
                services.AddTransient<CreateArchiveViewModel>();
                services.AddTransient<GogInstallerArchiveViewModel>();
                services.AddTransient<BurnDiscViewModel>();
                services.AddTransient<VerifyDiscViewModel>();
                services.AddTransient<RestoreViewModel>();

                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.StartAsync();

        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("PC Game Preservation Studio starting up");

        var settingsService = _host.Services.GetRequiredService<ISettingsService>();
        var settings = await settingsService.LoadAsync();

        if (!settings.HasCompletedFirstRun)
        {
            var firstRunViewModel = _host.Services.GetRequiredService<FirstRunViewModel>();
            var firstRunWindow = new FirstRunWindow { DataContext = firstRunViewModel };
            firstRunViewModel.Completed += () => firstRunWindow.Close();
            firstRunWindow.ShowDialog();
        }

        var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
        mainViewModel.Initialize();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
