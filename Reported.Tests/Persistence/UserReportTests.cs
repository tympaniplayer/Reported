using Microsoft.EntityFrameworkCore;
using Reported.Persistence;
using Reported.Tests.Helpers;

namespace Reported.Tests.Persistence;

public sealed class UserReportTests
{
    [Fact]
    public async Task CreateUserReport_PersistsAllFields()
    {
        using var factory = TestDbContextFactory.Create();
        var db = factory.Context;

        var report = new UserReport(123456UL, "TestUser", 789012UL, "Reporter", false, "NA");
        db.Set<UserReport>().Add(report);
        await db.SaveChangesAsync();

        var saved = await db.Set<UserReport>().FirstAsync();
        Assert.Equal(123456UL, saved.DiscordId);
        Assert.Equal("TestUser", saved.DiscordName);
        Assert.Equal(789012UL, saved.InitiatedUserDiscordId);
        Assert.Equal("Reporter", saved.InitiatedDiscordName);
        Assert.False(saved.Confused);
        Assert.Equal("NA", saved.Description);
    }

    [Fact]
    public async Task MultipleReports_SameUser_ReturnsCorrectCount()
    {
        using var factory = TestDbContextFactory.Create();
        var db = factory.Context;

        db.Set<UserReport>().Add(new UserReport(100UL, "User", 200UL, "R1", false, "NA"));
        db.Set<UserReport>().Add(new UserReport(100UL, "User", 300UL, "R2", false, "VA"));
        db.Set<UserReport>().Add(new UserReport(100UL, "User", 200UL, "R1", false, "NA"));
        await db.SaveChangesAsync();

        var count = await db.Set<UserReport>().CountAsync(r => r.DiscordId == 100UL);
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task QueryByDiscordId_ReturnsOnlyTargetUserReports()
    {
        using var factory = TestDbContextFactory.Create();
        var db = factory.Context;

        db.Set<UserReport>().Add(new UserReport(100UL, "User1", 300UL, "Reporter", false, "NA"));
        db.Set<UserReport>().Add(new UserReport(200UL, "User2", 300UL, "Reporter", false, "VA"));
        db.Set<UserReport>().Add(new UserReport(100UL, "User1", 300UL, "Reporter", false, "CH"));
        await db.SaveChangesAsync();

        var user1Reports = await db.Set<UserReport>().Where(r => r.DiscordId == 100UL).ToListAsync();
        Assert.Equal(2, user1Reports.Count);
        Assert.All(user1Reports, r => Assert.Equal(100UL, r.DiscordId));
    }

    [Fact]
    public async Task DeleteReport_RemovesFromDatabase()
    {
        using var factory = TestDbContextFactory.Create();
        var db = factory.Context;

        var report = new UserReport(100UL, "User", 200UL, "Reporter", false, "NA");
        db.Set<UserReport>().Add(report);
        await db.SaveChangesAsync();

        db.Set<UserReport>().Remove(report);
        await db.SaveChangesAsync();

        var count = await db.Set<UserReport>().CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task DiscordIdIndex_AllowsEfficientQueryByDiscordId()
    {
        using var factory = TestDbContextFactory.Create();
        var db = factory.Context;

        for (var i = 0; i < 50; i++)
        {
            db.Set<UserReport>().Add(new UserReport((ulong)(i % 5), $"User{i % 5}", 999UL, "Reporter", false, "NA"));
        }
        await db.SaveChangesAsync();

        var results = await db.Set<UserReport>().Where(r => r.DiscordId == 2UL).ToListAsync();
        Assert.Equal(10, results.Count);
    }
}
