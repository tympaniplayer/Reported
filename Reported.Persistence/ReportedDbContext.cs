using Microsoft.EntityFrameworkCore;

namespace Reported.Persistence;

public sealed class ReportedDbContext : DbContext
{
    private readonly string _dbPath;
    public ReportedDbContext()
    {
        var folder = Environment.SpecialFolder.LocalApplicationData;
        var path = Environment.GetFolderPath(folder);
        _dbPath = Path.Join(path, "reported.db");
    }
    
    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={_dbPath}");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReportedDbContext).Assembly);
}