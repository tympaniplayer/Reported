using Microsoft.EntityFrameworkCore;
using Reported.Models;
using Reported.Persistence;
using Reported.Services;
using Reported.Tests.Helpers;

namespace Reported.Tests.Services;

public sealed class AppealServiceTests
{
    [Fact]
    public async Task ProcessAppeal_Win_RemovesReportAndTracksStats()
    {
        using var factory = TestDbContextFactory.Create();
        var db = factory.Context;

        db.Set<UserReport>().Add(new UserReport(100UL, "User", 200UL, "Reporter", false, "NA"));
        await db.SaveChangesAsync();

        // [50] → win (50 > 49)
        var random = new FakeRandomProvider(50);
        var service = new AppealService(db, random);

        var result = await service.ProcessAppeal(100UL, "User");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Won);
        Assert.False(result.Value.HadNoReports);
        Assert.Equal(1, result.Value.AppealWins);
        Assert.Equal(1, result.Value.AppealAttempts);
        Assert.Equal(0, result.Value.PenaltyReportsAdded);

        var reportCount = await db.Set<UserReport>().CountAsync();
        Assert.Equal(0, reportCount);
    }

    [Fact]
    public async Task ProcessAppeal_Loss_AddsPenaltyReport()
    {
        using var factory = TestDbContextFactory.Create();
        var db = factory.Context;

        db.Set<UserReport>().Add(new UserReport(100UL, "User", 200UL, "Reporter", false, "NA"));
        await db.SaveChangesAsync();

        // [49] → loss (49 <= 49)
        var random = new FakeRandomProvider(49);
        var service = new AppealService(db, random);

        var result = await service.ProcessAppeal(100UL, "User");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Won);
        Assert.Equal(0, result.Value.AppealWins);
        Assert.Equal(1, result.Value.AppealAttempts);
        Assert.Equal(1, result.Value.PenaltyReportsAdded);

        var reportCount = await db.Set<UserReport>().CountAsync();
        Assert.Equal(2, reportCount); // original + 1 penalty
    }

    [Fact]
    public async Task ProcessAppeal_NoReports_AddsTenPenaltyReports()
    {
        using var factory = TestDbContextFactory.Create();
        var service = new AppealService(factory.Context, new FakeRandomProvider());

        var result = await service.ProcessAppeal(100UL, "User");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.HadNoReports);
        Assert.Equal(10, result.Value.PenaltyReportsAdded);
        Assert.Equal(0, result.Value.AppealWins);
        Assert.Equal(0, result.Value.AppealAttempts);

        var reports = await factory.Context.Set<UserReport>().ToListAsync();
        Assert.Equal(10, reports.Count);
        Assert.All(reports, r =>
        {
            Assert.Equal("DU", r.Description);
            Assert.Equal(100UL, r.DiscordId);
        });

        // No AppealRecord should be created
        var appealCount = await factory.Context.Set<AppealRecord>().CountAsync();
        Assert.Equal(0, appealCount);
    }

    [Fact]
    public async Task ProcessAppeal_MultipleAppeals_TracksCumulativeStats()
    {
        using var factory = TestDbContextFactory.Create();
        var db = factory.Context;

        // Seed 3 reports
        db.Set<UserReport>().Add(new UserReport(100UL, "User", 200UL, "Reporter", false, "NA"));
        db.Set<UserReport>().Add(new UserReport(100UL, "User", 200UL, "Reporter", false, "VA"));
        db.Set<UserReport>().Add(new UserReport(100UL, "User", 200UL, "Reporter", false, "CH"));
        await db.SaveChangesAsync();

        // First appeal: win
        var service1 = new AppealService(db, new FakeRandomProvider(50));
        var result1 = await service1.ProcessAppeal(100UL, "User");
        Assert.True(result1.Value.Won);
        Assert.Equal(1, result1.Value.AppealWins);
        Assert.Equal(1, result1.Value.AppealAttempts);

        // Second appeal: loss (adds 1 penalty report)
        var service2 = new AppealService(db, new FakeRandomProvider(49));
        var result2 = await service2.ProcessAppeal(100UL, "User");
        Assert.False(result2.Value.Won);
        Assert.Equal(1, result2.Value.AppealWins);
        Assert.Equal(2, result2.Value.AppealAttempts);
    }

    [Fact]
    public async Task GetAppealStats_NoRecord_ReturnsZeros()
    {
        using var factory = TestDbContextFactory.Create();
        var service = new AppealService(factory.Context, new FakeRandomProvider());

        var result = await service.GetAppealStats(100UL);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.Wins);
        Assert.Equal(0, result.Value.Attempts);
        Assert.Equal(0, result.Value.WinRate);
    }

    [Fact]
    public async Task GetAppealStats_WithRecord_CalculatesWinRate()
    {
        using var factory = TestDbContextFactory.Create();
        var db = factory.Context;

        var record = new AppealRecord(100UL, "User")
        {
            AppealWins = 3,
            AppealAttempts = 5
        };
        db.Set<AppealRecord>().Add(record);
        await db.SaveChangesAsync();

        var service = new AppealService(db, new FakeRandomProvider());

        var result = await service.GetAppealStats(100UL);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Wins);
        Assert.Equal(5, result.Value.Attempts);
        Assert.Equal(60, result.Value.WinRate);
    }
}
