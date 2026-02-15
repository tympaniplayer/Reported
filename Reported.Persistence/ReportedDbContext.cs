using Microsoft.EntityFrameworkCore;

namespace Reported.Persistence;

public sealed class ReportedDbContext : DbContext
{
    private readonly string _dbPath;
    public ReportedDbContext()
    {
        var envPath = Environment.GetEnvironmentVariable("DATABASE_PATH");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            _dbPath = envPath;
        }
        else
        {
            var folder = Environment.SpecialFolder.LocalApplicationData;
            var path = Environment.GetFolderPath(folder);
            _dbPath = Path.Join(path, "reported.db");
        }
    }
    
    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={_dbPath}");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReportedDbContext).Assembly);
<<<<<<< Updated upstream
=======

    /// <summary>
    /// Initializes the database using migrations. Handles the transition from
    /// legacy databases created with EnsureCreated (which lack migration history)
    /// by backfilling __EFMigrationsHistory for already-applied migrations.
    /// </summary>
    public static async Task InitializeDatabaseAsync()
    {
        await using var dbContext = new ReportedDbContext();

        // If UserReport exists, this is a legacy database (created with EnsureCreated
        // or partially migrated). Ensure the history table exists and pre-existing
        // migrations are recorded so MigrateAsync only applies new ones.
        // Uses IF NOT EXISTS / OR IGNORE for full idempotency.
        var conn = dbContext.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='UserReport'";
        var isLegacyDb = await cmd.ExecuteScalarAsync() != null;

        if (isLegacyDb)
        {
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                    "MigrationId" TEXT NOT NULL PRIMARY KEY,
                    "ProductVersion" TEXT NOT NULL
                );
                INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ('20250316151632_Initial', '9.0.3');
                INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ('20250317210437_UpdateTable', '9.0.3');
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        await conn.CloseAsync();
        await dbContext.Database.MigrateAsync();
    }
>>>>>>> Stashed changes
}