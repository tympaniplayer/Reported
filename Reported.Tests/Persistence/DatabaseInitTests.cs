using Microsoft.Data.Sqlite;
using Reported.Persistence;

namespace Reported.Tests.Persistence;

public sealed class DatabaseInitTests : IDisposable
{
    private readonly string _originalDbPath;
    private readonly List<string> _tempFiles = [];

    public DatabaseInitTests()
    {
        _originalDbPath = Environment.GetEnvironmentVariable("DATABASE_PATH") ?? "";
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("DATABASE_PATH", _originalDbPath == "" ? null : _originalDbPath);
        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); } catch { /* cleanup best-effort */ }
            try { File.Delete(file + "-journal"); } catch { }
            try { File.Delete(file + "-shm"); } catch { }
            try { File.Delete(file + "-wal"); } catch { }
        }
    }

    private string CreateTempDbPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"reported_test_{Guid.NewGuid():N}.db");
        _tempFiles.Add(path);
        Environment.SetEnvironmentVariable("DATABASE_PATH", path);
        return path;
    }

    [Fact]
    public async Task FreshDatabase_CreatesAllTables()
    {
        var dbPath = CreateTempDbPath();

        await ReportedDbContext.InitializeDatabaseAsync();

        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        var tables = new List<string>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        Assert.Contains("UserReport", tables);
        Assert.Contains("AppealRecord", tables);
        Assert.Contains("__EFMigrationsHistory", tables);
    }

    [Fact]
    public async Task IdempotentRerun_NoErrors()
    {
        CreateTempDbPath();

        await ReportedDbContext.InitializeDatabaseAsync();
        await ReportedDbContext.InitializeDatabaseAsync(); // second run should not throw
    }

    [Fact]
    public async Task LegacyDatabase_BackfillsMigrationHistory()
    {
        var dbPath = CreateTempDbPath();

        // Manually create a legacy database (pre-migration state with UserReport but no __EFMigrationsHistory)
        await using (var connection = new SqliteConnection($"Data Source={dbPath}"))
        {
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE "UserReport" (
                    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    "DiscordId" INTEGER NOT NULL,
                    "DiscordName" TEXT NOT NULL,
                    "InitiatedUserDiscordId" INTEGER NOT NULL,
                    "InitiatedDiscordName" TEXT NOT NULL,
                    "Confused" INTEGER NOT NULL DEFAULT 0,
                    "Description" TEXT
                );
                CREATE INDEX "IX_UserReport_DiscordId" ON "UserReport" ("DiscordId");
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        // InitializeDatabaseAsync should detect legacy DB and backfill migration history
        await ReportedDbContext.InitializeDatabaseAsync();

        // Verify migration history was backfilled
        await using (var connection = new SqliteConnection($"Data Source={dbPath}"))
        {
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();

            cmd.CommandText = "SELECT COUNT(*) FROM __EFMigrationsHistory";
            var migrationCount = (long)(await cmd.ExecuteScalarAsync())!;
            Assert.True(migrationCount >= 2, $"Expected at least 2 migration records, got {migrationCount}");

            // Verify AppealRecord table was created by migrations
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='AppealRecord'";
            var appealTableExists = await cmd.ExecuteScalarAsync();
            Assert.NotNull(appealTableExists);
        }
    }
}
