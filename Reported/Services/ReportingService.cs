using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Reported.Models;
using Reported.Persistence;

namespace Reported.Services;

public sealed class ReportingService(ReportedDbContext dbContext, IRandomProvider random)
{
    private readonly ReportedDbContext _dbContext = dbContext;
    private readonly IRandomProvider _random = random;

    public async Task<Result<ReportOutcome>> CreateReport(
        ulong targetDiscordId,
        string targetName,
        ulong initiatorDiscordId,
        string initiatorName,
        string reasonCode)
    {
        try
        {
            var reasonDescription = Constants.ReportReasons[reasonCode];

            // Self-report check: 5% chance
            var selfReportRoll = _random.Next(0, 100);
            if (selfReportRoll < 5)
            {
                if (await IsBirthdayToday(initiatorDiscordId))
                {
                    return Result.Success(new ReportOutcome(
                        initiatorDiscordId, initiatorName,
                        reasonCode, reasonDescription,
                        ReportCount: 0,
                        TotalReportsOnTarget: await _dbContext.Set<UserReport>()
                            .CountAsync(r => r.DiscordId == initiatorDiscordId),
                        TotalReportsOfThisType: await _dbContext.Set<UserReport>()
                            .CountAsync(r => r.DiscordId == initiatorDiscordId && r.Description == reasonCode),
                        IsCriticalHit: false,
                        IsSelfReport: true,
                        IsBirthdayImmune: true));
                }

                for (var i = 0; i < 5; i++)
                {
                    _dbContext.Set<UserReport>().Add(new UserReport(
                        initiatorDiscordId, initiatorName,
                        initiatorDiscordId, initiatorName,
                        true, reasonCode));
                }

                await _dbContext.SaveChangesAsync();

                var totalOnInitiator = await _dbContext.Set<UserReport>()
                    .CountAsync(r => r.DiscordId == initiatorDiscordId);
                var totalOfTypeOnInitiator = await _dbContext.Set<UserReport>()
                    .CountAsync(r => r.DiscordId == initiatorDiscordId && r.Description == reasonCode);

                return Result.Success(new ReportOutcome(
                    initiatorDiscordId, initiatorName,
                    reasonCode, reasonDescription,
                    ReportCount: 5,
                    TotalReportsOnTarget: totalOnInitiator,
                    TotalReportsOfThisType: totalOfTypeOnInitiator,
                    IsCriticalHit: false,
                    IsSelfReport: true));
            }

            if (await IsBirthdayToday(targetDiscordId))
            {
                var totalOnTarget = await _dbContext.Set<UserReport>()
                    .CountAsync(r => r.DiscordId == targetDiscordId);
                var totalOfTypeOnTarget = await _dbContext.Set<UserReport>()
                    .CountAsync(r => r.DiscordId == targetDiscordId && r.Description == reasonCode);
                return Result.Success(new ReportOutcome(
                    targetDiscordId, targetName,
                    reasonCode, reasonDescription,
                    ReportCount: 0,
                    TotalReportsOnTarget: totalOnTarget,
                    TotalReportsOfThisType: totalOfTypeOnTarget,
                    IsCriticalHit: false,
                    IsSelfReport: false,
                    IsBirthdayImmune: true));
            }

            // Critical hit check: 1% chance
            var criticalRoll = _random.Next(0, 100);
            var isCriticalHit = criticalRoll == 1;
            var times = isCriticalHit ? 2 : 1;

            var existingTotal = await _dbContext.Set<UserReport>()
                .CountAsync(r => r.DiscordId == targetDiscordId);
            var existingOfType = await _dbContext.Set<UserReport>()
                .CountAsync(r => r.DiscordId == targetDiscordId && r.Description == reasonCode);

            for (var i = 0; i < times; i++)
            {
                _dbContext.Set<UserReport>().Add(new UserReport(
                    targetDiscordId, targetName,
                    initiatorDiscordId, initiatorName,
                    false, reasonCode));

                await _dbContext.SaveChangesAsync();
            }

            return Result.Success(new ReportOutcome(
                targetDiscordId, targetName,
                reasonCode, reasonDescription,
                ReportCount: times,
                TotalReportsOnTarget: existingTotal + times,
                TotalReportsOfThisType: existingOfType + times,
                IsCriticalHit: isCriticalHit,
                IsSelfReport: false));
        }
        catch (Exception ex)
        {
            return Result.Failure<ReportOutcome>(ex.Message);
        }
    }

    private async Task<bool> IsBirthdayToday(ulong discordId)
    {
        var prefs = await _dbContext.Set<UserPreferences>()
            .FirstOrDefaultAsync(p => p.DiscordId == discordId);
        if (prefs?.BirthdayMonth is null || prefs.BirthdayDay is null)
            return false;
        var today = DateTime.UtcNow;
        return prefs.BirthdayMonth == today.Month && prefs.BirthdayDay == today.Day;
    }

    public async Task<Result<IReadOnlyList<ReportGroup>>> GetReportsByReporter(ulong userDiscordId)
    {
        try
        {
            var groups = await _dbContext.Set<UserReport>()
                .Where(r => r.DiscordId == userDiscordId)
                .GroupBy(r => r.InitiatedDiscordName)
                .Select(g => new ReportGroup(g.Key, g.Key, g.Count()))
                .ToListAsync();

            return Result.Success<IReadOnlyList<ReportGroup>>(groups);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<ReportGroup>>(ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<ReportGroup>>> GetReportsByReason(ulong userDiscordId)
    {
        try
        {
            var groups = await _dbContext.Set<UserReport>()
                .Where(r => r.DiscordId == userDiscordId)
                .GroupBy(r => r.Description)
                .Select(g => new { Key = g.Key, Count = g.Count() })
                .ToListAsync();

            var result = groups.Select(g =>
            {
                if (string.IsNullOrWhiteSpace(g.Key))
                    return new ReportGroup("", "Unknown Reason", g.Count);

                var displayName = Constants.ReportReasons.TryGetValue(g.Key, out var name)
                    ? name
                    : g.Key;
                return new ReportGroup(g.Key, displayName, g.Count);
            }).ToList();

            return Result.Success<IReadOnlyList<ReportGroup>>(result);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<ReportGroup>>(ex.Message);
        }
    }
}
