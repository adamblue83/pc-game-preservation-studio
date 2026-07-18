using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Archiving.Tests;

/// <summary>Test double standing in for a real oscdimg-backed <see cref="IIsoBuilder"/>.</summary>
public sealed class FakeIsoBuilder(bool succeeds, string? errorMessage = null) : IIsoBuilder
{
    public IsoBackendAvailability GetAvailability(string? oscdimgPathOverride = null) =>
        new() { IsAvailable = succeeds, ExecutablePath = succeeds ? "fake-oscdimg.exe" : null, StatusMessage = succeeds ? "Available" : "Not available" };

    public async Task<IsoBuildResult> BuildIsoAsync(string sourceFolder, string outputIsoPath, string? oscdimgPathOverride = null, CancellationToken cancellationToken = default)
    {
        if (!succeeds)
        {
            return new IsoBuildResult { Success = false, ErrorMessage = errorMessage ?? "Fake ISO build failure" };
        }

        await File.WriteAllTextAsync(outputIsoPath, "fake iso contents", cancellationToken);
        return new IsoBuildResult { Success = true, IsoPath = outputIsoPath, IsoSizeBytes = new FileInfo(outputIsoPath).Length };
    }
}
