using System.Text.Json;
using Microsoft.Extensions.Logging;
using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;
using PcGamePreservationStudio.Infrastructure;

namespace PcGamePreservationStudio.Persistence;

public sealed class JsonSettingsService(ILogger<JsonSettingsService> logger, string? filePathOverride = null) : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath = filePathOverride ?? AppPaths.SettingsFilePath;

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return new AppSettings();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken);
            return settings ?? new AppSettings();
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Settings file at {Path} was unreadable; falling back to defaults", PathRedactor.Redact(_filePath));
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken);
    }
}
