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

    /// <summary>
    /// Initializes the database using migrations. Handles the transition from
    /// legacy databases created with EnsureCreated (which lack migration history)
    /// by backfilling __EFMigrationsHistory for already-applied migrations.
    /// </summary>
    public static async Task InitializeDatabaseAsync()
    {
        await using var dbContext = new ReportedDbContext();

        var conn = dbContext.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory'";
        var historyExists = await cmd.ExecuteScalarAsync() != null;

        if (!historyExists)
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='UserReport'";
            var tablesExist = await cmd.ExecuteScalarAsync() != null;

            if (tablesExist)
            {
                // Database was created with EnsureCreated — backfill history
                // so MigrateAsync only applies new migrations.
                cmd.CommandText = """
                    CREATE TABLE "__EFMigrationsHistory" (
                        "MigrationId" TEXT NOT NULL PRIMARY KEY,
                        "ProductVersion" TEXT NOT NULL
                    );
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                    VALUES ('20250316151632_Initial', '9.0.3');
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                    VALUES ('20250317210437_UpdateTable', '9.0.3');
                    """;
                await cmd.ExecuteNonQueryAsync();
            }
        }

        await conn.CloseAsync();
        await dbContext.Database.MigrateAsync();
    }
}