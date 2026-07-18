using PcGamePreservationStudio.Archiving;

namespace PcGamePreservationStudio.Archiving.Tests;

public sealed class ArchiveFolderNamingTests
{
    [Fact]
    public void ToArchiveFolderName_AppendsArchiveSuffix()
    {
        Assert.Equal("Dota 2_ARCHIVE", ArchiveFolderNaming.ToArchiveFolderName("Dota 2"));
    }

    [Fact]
    public void ToArchiveFolderName_ReplacesInvalidFileNameCharacters()
    {
        var result = ArchiveFolderNaming.ToArchiveFolderName("Devil: Blade / Reboot?");

        foreach (var c in Path.GetInvalidFileNameChars())
        {
            Assert.DoesNotContain(c, result);
        }
    }

    [Fact]
    public void ToArchiveFolderName_FallsBackToPlaceholder_WhenTitleIsEmpty()
    {
        Assert.Equal("GAME_ARCHIVE", ArchiveFolderNaming.ToArchiveFolderName("   "));
    }
}
