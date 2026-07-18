using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;

namespace PcGamePreservationStudio.Burning;

/// <summary>
/// Enumerates optical drives and burns an already-built ISO to one, via IMAPI2
/// (MsftDiscMaster2 / MsftDiscRecorder2 / MsftDiscFormat2Data) through late-bound ("dynamic") COM
/// interop — the same approach long-standing PowerShell/VBScript IMAPI2 burning scripts use, chosen
/// over hand-authoring .NET 8 source-generated COM interfaces for these IDispatch-based Automation
/// interfaces. See docs/BURNING_BACKENDS.md for the full risk assessment: this code has not been
/// validated against every optical drive/media combination, only what was available for testing.
/// </summary>
public sealed class Imapi2DiscBurner(ILogger<Imapi2DiscBurner> logger) : IDiscBurner
{
    private const string DiscMasterProgId = "IMAPI2.MsftDiscMaster2";
    private const string DiscRecorderProgId = "IMAPI2.MsftDiscRecorder2";
    private const string DiscFormatDataProgId = "IMAPI2.MsftDiscFormat2Data";
    private const string ClientName = "PC Game Preservation Studio";

    public Task<IReadOnlyList<OpticalDriveInfo>> GetOpticalDrivesAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => GetOpticalDrives(), cancellationToken);

    private IReadOnlyList<OpticalDriveInfo> GetOpticalDrives()
    {
        var mediaLoadedByDrive = QueryMediaLoadedByDrive();

        object? discMaster = null;
        try
        {
            var discMasterType = Type.GetTypeFromProgID(DiscMasterProgId, throwOnError: false);
            if (discMasterType is null)
            {
                logger.LogInformation("IMAPI2 ({ProgId}) is not registered on this machine", DiscMasterProgId);
                return [];
            }

            discMaster = Activator.CreateInstance(discMasterType);
            dynamic master = discMaster!;
            int count = master.Count;

            var drives = new List<OpticalDriveInfo>();
            for (var i = 0; i < count; i++)
            {
                string uniqueId = master.Item(i);
                var drive = TryDescribeRecorder(uniqueId, mediaLoadedByDrive);
                if (drive is not null)
                {
                    drives.Add(drive);
                }
            }

            return drives;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or MissingMemberException)
        {
            logger.LogWarning(ex, "Failed to enumerate optical drives via IMAPI2");
            return [];
        }
        finally
        {
            ReleaseComObject(discMaster);
        }
    }

    private OpticalDriveInfo? TryDescribeRecorder(string uniqueId, IReadOnlyDictionary<string, bool> mediaLoadedByDrive)
    {
        object? recorder = null;
        try
        {
            var recorderType = Type.GetTypeFromProgID(DiscRecorderProgId, throwOnError: false);
            if (recorderType is null)
            {
                return null;
            }

            recorder = Activator.CreateInstance(recorderType);
            dynamic dynRecorder = recorder!;
            dynRecorder.InitializeDiscRecorder(uniqueId);

            object volumePathNames = dynRecorder.VolumePathNames;
            var driveLetter = ExtractDriveLetter(volumePathNames);
            var isMediaPresent = driveLetter is not null && mediaLoadedByDrive.TryGetValue(driveLetter, out var loaded) && loaded;

            return new OpticalDriveInfo
            {
                DriveId = uniqueId,
                DriveLetter = driveLetter ?? "?",
                IsMediaPresent = isMediaPresent,
                IsWritable = true,
            };
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or MissingMemberException)
        {
            logger.LogWarning(ex, "Failed to describe optical drive {UniqueId}", uniqueId);
            return null;
        }
        finally
        {
            ReleaseComObject(recorder);
        }
    }

    public Task<DiscBurnResult> BurnIsoAsync(string isoPath, string driveId, IProgress<double>? progress = null, CancellationToken cancellationToken = default) =>
        Task.Run(() => BurnIso(isoPath, driveId, progress, cancellationToken), cancellationToken);

    private async Task<DiscBurnResult> BurnIso(string isoPath, string driveId, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (!File.Exists(isoPath))
        {
            return new DiscBurnResult { Success = false, ErrorMessage = $"ISO file not found: {isoPath}" };
        }

        cancellationToken.ThrowIfCancellationRequested();

        object? recorder = null;
        object? discFormat = null;
        try
        {
            var recorderType = Type.GetTypeFromProgID(DiscRecorderProgId, throwOnError: false);
            var discFormatType = Type.GetTypeFromProgID(DiscFormatDataProgId, throwOnError: false);
            if (recorderType is null || discFormatType is null)
            {
                return new DiscBurnResult { Success = false, ErrorMessage = "IMAPI2 is not available on this machine." };
            }

            recorder = Activator.CreateInstance(recorderType);
            dynamic dynRecorder = recorder!;
            dynRecorder.InitializeDiscRecorder(driveId);

            discFormat = Activator.CreateInstance(discFormatType);
            dynamic dynDiscFormat = discFormat!;
            dynDiscFormat.ClientName = ClientName;
            dynDiscFormat.Recorder = dynRecorder;

            if (!(bool)dynDiscFormat.IsRecorderSupported(dynRecorder))
            {
                return new DiscBurnResult { Success = false, ErrorMessage = "This drive does not support burning data discs." };
            }

            if (!(bool)dynDiscFormat.IsCurrentMediaSupported(dynRecorder))
            {
                return new DiscBurnResult { Success = false, ErrorMessage = "No supported media was detected in this drive. Insert blank media and try again." };
            }

            // IsCurrentMediaSupported only checks media *type* compatibility, not whether it's
            // blank — writing onto already-recorded media without erasing it first was confirmed
            // (against real BD-R hardware) to silently corrupt the disc rather than fail cleanly.
            // Erasing rewritable media isn't implemented, so refuse non-blank media outright rather
            // than risk repeating that: better an honest refusal than a corrupted disc.
            if (!(bool)dynDiscFormat.MediaHeuristicallyBlank)
            {
                return new DiscBurnResult { Success = false, ErrorMessage = "This disc already has data on it. This app can only burn to blank media — erasing rewritable media isn't supported yet. Insert a blank disc and try again." };
            }

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(0.0);

            using (var fileStream = new FileStream(isoPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var comStream = new ComStreamWrapper(fileStream))
            {
                // IMAPI2's automation API exposes write progress only via a COM connection-point
                // event that late-bound "dynamic" interop cannot subscribe to, so this call blocks
                // until the burn finishes (or fails) with no incremental progress in between —
                // cancellation is only honored before it starts, not while it's running.
                dynDiscFormat.Write(comStream);
            }

            progress?.Report(1.0);

            // Confirmed against real hardware: Windows can take a few seconds to remount the
            // drive's new session after Write() returns, during which the volume looks empty or
            // is briefly unreadable — a caller that immediately re-reads the disc (e.g. to verify
            // it) can see a false failure even though the burn itself succeeded. Wait for the
            // volume to actually be listable before reporting success.
            object volumePathNames = dynRecorder.VolumePathNames;
            var driveLetter = ExtractDriveLetter(volumePathNames);
            if (driveLetter is not null)
            {
                await WaitForVolumeToBeReadableAsync(driveLetter, cancellationToken);
            }

            return new DiscBurnResult { Success = true };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or MissingMemberException or IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Failed to burn {IsoPath} to drive {DriveId}", isoPath, driveId);
            return new DiscBurnResult { Success = false, ErrorMessage = $"Burning failed: {ex.Message}" };
        }
        finally
        {
            ReleaseComObject(discFormat);
            ReleaseComObject(recorder);
        }
    }

    /// <summary>Best-effort wait for Windows to remount a just-burned disc's new session — the burn
    /// itself already succeeded by the time this is called, so a timeout here is logged and swallowed
    /// rather than turned into a failure.</summary>
    private async Task WaitForVolumeToBeReadableAsync(string driveLetter, CancellationToken cancellationToken)
    {
        var volumeRoot = driveLetter.EndsWith(':') ? driveLetter + "\\" : driveLetter;
        var deadline = DateTime.UtcNow.AddSeconds(20);

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Directory.GetFileSystemEntries(volumeRoot);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(1000, cancellationToken);
            }
        }

        logger.LogWarning("Disc at {DriveLetter} did not become readable within the expected time after burning", driveLetter);
    }

    private static string? ExtractDriveLetter(object volumePathNames)
    {
        if (volumePathNames is not object[] paths || paths.Length == 0)
        {
            return null;
        }

        var firstPath = paths[0]?.ToString();
        if (string.IsNullOrWhiteSpace(firstPath) || firstPath.Length < 2)
        {
            return null;
        }

        // "D:\" -> "D:"
        return firstPath[..2];
    }

    private static IReadOnlyDictionary<string, bool> QueryMediaLoadedByDrive()
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Drive, MediaLoaded FROM Win32_CDROMDrive");
            foreach (var item in searcher.Get())
            {
                using (item)
                {
                    var drive = item["Drive"] as string;
                    if (drive is not null)
                    {
                        result[drive] = item["MediaLoaded"] is true;
                    }
                }
            }
        }
        catch (ManagementException)
        {
            // WMI unavailable — media-loaded state simply can't be reported; drives still enumerate.
        }

        return result;
    }

    private static void ReleaseComObject(object? comObject)
    {
        if (comObject is not null && Marshal.IsComObject(comObject))
        {
            Marshal.ReleaseComObject(comObject);
        }
    }
}
