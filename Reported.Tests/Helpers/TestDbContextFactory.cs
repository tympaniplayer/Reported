using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Reported.Persistence;

namespace Reported.Tests.Helpers;

public sealed class TestDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;
    public ReportedDbContext Context { get; }

    private TestDbContextFactory(SqliteConnection connection, ReportedDbContext context)
    {
        _connection = connection;
        Context = context;
    }

    public static TestDbContextFactory Create()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ReportedDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new ReportedDbContext(options);
        context.Database.EnsureCreated();

        return new TestDbContextFactory(connection, context);
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
