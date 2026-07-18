namespace PcGamePreservationStudio.Burning;

/// <summary>
/// Locates a user-installed copy of oscdimg.exe (Windows ADK Deployment Tools). This project never
/// bundles or redistributes oscdimg.exe — its license permits producing only Microsoft-authorized
/// content, so this only detects a copy the user already installed themselves.
/// </summary>
public static class OscdimgLocator
{
    private static readonly string[] AdkArchitectures = ["amd64", "x86", "arm64"];

    public static string? Locate(string? userOverride = null)
    {
        if (!string.IsNullOrWhiteSpace(userOverride) && File.Exists(userOverride))
        {
            return userOverride;
        }

        foreach (var programFilesRoot in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                 })
        {
            if (string.IsNullOrWhiteSpace(programFilesRoot))
            {
                continue;
            }

            foreach (var arch in AdkArchitectures)
            {
                var candidate = Path.Combine(programFilesRoot, "Windows Kits", "10",
                    "Assessment and Deployment Kit", "Deployment Tools", arch, "Oscdimg", "oscdimg.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return LocateOnPath();
    }

    private static string? LocateOnPath()
    {
        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathVariable))
        {
            return null;
        }

        foreach (var directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory, "oscdimg.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (ArgumentException)
            {
                // Malformed PATH entry — skip it.
            }
        }

        return null;
    }
}
