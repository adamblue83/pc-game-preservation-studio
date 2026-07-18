using Microsoft.Extensions.Logging.Abstractions;
using PcGamePreservationStudio.Archiving;
using PcGamePreservationStudio.Core.Models;
using PcGamePreservationStudio.Media;

namespace PcGamePreservationStudio.Archiving.Tests;

public sealed class RestoreServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"pgps-restore-{Guid.NewGuid():N}");

    private string SourceDir => Path.Combine(_root, "Source", "MyGame");

    private string ArchiveDestinationDir => Path.Combine(_root, "ArchiveDestination");

    private string SaveDir => Path.Combine(_root, "Saves", "MyGameSave");

    private string RestoreInstallDir => Path.Combine(_root, "RestoreInstall");

    private async Task<string> BuildArchiveAsync(bool includeSave = true)
    {
        Directory.CreateDirectory(SourceDir);
        Directory.CreateDirectory(Path.Combine(SourceDir, "bin"));
        await File.WriteAllTextAsync(Path.Combine(SourceDir, "game.exe"), "fake executable contents");
        await File.WriteAllTextAsync(Path.Combine(SourceDir, "bin", "data.pak"), "fake game data");
        Directory.CreateDirectory(ArchiveDestinationDir);

        var game = new GameLibraryEntry
        {
            Id = Guid.NewGuid(),
            Platform = GamePlatform.LocalFolder,
            Title = "My Game",
            InstallPath = SourceDir,
        };

        var request = new ArchiveBuildRequest { Game = game, DestinationFolder = ArchiveDestinationDir };

        if (includeSave)
        {
            Directory.CreateDirectory(SaveDir);
            await File.WriteAllTextAsync(Path.Combine(SaveDir, "save1.dat"), "save data");
            request = request with { IncludedSaveLocationPaths = [SaveDir] };
        }

        var builder = new ArchiveBuilder(NullLogger<ArchiveBuilder>.Instance, new DiscCapacityService(), new NoOpIsoBuilder());
        var result = await builder.BuildAsync(request);
        Assert.True(result.Success);
        return result.ArchiveFolderPath;
    }

    private static RestoreService CreateRestoreService() =>
        new(new ArchiveVerificationService(NullLogger<ArchiveVerificationService>.Instance), NullLogger<RestoreService>.Instance);

    [Fact]
    public async Task PreflightAsync_ReadsManifestAndSaveLocations()
    {
        var archivePath = await BuildArchiveAsync();
        var service = CreateRestoreService();

        var preflight = await service.PreflightAsync(archivePath);

        Assert.True(preflight.Success);
        Assert.NotNull(preflight.Manifest);
        Assert.Equal("My Game", preflight.Manifest!.Title);
        Assert.True(preflight.HasGameFiles);
        Assert.False(preflight.HasInstallerFiles);
        var saveLocation = Assert.Single(preflight.SaveLocations);
        Assert.Equal("MyGameSave", saveLocation.ArchiveFolderName);
        Assert.Equal(SaveDir, saveLocation.OriginalFullPath);
    }

    [Fact]
    public async Task PreflightAsync_ReturnsFailure_WhenManifestIsMissing()
    {
        var service = CreateRestoreService();
        var emptyFolder = Path.Combine(_root, "NotAnArchive");
        Directory.CreateDirectory(emptyFolder);

        var preflight = await service.PreflightAsync(emptyFolder);

        Assert.False(preflight.Success);
        Assert.NotNull(preflight.ErrorMessage);
    }

    [Fact]
    public async Task RestoreAsync_CopiesGameFilesAndSaves_WhenArchiveIsIntact()
    {
        var archivePath = await BuildArchiveAsync();
        var service = CreateRestoreService();
        var preflight = await service.PreflightAsync(archivePath);
        var saveLocation = preflight.SaveLocations[0];

        var result = await service.RestoreAsync(new RestoreRequest
        {
            ArchiveSourcePath = archivePath,
            InstallDestinationPath = RestoreInstallDir,
            SaveLocationsToRestore = [new RestoreSaveLocationSelection { ArchiveFolderName = saveLocation.ArchiveFolderName, RestoreToPath = saveLocation.OriginalFullPath }],
        });

        Assert.True(result.Success);
        Assert.Equal(VerificationOutcome.Verified, result.VerificationResult?.Outcome);
        Assert.True(File.Exists(Path.Combine(RestoreInstallDir, "game.exe")));
        Assert.True(File.Exists(Path.Combine(RestoreInstallDir, "bin", "data.pak")));
        Assert.Equal("save data", await File.ReadAllTextAsync(Path.Combine(SaveDir, "save1.dat")));
    }

    [Fact]
    public async Task RestoreAsync_RefusesToRestore_WhenArchiveHasBeenTampered()
    {
        var archivePath = await BuildArchiveAsync();
        await File.WriteAllTextAsync(Path.Combine(archivePath, "Game", "game.exe"), "tampered contents");
        var service = CreateRestoreService();

        var result = await service.RestoreAsync(new RestoreRequest { ArchiveSourcePath = archivePath, InstallDestinationPath = RestoreInstallDir });

        Assert.False(result.Success);
        Assert.Equal(VerificationOutcome.Failed, result.VerificationResult?.Outcome);
        Assert.False(Directory.Exists(RestoreInstallDir) && Directory.EnumerateFileSystemEntries(RestoreInstallDir).Any());
    }

    [Fact]
    public async Task RestoreAsync_SkipsExistingFiles_WhenOverwriteNotRequested()
    {
        var archivePath = await BuildArchiveAsync(includeSave: false);
        Directory.CreateDirectory(RestoreInstallDir);
        await File.WriteAllTextAsync(Path.Combine(RestoreInstallDir, "game.exe"), "pre-existing file, should not be overwritten");

        var service = CreateRestoreService();
        var result = await service.RestoreAsync(new RestoreRequest
        {
            ArchiveSourcePath = archivePath,
            InstallDestinationPath = RestoreInstallDir,
            OverwriteExistingFiles = false,
        });

        Assert.True(result.Success);
        Assert.Single(result.SkippedExistingFiles);
        Assert.Equal("pre-existing file, should not be overwritten", await File.ReadAllTextAsync(Path.Combine(RestoreInstallDir, "game.exe")));
        Assert.True(File.Exists(Path.Combine(RestoreInstallDir, "bin", "data.pak")));
    }

    [Fact]
    public async Task RestoreAsync_OverwritesExistingFiles_WhenRequested()
    {
        var archivePath = await BuildArchiveAsync(includeSave: false);
        Directory.CreateDirectory(RestoreInstallDir);
        await File.WriteAllTextAsync(Path.Combine(RestoreInstallDir, "game.exe"), "stale contents");

        var service = CreateRestoreService();
        var result = await service.RestoreAsync(new RestoreRequest
        {
            ArchiveSourcePath = archivePath,
            InstallDestinationPath = RestoreInstallDir,
            OverwriteExistingFiles = true,
        });

        Assert.True(result.Success);
        Assert.Empty(result.SkippedExistingFiles);
        Assert.Equal("fake executable contents", await File.ReadAllTextAsync(Path.Combine(RestoreInstallDir, "game.exe")));
    }

    [Fact]
    public async Task RestoreAsync_RestoresInstallerOnlyArchive_ToInstallDestination()
    {
        var installerSourceDir = Path.Combine(_root, "InstallerSource");
        Directory.CreateDirectory(installerSourceDir);
        var installerFile = Path.Combine(installerSourceDir, "setup_my_game.exe");
        await File.WriteAllTextAsync(installerFile, "fake installer");
        Directory.CreateDirectory(ArchiveDestinationDir);

        var game = new GameLibraryEntry { Id = Guid.NewGuid(), Platform = GamePlatform.Gog, Title = "My GOG Game", InstallPath = string.Empty };
        var builder = new ArchiveBuilder(NullLogger<ArchiveBuilder>.Instance, new DiscCapacityService(), new NoOpIsoBuilder());
        var buildResult = await builder.BuildAsync(new ArchiveBuildRequest
        {
            Game = game,
            DestinationFolder = ArchiveDestinationDir,
            InstallerFilePaths = [installerFile],
        });
        Assert.True(buildResult.Success);

        var service = CreateRestoreService();
        var preflight = await service.PreflightAsync(buildResult.ArchiveFolderPath);
        Assert.True(preflight.HasInstallerFiles);
        Assert.False(preflight.HasGameFiles);

        var result = await service.RestoreAsync(new RestoreRequest
        {
            ArchiveSourcePath = buildResult.ArchiveFolderPath,
            InstallDestinationPath = RestoreInstallDir,
        });

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(RestoreInstallDir, "setup_my_game.exe")));
        Assert.Contains(result.Notes, n => n.Contains("installer", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
