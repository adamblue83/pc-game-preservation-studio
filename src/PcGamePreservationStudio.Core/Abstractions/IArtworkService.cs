namespace PcGamePreservationStudio.Core.Abstractions;

/// <summary>Manages cover art and printable archive information sheets. Phase 9, not yet implemented.</summary>
public interface IArtworkService
{
    Task<string> SaveArtworkAsync(Guid archiveId, string artworkFilePath, CancellationToken cancellationToken = default);
}
