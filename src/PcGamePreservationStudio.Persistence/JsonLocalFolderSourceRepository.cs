using System.Text.Json;
using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;
using PcGamePreservationStudio.Infrastructure;

namespace PcGamePreservationStudio.Persistence;

public sealed class JsonLocalFolderSourceRepository(string? filePathOverride = null) : ILocalFolderSourceRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath = filePathOverride ?? Path.Combine(AppPaths.AppDataDirectory, "local-folder-sources.json");

    public async Task<IReadOnlyList<LocalFolderSource>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(_filePath);
        var sources = await JsonSerializer.DeserializeAsync<List<LocalFolderSource>>(stream, SerializerOptions, cancellationToken);
        return sources ?? [];
    }

    public async Task AddAsync(LocalFolderSource source, CancellationToken cancellationToken = default)
    {
        var existing = (await GetAllAsync(cancellationToken)).ToList();
        existing.Add(source);

        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, existing, SerializerOptions, cancellationToken);
    }
}
