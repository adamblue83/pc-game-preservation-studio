using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Core.Abstractions;

/// <summary>
/// Verifies an archive folder against its recorded checksums — a burned disc's mounted drive
/// letter works here too, since it's just another folder path (see the "Verify Disc" flow). Never
/// reports <see cref="VerificationOutcome.Verified"/> on build/burn success alone — always re-reads
/// and re-hashes. Reading checksums out of a .iso file directly (without burning/mounting it first)
/// is not implemented.
/// </summary>
public interface IArchiveVerificationService
{
    Task<VerificationResult> VerifyAsync(string archiveFolderPath, CancellationToken cancellationToken = default);
}
