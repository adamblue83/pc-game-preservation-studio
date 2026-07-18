using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Steam.Tests;

internal sealed class StubSettingsService(AppSettings settings) : ISettingsService
{
    public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(settings);

    public Task SaveAsync(AppSettings newSettings, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
