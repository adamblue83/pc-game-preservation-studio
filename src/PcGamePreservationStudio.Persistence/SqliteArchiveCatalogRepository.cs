using Microsoft.Data.Sqlite;
using PcGamePreservationStudio.Core.Abstractions;
using PcGamePreservationStudio.Core.Models;
using PcGamePreservationStudio.Infrastructure;

namespace PcGamePreservationStudio.Persistence;

public sealed class SqliteArchiveCatalogRepository : IArchiveCatalogRepository
{
    private readonly string _connectionString;

    public SqliteArchiveCatalogRepository(string? databasePathOverride = null)
    {
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePathOverride ?? AppPaths.CatalogDatabasePath }.ToString();
    }

    public async Task<IReadOnlyList<ArchiveCatalogEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, GameTitle, Platform, CreatedUtc, DestinationType, Status, ArchiveLocation, Notes
            FROM ArchiveCatalogEntries
            ORDER BY CreatedUtc DESC
            """;

        var results = new List<ArchiveCatalogEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ArchiveCatalogEntry
            {
                Id = Guid.Parse(reader.GetString(0)),
                GameTitle = reader.GetString(1),
                Platform = Enum.Parse<GamePlatform>(reader.GetString(2)),
                CreatedUtc = DateTimeOffset.Parse(reader.GetString(3)),
                DestinationType = Enum.Parse<ArchiveDestinationType>(reader.GetString(4)),
                Status = Enum.Parse<ArchiveStatus>(reader.GetString(5)),
                ArchiveLocation = reader.IsDBNull(6) ? null : reader.GetString(6),
                Notes = reader.IsDBNull(7) ? null : reader.GetString(7),
            });
        }

        return results;
    }

    public async Task AddAsync(ArchiveCatalogEntry entry, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ArchiveCatalogEntries (Id, GameTitle, Platform, CreatedUtc, DestinationType, Status, ArchiveLocation, Notes)
            VALUES ($id, $gameTitle, $platform, $createdUtc, $destinationType, $status, $archiveLocation, $notes)
            """;
        command.Parameters.AddWithValue("$id", entry.Id.ToString());
        command.Parameters.AddWithValue("$gameTitle", entry.GameTitle);
        command.Parameters.AddWithValue("$platform", entry.Platform.ToString());
        command.Parameters.AddWithValue("$createdUtc", entry.CreatedUtc.ToString("O"));
        command.Parameters.AddWithValue("$destinationType", entry.DestinationType.ToString());
        command.Parameters.AddWithValue("$status", entry.Status.ToString());
        command.Parameters.AddWithValue("$archiveLocation", (object?)entry.ArchiveLocation ?? DBNull.Value);
        command.Parameters.AddWithValue("$notes", (object?)entry.Notes ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var createTable = connection.CreateCommand();
        createTable.CommandText = """
            CREATE TABLE IF NOT EXISTS ArchiveCatalogEntries (
                Id TEXT PRIMARY KEY,
                GameTitle TEXT NOT NULL,
                Platform TEXT NOT NULL,
                CreatedUtc TEXT NOT NULL,
                DestinationType TEXT NOT NULL,
                Status TEXT NOT NULL,
                ArchiveLocation TEXT NULL,
                Notes TEXT NULL
            )
            """;
        await createTable.ExecuteNonQueryAsync(cancellationToken);

        return connection;
    }
}
