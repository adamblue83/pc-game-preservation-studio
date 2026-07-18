using Microsoft.Extensions.Logging.Abstractions;
using PcGamePreservationStudio.Archiving;
using PcGamePreservationStudio.Core.Models;
using PcGamePreservationStudio.Media;

namespace PcGamePreservationStudio.Archiving.Tests;

/// <summary>Covers building an archive from loose installer files with no installed-game directory
/// (e.g. a GOG offline installer set) — Installers\ is populated, Game\ never gets created.</summary>
public sealed class ArchiveBuilderInstallerOnlyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"pgps-installeronly-{Guid.NewGuid():N}");

    private string SourceDir => Path.Combine(_root, "Downloads");

    private string DestinationDir => Path.Combine(_root, "Destination");

    private async Task<(GameLibraryEntry Game, List<string> InstallerPaths)> CreateInstallerFilesAsync()
    {
        Directory.CreateDirectory(SourceDir);
        Directory.CreateDirectory(DestinationDir);

        var exePath = Path.Combine(SourceDir, "setup_my_game_1.0.exe");
        var binPath = Path.Combine(SourceDir, "setup_my_game_1.0-1.bin");
        await File.WriteAllTextAsync(exePath, "fake installer");
        await File.WriteAllTextAsync(binPath, "fake installer data");

        var game = new GameLibraryEntry
        {
            Id = Guid.NewGuid(),
            Platform = GamePlatform.Gog,
            Title = "My Game",
            InstallPath = string.Empty,
        };

        return (game, [exePath, binPath]);
    }

    [Fact]
    public async Task BuildAsync_PopulatesInstallersFolder_AndNeverCreatesGameFolder()
    {
        var (game, installerPaths) = await CreateInstallerFilesAsync();
        var builder = new ArchiveBuilder(NullLogger<ArchiveBuilder>.Instance, new DiscCapacityService(), new NoOpIsoBuilder());

        var result = await builder.BuildAsync(new ArchiveBuildRequest
        {
            Game = game,
            DestinationFolder = DestinationDir,
            InstallerFilePaths = installerPaths,
        });

        Assert.True(result.Success);
        Assert.Equal(2, result.TotalFiles);
        Assert.True(File.Exists(Path.Combine(result.ArchiveFolderPath, "Installers", "setup_my_game_1.0.exe")));
        Assert.True(File.Exists(Path.Combine(result.ArchiveFolderPath, "Installers", "setup_my_game_1.0-1.bin")));
        Assert.False(Directory.Exists(Path.Combine(result.ArchiveFolderPath, "Game")));
    }

    [Fact]
    public async Task BuildAsync_RatesExcellent_ForAnInstallerOnlyGogArchive()
    {
        var (game, installerPaths) = await CreateInstallerFilesAsync();
        var builder = new ArchiveBuilder(NullLogger<ArchiveBuilder>.Instance, new DiscCapacityService(), new NoOpIsoBuilder());

        var result = await builder.BuildAsync(new ArchiveBuildRequest
        {
            Game = game,
            DestinationFolder = DestinationDir,
            InstallerFilePaths = installerPaths,
        });

        Assert.Equal(PreservationRating.Excellent, result.Preservation?.Rating);
    }

    [Fact]
    public async Task BuildAsync_VerifiesSuccessfully_ForAnInstallerOnlyArchive()
    {
        var (game, installerPaths) = await CreateInstallerFilesAsync();
        var builder = new ArchiveBuilder(NullLogger<ArchiveBuilder>.Instance, new DiscCapacityService(), new NoOpIsoBuilder());

        var result = await builder.BuildAsync(new ArchiveBuildRequest
        {
            Game = game,
            DestinationFolder = DestinationDir,
            InstallerFilePaths = installerPaths,
        });

        var verificationService = new ArchiveVerificationService(NullLogger<ArchiveVerificationService>.Instance);
        var verification = await verificationService.VerifyAsync(result.ArchiveFolderPath);

        Assert.Equal(VerificationOutcome.Verified, verification.Outcome);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
