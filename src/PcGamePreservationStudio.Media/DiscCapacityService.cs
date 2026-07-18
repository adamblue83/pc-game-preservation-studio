using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Media;

/// <summary>
/// Computes disc capacity fit and multi-disc file placement. "Advertised" capacities are the
/// decimal marketing figures printed on the box; "usable" capacities deduct an approximate
/// filesystem/session overhead — real formatted capacity varies by burner/filesystem, so this is
/// an estimate for planning purposes, not a mastering-precision guarantee.
/// </summary>
public sealed class DiscCapacityService : IDiscCapacityService
{
    private static readonly IReadOnlyDictionary<MediaType, long> AdvertisedCapacities = new Dictionary<MediaType, long>
    {
        [MediaType.Cd700] = 700_000_000,
        [MediaType.Dvd5] = 4_700_000_000,
        [MediaType.Dvd9] = 8_500_000_000,
        [MediaType.Bd25] = 25_000_000_000,
        [MediaType.Bd50] = 50_000_000_000,
        [MediaType.Bd100] = 100_000_000_000,
        [MediaType.Bd128] = 128_000_000_000,
    };

    private static readonly IReadOnlyDictionary<MediaType, long> DefaultSafetyMargins = new Dictionary<MediaType, long>
    {
        [MediaType.Cd700] = 100L * 1024 * 1024,
        [MediaType.Dvd5] = 100L * 1024 * 1024,
        [MediaType.Dvd9] = 100L * 1024 * 1024,
        [MediaType.Bd25] = 500L * 1024 * 1024,
        [MediaType.Bd50] = 500L * 1024 * 1024,
        [MediaType.Bd100] = 1024L * 1024 * 1024,
        [MediaType.Bd128] = 1024L * 1024 * 1024,
    };

    /// <summary>Approximate fraction of advertised capacity actually usable once formatted (filesystem/session overhead).</summary>
    private const double UsableCapacityFraction = 0.98;

    /// <summary>Effectively unlimited, used for media types with no fixed capacity ceiling (folder/ISO output).</summary>
    private const long UnlimitedCapacity = long.MaxValue / 2;

    public MediaCapacityPlan Plan(long archiveSizeBytes, MediaType mediaType, long? customCapacityBytes = null, long? safetyMarginOverrideBytes = null)
    {
        var advertised = ResolveAdvertisedCapacity(mediaType, customCapacityBytes);
        var usable = IsUnlimited(mediaType) ? advertised : (long)(advertised * UsableCapacityFraction);
        var safetyMargin = safetyMarginOverrideBytes ?? GetDefaultSafetyMargin(mediaType);

        return new MediaCapacityPlan
        {
            MediaType = mediaType,
            AdvertisedCapacityBytes = advertised,
            UsableCapacityBytes = usable,
            SafetyMarginBytes = safetyMargin,
            ArchiveSizeBytes = archiveSizeBytes,
        };
    }

    public MultiDiscPlan PlanMultiDisc(IReadOnlyList<PlannedFile> files, MediaType mediaType, long? customCapacityBytes = null, long? safetyMarginOverrideBytes = null)
    {
        var advertised = ResolveAdvertisedCapacity(mediaType, customCapacityBytes);
        var usable = IsUnlimited(mediaType) ? advertised : (long)(advertised * UsableCapacityFraction);
        var safetyMargin = safetyMarginOverrideBytes ?? GetDefaultSafetyMargin(mediaType);
        var perDiscCapacity = usable - safetyMargin;

        var fittable = new List<PlannedFile>();
        var oversized = new List<PlannedFile>();
        foreach (var file in files)
        {
            (file.SizeBytes > perDiscCapacity ? oversized : fittable).Add(file);
        }

        // First-fit decreasing bin packing: largest files placed first, onto the first disc with room.
        // This never splits a single file across discs.
        var discBuilders = new List<DiscBuilder>();
        foreach (var file in fittable.OrderByDescending(f => f.SizeBytes))
        {
            var disc = discBuilders.FirstOrDefault(d => d.Used + file.SizeBytes <= perDiscCapacity);
            if (disc is null)
            {
                disc = new DiscBuilder();
                discBuilders.Add(disc);
            }

            disc.Files.Add(file);
            disc.Used += file.SizeBytes;
        }

        var discs = discBuilders
            .Select((builder, index) => new DiscAssignment { DiscNumber = index + 1, Files = builder.Files, TotalBytes = builder.Used })
            .ToList();

        return new MultiDiscPlan
        {
            MediaType = mediaType,
            SafetyMarginBytes = safetyMargin,
            Discs = discs,
            FilesExceedingSingleDiscCapacity = oversized,
        };
    }

    private static bool IsUnlimited(MediaType mediaType) => mediaType is MediaType.FolderOnly or MediaType.IsoOnly;

    private static long ResolveAdvertisedCapacity(MediaType mediaType, long? customCapacityBytes)
    {
        if (AdvertisedCapacities.TryGetValue(mediaType, out var fixedCapacity))
        {
            return fixedCapacity;
        }

        if (IsUnlimited(mediaType))
        {
            return UnlimitedCapacity;
        }

        if (customCapacityBytes is { } custom && custom > 0)
        {
            return custom;
        }

        throw new ArgumentException($"A custom capacity is required for media type '{mediaType}'.", nameof(customCapacityBytes));
    }

    private static long GetDefaultSafetyMargin(MediaType mediaType) =>
        DefaultSafetyMargins.TryGetValue(mediaType, out var margin) ? margin : 0;

    private sealed class DiscBuilder
    {
        public List<PlannedFile> Files { get; } = [];
        public long Used { get; set; }
    }
}
