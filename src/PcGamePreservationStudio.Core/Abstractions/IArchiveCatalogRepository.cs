using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Core.Abstractions;

public interface IArchiveCatalogRepository
{
    Task<IReadOnlyList<ArchiveCatalogEntry>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(ArchiveCatalogEntry entry, CancellationToken cancellationToken = default);
}
