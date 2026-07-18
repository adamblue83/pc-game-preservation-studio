using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Archiving.Tests;

/// <summary>Test double for tests that never select <see cref="MediaType.IsoOnly"/> — reports unavailable if ever called.</summary>
public sealed class NoOpIsoBuilder : IIsoBuilder
{
    public IsoBackendAvailability GetAvailability(string? oscdimgPathOverride = null) =>
        new() { IsAvailable = false, StatusMessage = "Not used in this test." };

    public Task<IsoBuildResult> BuildIsoAsync(string sourceFolder, string outputIsoPath, string? oscdimgPathOverride = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new IsoBuildResult { Success = false, ErrorMessage = "Not used in this test." });
}
