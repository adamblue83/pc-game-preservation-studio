using PcGamePreservationStudio.Platforms.Steam;

namespace PcGamePreservationStudio.Steam.Tests;

public sealed class SteamVdfParserTests
{
    private const string LibraryFoldersVdf = """
        "libraryfolders"
        {
        	"0"
        	{
        		"path"		"C:\\Program Files (x86)\\Steam"
        		"label"		""
        		"contentid"		"1234567890123456789"
        		"totalsize"		"0"
        		"apps"
        		{
        			"228980"		"12345"
        		}
        	}
        	"1"
        	{
        		"path"		"D:\\SteamLibrary"
        		"label"		""
        		"contentid"		"9876543210987654321"
        		"totalsize"		"500000000000"
        		"apps"
        		{
        			"570"		"25000000"
        			"730"		"30000000"
        		}
        	}
        }
        """;

    [Fact]
    public void ParseLibraryFolders_ParsesEveryLibraryAndUnescapesBackslashes()
    {
        var folders = SteamVdfParser.ParseLibraryFolders(LibraryFoldersVdf);

        Assert.Equal(2, folders.Count);
        Assert.Equal(@"C:\Program Files (x86)\Steam", folders[0].Path);
        Assert.Equal(@"D:\SteamLibrary", folders[1].Path);
    }

    [Fact]
    public void ParseLibraryFolders_ReturnsEmpty_WhenRootIsNotAnObject()
    {
        const string rootIsAPlainValue = """
            "libraryfolders"		"not an object"
            """;

        var folders = SteamVdfParser.ParseLibraryFolders(rootIsAPlainValue);

        Assert.Empty(folders);
    }

    private const string ValidAppManifestAcf = """
        "AppState"
        {
        	"appid"		"570"
        	"Universe"		"1"
        	"name"		"Dota 2"
        	"StateFlags"		"4"
        	"installdir"		"dota 2 beta"
        	"LastUpdated"		"1700000000"
        	"SizeOnDisk"		"36854775808"
        	"buildid"		"12345678"
        }
        """;

    [Fact]
    public void ParseAppManifest_ParsesAllExpectedFields()
    {
        var manifest = SteamVdfParser.ParseAppManifest(ValidAppManifestAcf, @"D:\SteamLibrary", @"D:\SteamLibrary\steamapps\appmanifest_570.acf");

        Assert.NotNull(manifest);
        Assert.Equal("570", manifest!.AppId);
        Assert.Equal("Dota 2", manifest.Name);
        Assert.Equal("dota 2 beta", manifest.InstallDir);
        Assert.Equal(36854775808, manifest.SizeOnDiskBytes);
        Assert.Equal("12345678", manifest.BuildId);
        Assert.Equal("4", manifest.StateFlags);
    }

    [Theory]
    [InlineData("""
        "AppState"
        {
        	"appid"		"570"
        	"installdir"		"dota 2 beta"
        }
        """)]
    [InlineData("""
        "AppState"
        {
        	"name"		"Dota 2"
        	"installdir"		"dota 2 beta"
        }
        """)]
    [InlineData("""
        "AppState"
        {
        	"appid"		"570"
        	"name"		"Dota 2"
        }
        """)]
    public void ParseAppManifest_ReturnsNull_WhenARequiredFieldIsMissing(string manifestWithMissingField)
    {
        var manifest = SteamVdfParser.ParseAppManifest(manifestWithMissingField, @"C:\Steam", @"C:\Steam\steamapps\appmanifest_570.acf");

        Assert.Null(manifest);
    }
}
