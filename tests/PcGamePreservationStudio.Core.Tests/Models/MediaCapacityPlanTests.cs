using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Core.Tests.Models;

public sealed class MediaCapacityPlanTests
{
    [Fact]
    public void FitsOnSingleMedium_IsTrue_WhenArchivePlusMarginFitsWithinUsableCapacity()
    {
        var plan = new MediaCapacityPlan
        {
            MediaType = MediaType.Dvd5,
            AdvertisedCapacityBytes = 4_700_000_000,
            UsableCapacityBytes = 4_482_469_888,
            SafetyMarginBytes = 100 * 1024 * 1024,
            ArchiveSizeBytes = 4_000_000_000,
        };

        Assert.True(plan.FitsOnSingleMedium);
        Assert.True(plan.RemainingBytes > 0);
    }

    [Fact]
    public void FitsOnSingleMedium_IsFalse_WhenArchiveExceedsUsableCapacityMinusMargin()
    {
        var plan = new MediaCapacityPlan
        {
            MediaType = MediaType.Dvd5,
            AdvertisedCapacityBytes = 4_700_000_000,
            UsableCapacityBytes = 4_482_469_888,
            SafetyMarginBytes = 100 * 1024 * 1024,
            ArchiveSizeBytes = 4_450_000_000,
        };

        Assert.False(plan.FitsOnSingleMedium);
        Assert.True(plan.RemainingBytes < 0);
    }
}
