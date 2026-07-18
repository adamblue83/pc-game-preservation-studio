using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Burning;

/// <summary>
/// Builds a UDF ISO image by invoking a user-installed oscdimg.exe (Windows ADK Deployment Tools).
/// This project never bundles oscdimg.exe — see docs/BURNING_BACKENDS.md for the licensing reason.
/// All process invocation uses <see cref="ProcessStartInfo.ArgumentList"/>, never a shell string.
/// </summary>
public sealed class OscdimgIsoBuilder(ILogger<OscdimgIsoBuilder> logger) : IIsoBuilder
{
    private const string NotFoundMessage =
        "oscdimg.exe was not found. It ships with the Windows ADK's Deployment Tools component " +
        "(https://learn.microsoft.com/windows-hardware/get-started/adk-install) — install it, or set " +
        "its path in Settings, then try again. This app never bundles oscdimg.exe itself.";

    public IsoBackendAvailability GetAvailability(string? oscdimgPathOverride = null)
    {
        var path = OscdimgLocator.Locate(oscdimgPathOverride);
        return path is null
            ? new IsoBackendAvailability { IsAvailable = false, StatusMessage = NotFoundMessage }
            : new IsoBackendAvailability { IsAvailable = true, ExecutablePath = path, StatusMessage = $"oscdimg.exe found at {path}" };
    }

    public async Task<IsoBuildResult> BuildIsoAsync(string sourceFolder, string outputIsoPath, string? oscdimgPathOverride = null, CancellationToken cancellationToken = default)
    {
        var oscdimgPath = OscdimgLocator.Locate(oscdimgPathOverride);
        if (oscdimgPath is null)
        {
            return new IsoBuildResult { Success = false, ErrorMessage = NotFoundMessage };
        }

        try
        {
            if (File.Exists(outputIsoPath))
            {
                File.Delete(outputIsoPath);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = oscdimgPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            // -m: ignore the 4 GB single-file size limit (game files routinely exceed it).
            // -u2: joint UDF + ISO 9660 file system, UDF revision 1.02 — the broadest-compatibility
            // UDF revision oscdimg supports (see docs/BURNING_BACKENDS.md for the UDF-version tradeoffs).
            // -h: include hidden files/directories — without it oscdimg silently drops them, which
            // real GOG archives hit in practice (GOG Galaxy marks its own goggame-*.hashdb/info/script
            // bookkeeping files Hidden, so they were missing from the ISO despite the archive's own
            // checksums manifest listing them, until Verify Disc's read-back caught the mismatch).
            startInfo.ArgumentList.Add("-m");
            startInfo.ArgumentList.Add("-u2");
            startInfo.ArgumentList.Add("-h");
            startInfo.ArgumentList.Add(sourceFolder);
            startInfo.ArgumentList.Add(outputIsoPath);

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var stdErr = await stdErrTask;
            await stdOutTask;

            if (process.ExitCode != 0)
            {
                logger.LogWarning("oscdimg exited with code {ExitCode}: {StdErr}", process.ExitCode, stdErr);
                return new IsoBuildResult { Success = false, ErrorMessage = $"oscdimg exited with code {process.ExitCode}: {stdErr.Trim()}" };
            }

            if (!File.Exists(outputIsoPath))
            {
                return new IsoBuildResult { Success = false, ErrorMessage = "oscdimg reported success but no ISO file was produced." };
            }

            var isoSize = new FileInfo(outputIsoPath).Length;
            return new IsoBuildResult { Success = true, IsoPath = outputIsoPath, IsoSizeBytes = isoSize };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            logger.LogError(ex, "Failed to run oscdimg for {SourceFolder}", sourceFolder);
            return new IsoBuildResult { Success = false, ErrorMessage = $"Could not run oscdimg: {ex.Message}" };
        }
    }
}
