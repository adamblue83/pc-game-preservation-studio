using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Core.Abstractions;

/// <summary>Persists user-added local-folder game sources (the manual alternative to Steam/GOG detection).</summary>
public interface ILocalFolderSourceRepository
{
    Task<IReadOnlyList<LocalFolderSource>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(LocalFolderSource source, CancellationToken cancellationToken = default);
}
