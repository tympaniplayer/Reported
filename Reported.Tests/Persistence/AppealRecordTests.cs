using Microsoft.EntityFrameworkCore;
using Reported.Persistence;
using Reported.Tests.Helpers;

namespace Reported.Tests.Persistence;

public sealed class AppealRecordTests
{
    [Fact]
    public async Task CreateAppealRecord_PersistsWithDefaultZeros()
    {
        using var factory = TestDbContextFactory.Create();
        var db = factory.Context;

        var record = new AppealRecord(100UL, "TestUser");
        db.Set<AppealRecord>().Add(record);
        await db.SaveChangesAsync();

        var saved = await db.Set<AppealRecord>().FirstAsync();
        Assert.Equal(100UL, saved.DiscordId);
        Assert.Equal("TestUser", saved.DiscordName);
        Assert.Equal(0, saved.AppealWins);
        Assert.Equal(0, saved.AppealAttempts);
    }

    [Fact]
    public async Task IncrementAppealAttempts_UpdatesCounter()
    {
        using var factory = TestDbContextFactory.Create();
        var db = factory.Context;

        var record = new AppealRecord(100UL, "TestUser");
        db.Set<AppealRecord>().Add(record);
        await db.SaveChangesAsync();

        record.AppealAttempts++;
        await db.SaveChangesAsync();

        var saved = await db.Set<AppealRecord>().FirstAsync();
        Assert.Equal(1, saved.AppealAttempts);
        Assert.Equal(0, saved.AppealWins);
    }

    [Fact]
    public async Task IncrementBothCounters_BothUpdate()
    {
        using var factory = TestDbContextFactory.Create();
        var db = factory.Context;

        var record = new AppealRecord(100UL, "TestUser");
        db.Set<AppealRecord>().Add(record);
        await db.SaveChangesAsync();

        record.AppealWins++;
        record.AppealAttempts++;
        await db.SaveChangesAsync();

        var saved = await db.Set<AppealRecord>().FirstAsync();
        Assert.Equal(1, saved.AppealWins);
        Assert.Equal(1, saved.AppealAttempts);
    }

    [Fact]
    public async Task IncrementOnlyAttempts_WinsUnchanged()
    {
        using var factory = TestDbContextFactory.Create();
        var db = factory.Context;

        var record = new AppealRecord(100UL, "TestUser");
        record.AppealWins = 3;
        record.AppealAttempts = 5;
        db.Set<AppealRecord>().Add(record);
        await db.SaveChangesAsync();

        record.AppealAttempts++;
        await db.SaveChangesAsync();

        var saved = await db.Set<AppealRecord>().FirstAsync();
        Assert.Equal(3, saved.AppealWins);
        Assert.Equal(6, saved.AppealAttempts);
    }

    [Fact]
    public async Task MultipleUsers_Coexist()
    {
        using var factory = TestDbContextFactory.Create();
        var db = factory.Context;

        db.Set<AppealRecord>().Add(new AppealRecord(100UL, "User1"));
        db.Set<AppealRecord>().Add(new AppealRecord(200UL, "User2"));
        await db.SaveChangesAsync();

        var count = await db.Set<AppealRecord>().CountAsync();
        Assert.Equal(2, count);

        var user1 = await db.Set<AppealRecord>().FirstAsync(a => a.DiscordId == 100UL);
        var user2 = await db.Set<AppealRecord>().FirstAsync(a => a.DiscordId == 200UL);
        Assert.Equal("User1", user1.DiscordName);
        Assert.Equal("User2", user2.DiscordName);
    }

    [Fact]
    public async Task DuplicateDiscordId_ThrowsUniqueConstraintViolation()
    {
        using var factory = TestDbContextFactory.Create();
        var db = factory.Context;

        db.Set<AppealRecord>().Add(new AppealRecord(100UL, "User1"));
        await db.SaveChangesAsync();

        db.Set<AppealRecord>().Add(new AppealRecord(100UL, "User1Again"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
