using PcGamePreservationStudio.Core.Models;
using PcGamePreservationStudio.Media;

namespace PcGamePreservationStudio.Media.Tests;

public sealed class DiscCapacityServiceTests
{
    private readonly DiscCapacityService _service = new();

    [Theory]
    [InlineData(MediaType.Cd700, 700_000_000)]
    [InlineData(MediaType.Dvd5, 4_700_000_000)]
    [InlineData(MediaType.Dvd9, 8_500_000_000)]
    [InlineData(MediaType.Bd25, 25_000_000_000)]
    [InlineData(MediaType.Bd50, 50_000_000_000)]
    [InlineData(MediaType.Bd100, 100_000_000_000)]
    [InlineData(MediaType.Bd128, 128_000_000_000)]
    public void Plan_UsesTheDecimalAdvertisedCapacityForEachFixedMedium(MediaType mediaType, long expectedAdvertisedBytes)
    {
        var plan = _service.Plan(archiveSizeBytes: 0, mediaType);

        Assert.Equal(expectedAdvertisedBytes, plan.AdvertisedCapacityBytes);
        Assert.True(plan.UsableCapacityBytes < plan.AdvertisedCapacityBytes, "Usable capacity should be less than advertised due to formatting overhead.");
    }

    [Theory]
    [InlineData(MediaType.Cd700, 100)]
    [InlineData(MediaType.Dvd5, 100)]
    [InlineData(MediaType.Dvd9, 100)]
    [InlineData(MediaType.Bd25, 500)]
    [InlineData(MediaType.Bd50, 500)]
    [InlineData(MediaType.Bd100, 1024)]
    [InlineData(MediaType.Bd128, 1024)]
    public void Plan_UsesTheDocumentedDefaultSafetyMargin(MediaType mediaType, long expectedMarginMb)
    {
        var plan = _service.Plan(archiveSizeBytes: 0, mediaType);

        Assert.Equal(expectedMarginMb * 1024 * 1024, plan.SafetyMarginBytes);
    }

    [Fact]
    public void Plan_HonorsASafetyMarginOverride()
    {
        var plan = _service.Plan(archiveSizeBytes: 0, MediaType.Dvd5, safetyMarginOverrideBytes: 250L * 1024 * 1024);

        Assert.Equal(250L * 1024 * 1024, plan.SafetyMarginBytes);
    }

    [Fact]
    public void Plan_ThrowsWhenCustomMediaHasNoCapacitySpecified()
    {
        Assert.Throws<ArgumentException>(() => _service.Plan(archiveSizeBytes: 0, MediaType.Custom));
    }

    [Fact]
    public void Plan_UsesTheProvidedCustomCapacity()
    {
        var plan = _service.Plan(archiveSizeBytes: 0, MediaType.Usb, customCapacityBytes: 16_000_000_000);

        Assert.Equal(16_000_000_000, plan.AdvertisedCapacityBytes);
    }

    [Fact]
    public void Plan_TreatsFolderOnlyAsAlwaysFitting()
    {
        var plan = _service.Plan(archiveSizeBytes: 500_000_000_000, MediaType.FolderOnly);

        Assert.True(plan.FitsOnSingleMedium);
    }

    [Fact]
    public void Plan_ReportsDoesNotFit_WhenArchiveExceedsUsableCapacityMinusMargin()
    {
        var plan = _service.Plan(archiveSizeBytes: 4_600_000_000, MediaType.Dvd5);

        Assert.False(plan.FitsOnSingleMedium);
    }

    [Fact]
    public void PlanMultiDisc_PlacesAllFilesOnOneDisc_WhenTheyFitTogether()
    {
        IReadOnlyList<PlannedFile> files =
        [
            new("Game/a.bin", 100_000_000),
            new("Game/b.bin", 200_000_000),
        ];

        var plan = _service.PlanMultiDisc(files, MediaType.Dvd5);

        Assert.Equal(1, plan.DiscCount);
        Assert.False(plan.RequiresMultipleDiscs);
        Assert.Empty(plan.FilesExceedingSingleDiscCapacity);
    }

    [Fact]
    public void PlanMultiDisc_SplitsAcrossDiscs_WhenTotalExceedsOneDiscsCapacity()
    {
        // Custom 1,000,000-byte "disc" with a default-margin override of 0 for a clean, exact test.
        IReadOnlyList<PlannedFile> files =
        [
            new("Game/a.bin", 600_000),
            new("Game/b.bin", 600_000),
            new("Game/c.bin", 600_000),
        ];

        var plan = _service.PlanMultiDisc(files, MediaType.Custom, customCapacityBytes: 1_000_000, safetyMarginOverrideBytes: 0);

        Assert.True(plan.RequiresMultipleDiscs);
        Assert.Empty(plan.FilesExceedingSingleDiscCapacity);
        // Every file must be assigned to exactly one disc — never split.
        var allAssignedFiles = plan.Discs.SelectMany(d => d.Files).ToList();
        Assert.Equal(files.Count, allAssignedFiles.Count);
        Assert.All(plan.Discs, disc => Assert.True(disc.TotalBytes <= 1_000_000));
    }

    [Fact]
    public void PlanMultiDisc_FlagsAFileTooLargeForAnySingleDisc()
    {
        IReadOnlyList<PlannedFile> files =
        [
            new("Game/huge.bin", 2_000_000),
            new("Game/small.bin", 100_000),
        ];

        var plan = _service.PlanMultiDisc(files, MediaType.Custom, customCapacityBytes: 1_000_000, safetyMarginOverrideBytes: 0);

        Assert.True(plan.HasBlockingOversizedFiles);
        Assert.Contains(plan.FilesExceedingSingleDiscCapacity, f => f.RelativePath == "Game/huge.bin");
        // The file that does fit should still be placed on a disc.
        Assert.Contains(plan.Discs.SelectMany(d => d.Files), f => f.RelativePath == "Game/small.bin");
    }
}
