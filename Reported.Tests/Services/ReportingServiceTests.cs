using Microsoft.EntityFrameworkCore;
using Reported.Models;
using Reported.Persistence;
using Reported.Services;
using Reported.Tests.Helpers;

namespace Reported.Tests.Services;

public sealed class ReportingServiceTests
{
    [Fact]
    public async Task CreateReport_NormalReport_CreatesOneReport()
    {
        using var factory = TestDbContextFactory.Create();
        // [50, 50] → no self-report (50 >= 5), no crit (50 != 1)
        var random = new FakeRandomProvider(50, 50);
        var service = new ReportingService(factory.Context, random);

        var result = await service.CreateReport(100UL, "Target", 200UL, "Reporter", "NA");

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.ReportCount);
        Assert.False(result.Value.IsSelfReport);
        Assert.False(result.Value.IsCriticalHit);
        Assert.Equal(100UL, result.Value.TargetDiscordId);
        Assert.Equal("Negative Attitude", result.Value.ReasonDescription);

        var dbCount = await factory.Context.Set<UserReport>().CountAsync();
        Assert.Equal(1, dbCount);
    }

    [Fact]
    public async Task CreateReport_CriticalHit_CreatesTwoReports()
    {
        using var factory = TestDbContextFactory.Create();
        // [50, 1] → no self-report (50 >= 5), crit (1 == 1)
        var random = new FakeRandomProvider(50, 1);
        var service = new ReportingService(factory.Context, random);

        var result = await service.CreateReport(100UL, "Target", 200UL, "Reporter", "VA");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.ReportCount);
        Assert.True(result.Value.IsCriticalHit);
        Assert.False(result.Value.IsSelfReport);

        var dbCount = await factory.Context.Set<UserReport>().CountAsync();
        Assert.Equal(2, dbCount);
    }

    [Fact]
    public async Task CreateReport_SelfReport_CreatesFiveReportsOnInitiator()
    {
        using var factory = TestDbContextFactory.Create();
        // [3] → self-report (3 < 5)
        var random = new FakeRandomProvider(3);
        var service = new ReportingService(factory.Context, random);

        var result = await service.CreateReport(100UL, "Target", 200UL, "Reporter", "CH");

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value.ReportCount);
        Assert.True(result.Value.IsSelfReport);
        Assert.False(result.Value.IsCriticalHit);
        Assert.Equal(200UL, result.Value.TargetDiscordId); // Reports land on initiator

        var reports = await factory.Context.Set<UserReport>().ToListAsync();
        Assert.Equal(5, reports.Count);
        Assert.All(reports, r =>
        {
            Assert.Equal(200UL, r.DiscordId);
            Assert.True(r.Confused);
        });
    }

    [Fact]
    public async Task CreateReport_CountsReflectDbState()
    {
        using var factory = TestDbContextFactory.Create();
        var db = factory.Context;

        // Seed existing reports
        db.Set<UserReport>().Add(new UserReport(100UL, "Target", 300UL, "Other", false, "NA"));
        db.Set<UserReport>().Add(new UserReport(100UL, "Target", 300UL, "Other", false, "VA"));
        await db.SaveChangesAsync();

        var random = new FakeRandomProvider(50, 50);
        var service = new ReportingService(db, random);

        var result = await service.CreateReport(100UL, "Target", 200UL, "Reporter", "NA");

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.TotalReportsOnTarget); // 2 existing + 1 new
        Assert.Equal(2, result.Value.TotalReportsOfThisType); // 1 existing NA + 1 new NA
    }

    [Fact]
    public async Task GetReportsByReporter_GroupsByReporterName()
    {
        using var factory = TestDbContextFactory.Create();
        var db = factory.Context;

        db.Set<UserReport>().Add(new UserReport(100UL, "Target", 200UL, "Alice", false, "NA"));
        db.Set<UserReport>().Add(new UserReport(100UL, "Target", 200UL, "Alice", false, "VA"));
        db.Set<UserReport>().Add(new UserReport(100UL, "Target", 300UL, "Bob", false, "CH"));
        await db.SaveChangesAsync();

        var service = new ReportingService(db, new FakeRandomProvider());

        var result = await service.GetReportsByReporter(100UL);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);

        var alice = result.Value.First(g => g.GroupKey == "Alice");
        var bob = result.Value.First(g => g.GroupKey == "Bob");
        Assert.Equal(2, alice.Count);
        Assert.Equal(1, bob.Count);
    }

    [Fact]
    public async Task GetReportsByReason_GroupsByReason_HandlesNullDescription()
    {
        using var factory = TestDbContextFactory.Create();
        var db = factory.Context;

        db.Set<UserReport>().Add(new UserReport(100UL, "Target", 200UL, "Reporter", false, "NA"));
        db.Set<UserReport>().Add(new UserReport(100UL, "Target", 200UL, "Reporter", false, "NA"));
        db.Set<UserReport>().Add(new UserReport(100UL, "Target", 200UL, "Reporter", false, null));
        await db.SaveChangesAsync();

        var service = new ReportingService(db, new FakeRandomProvider());

        var result = await service.GetReportsByReason(100UL);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);

        var naGroup = result.Value.First(g => g.GroupKey == "NA");
        Assert.Equal("Negative Attitude", naGroup.DisplayName);
        Assert.Equal(2, naGroup.Count);

        var unknownGroup = result.Value.First(g => g.DisplayName == "Unknown Reason");
        Assert.Equal(1, unknownGroup.Count);
    }
}
