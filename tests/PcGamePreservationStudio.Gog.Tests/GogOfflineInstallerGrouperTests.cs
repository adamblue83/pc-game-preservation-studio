using PcGamePreservationStudio.Platforms.Gog;

namespace PcGamePreservationStudio.Gog.Tests;

public sealed class GogOfflineInstallerGrouperTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"pgps-gog-{Guid.NewGuid():N}");

    private void CreateFile(string name, int sizeBytes = 10)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(Path.Combine(_root, name), new byte[sizeBytes]);
    }

    [Fact]
    public void GroupInstallers_ReturnsEmpty_WhenFolderDoesNotExist()
    {
        var groups = GogOfflineInstallerGrouper.GroupInstallers(Path.Combine(_root, "does-not-exist"));

        Assert.Empty(groups);
    }

    [Fact]
    public void GroupInstallers_GroupsSetupExeWithItsNumberedBinParts()
    {
        CreateFile("setup_my_game_1.0.exe");
        CreateFile("setup_my_game_1.0-1.bin");
        CreateFile("setup_my_game_1.0-2.bin");

        var groups = GogOfflineInstallerGrouper.GroupInstallers(_root);

        var group = Assert.Single(groups);
        Assert.Equal("setup_my_game_1.0", group.GroupKey);
        Assert.Equal(3, group.Files.Count);
        Assert.Equal(GogInstallerGroupKind.BaseGame, group.Kind);
    }

    [Fact]
    public void GroupInstallers_KeepsDlcInstallerSeparateFromBaseGame()
    {
        CreateFile("setup_my_game_1.0.exe");
        CreateFile("setup_my_game_1.0-1.bin");
        CreateFile("setup_my_game_expansion_dlc_1.0.exe");

        var groups = GogOfflineInstallerGrouper.GroupInstallers(_root);

        Assert.Equal(2, groups.Count);
        var baseGroup = Assert.Single(groups, g => g.Kind == GogInstallerGroupKind.BaseGame);
        var dlcGroup = Assert.Single(groups, g => g.Kind == GogInstallerGroupKind.Dlc);
        Assert.Equal(2, baseGroup.Files.Count);
        Assert.Single(dlcGroup.Files);
    }

    [Theory]
    [InlineData("game_patch_1.0_to_1.1.exe", GogInstallerGroupKind.Patch)]
    [InlineData("game_update_1.1.exe", GogInstallerGroupKind.Patch)]
    [InlineData("game_soundtrack.mp3", GogInstallerGroupKind.Soundtrack)]
    [InlineData("game_ost.flac", GogInstallerGroupKind.Soundtrack)]
    [InlineData("game_manual.pdf", GogInstallerGroupKind.Manual)]
    public void GroupInstallers_ClassifiesGroupsByFilenameKeywords(string fileName, GogInstallerGroupKind expectedKind)
    {
        CreateFile(fileName);

        var groups = GogOfflineInstallerGrouper.GroupInstallers(_root);

        var group = Assert.Single(groups);
        Assert.Equal(expectedKind, group.Kind);
    }

    [Fact]
    public void GroupInstallers_ClassifiesNonExecutableWithoutKeywordsAsExtra()
    {
        CreateFile("readme.txt");

        var groups = GogOfflineInstallerGrouper.GroupInstallers(_root);

        var group = Assert.Single(groups);
        Assert.Equal(GogInstallerGroupKind.Extra, group.Kind);
    }

    [Fact]
    public void GroupInstallers_DoesNotMergeUnrelatedFilesWithDifferentBaseNames()
    {
        CreateFile("setup_game_one_1.0.exe");
        CreateFile("setup_game_two_1.0.exe");

        var groups = GogOfflineInstallerGrouper.GroupInstallers(_root);

        Assert.Equal(2, groups.Count);
        Assert.All(groups, g => Assert.Equal(GogInstallerGroupKind.BaseGame, g.Kind));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
