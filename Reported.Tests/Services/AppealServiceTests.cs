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
    public async Task ProcessAppeal_Win_MarksReportAsAppealed()
    {
        using var factory = TestDbContextFactory.Create();
        var db = factory.Context;

        db.Set<UserReport>().Add(new UserReport(100UL, "User", 200UL, "Reporter", false, "NA"));
        await db.SaveChangesAsync();

        var service = new AppealService(db, new FakeRandomProvider(50));
        var result = await service.ProcessAppeal(100UL, "User");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Won);
        // Report is deleted on win, so no report to check HasBeenAppealed on
        Assert.Equal(AppealRejectionReason.None, result.Value.RejectionReason);
    }

    [Fact]
    public async Task ProcessAppeal_Loss_MarksReportAsAppealed()
    {
        using var factory = TestDbContextFactory.Create();
        var db = factory.Context;

        db.Set<UserReport>().Add(new UserReport(100UL, "User", 200UL, "Reporter", false, "NA"));
        await db.SaveChangesAsync();

        var service = new AppealService(db, new FakeRandomProvider(49));
        var result = await service.ProcessAppeal(100UL, "User");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Won);

        var reports = await db.Set<UserReport>().ToListAsync();
        // Original report should be marked as appealed
        var original = reports.First(r => r.InitiatedUserDiscordId == 200UL);
        Assert.True(original.HasBeenAppealed);
    }

    [Fact]
    public async Task ProcessAppeal_AllReportsAppealed_ReturnsAllAppealedRejection()
    {
        using var factory = TestDbContextFactory.Create();
        var db = factory.Context;

        db.Set<UserReport>().Add(new UserReport(100UL, "User", 200UL, "Reporter", false, "NA", hasBeenAppealed: true));
        await db.SaveChangesAsync();

        var service = new AppealService(db, new FakeRandomProvider());
        var result = await service.ProcessAppeal(100UL, "User");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Won);
        Assert.Equal(AppealRejectionReason.AllAppealed, result.Value.RejectionReason);
    }

    [Fact]
    public async Task ProcessAppeal_MixedAppealedAndUnappealed_SelectsOnlyUnappealed()
    {
        using var factory = TestDbContextFactory.Create();
        var db = factory.Context;

        // Two appealed, one unappealed
        db.Set<UserReport>().Add(new UserReport(100UL, "User", 200UL, "Reporter", false, "NA", hasBeenAppealed: true));
        db.Set<UserReport>().Add(new UserReport(100UL, "User", 200UL, "Reporter", false, "VA", hasBeenAppealed: true));
        db.Set<UserReport>().Add(new UserReport(100UL, "User", 200UL, "Reporter", false, "CH"));
        await db.SaveChangesAsync();

        // Win — removes the unappealed report
        var service = new AppealService(db, new FakeRandomProvider(50));
        var result = await service.ProcessAppeal(100UL, "User");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Won);
        Assert.Equal(AppealRejectionReason.None, result.Value.RejectionReason);

        // Only the two appealed reports should remain
        var remaining = await db.Set<UserReport>().ToListAsync();
        Assert.Equal(2, remaining.Count);
        Assert.All(remaining, r => Assert.True(r.HasBeenAppealed));
    }

    [Fact]
    public async Task ProcessAppeal_Loss_PenaltyReportIsPreMarkedAsAppealed()
    {
        using var factory = TestDbContextFactory.Create();
        var db = factory.Context;

        db.Set<UserReport>().Add(new UserReport(100UL, "User", 200UL, "Reporter", false, "NA"));
        await db.SaveChangesAsync();

        var service = new AppealService(db, new FakeRandomProvider(49));
        var result = await service.ProcessAppeal(100UL, "User");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Won);

        // The penalty report (self-initiated) should be marked as appealed
        var reports = await db.Set<UserReport>().ToListAsync();
        var penalty = reports.First(r => r.InitiatedUserDiscordId == 100UL);
        Assert.True(penalty.HasBeenAppealed);
    }

    [Fact]
    public async Task ProcessAppeal_OnlySelfReports_ReturnsOnlySelfReportsRejection()
    {
        using var factory = TestDbContextFactory.Create();
        var db = factory.Context;

        // Self-report: reporter and reported are the same user
        db.Set<UserReport>().Add(new UserReport(100UL, "User", 100UL, "User", false, "NA"));
        await db.SaveChangesAsync();

        var service = new AppealService(db, new FakeRandomProvider());
        var result = await service.ProcessAppeal(100UL, "User");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Won);
        Assert.Equal(AppealRejectionReason.OnlySelfReports, result.Value.RejectionReason);
    }

    [Fact]
    public async Task ProcessAppeal_MixedSelfAndOtherReports_SelectsOnlyOtherInitiated()
    {
        using var factory = TestDbContextFactory.Create();
        var db = factory.Context;

        // Self-report (not eligible)
        db.Set<UserReport>().Add(new UserReport(100UL, "User", 100UL, "User", false, "NA"));
        // Other-initiated report (eligible)
        db.Set<UserReport>().Add(new UserReport(100UL, "User", 200UL, "Reporter", false, "VA"));
        await db.SaveChangesAsync();

        var service = new AppealService(db, new FakeRandomProvider(50));
        var result = await service.ProcessAppeal(100UL, "User");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Won);
        Assert.Equal(AppealRejectionReason.None, result.Value.RejectionReason);

        // Only the self-report should remain
        var remaining = await db.Set<UserReport>().ToListAsync();
        Assert.Single(remaining);
        Assert.Equal(100UL, remaining[0].InitiatedUserDiscordId);
    }

    [Fact]
    public async Task ProcessAppeal_BackfireReport_IsNotEligibleForAppeal()
    {
        using var factory = TestDbContextFactory.Create();
        var db = factory.Context;

        // Backfire report: self-report with Confused = true
        db.Set<UserReport>().Add(new UserReport(100UL, "User", 100UL, "User", true, "NA"));
        await db.SaveChangesAsync();

        var service = new AppealService(db, new FakeRandomProvider());
        var result = await service.ProcessAppeal(100UL, "User");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Won);
        Assert.Equal(AppealRejectionReason.OnlySelfReports, result.Value.RejectionReason);
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
