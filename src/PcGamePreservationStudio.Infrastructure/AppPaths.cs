namespace PcGamePreservationStudio.Infrastructure;

/// <summary>Well-known application data locations, created on first access.</summary>
public static class AppPaths
{
    public static string AppDataDirectory
    {
        get
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PcGamePreservationStudio");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string SettingsFilePath => Path.Combine(AppDataDirectory, "settings.json");

    public static string CatalogDatabasePath => Path.Combine(AppDataDirectory, "catalog.db");
}
