namespace PcGamePreservationStudio.Archiving;

public static class ArchiveFolderNaming
{
    public static string ToArchiveFolderName(string gameTitle)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(gameTitle.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "GAME_ARCHIVE" : $"{sanitized}_ARCHIVE";
    }
}
