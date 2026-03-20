using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Reported.Models;
using Reported.Persistence;

namespace Reported.Services;

public sealed class AppealService
{
    private readonly ReportedDbContext _dbContext;
    private readonly IRandomProvider _random;

    public AppealService(ReportedDbContext dbContext, IRandomProvider random)
    {
        _dbContext = dbContext;
        _random = random;
    }

    public async Task<Result<AppealOutcome>> ProcessAppeal(ulong userDiscordId, string userName)
    {
        try
        {
            // Find the oldest eligible report: unappealed and not self-initiated
            var report = await _dbContext.Set<UserReport>()
                .OrderBy(r => r.Id)
                .FirstOrDefaultAsync(r =>
                    r.DiscordId == userDiscordId
                    && !r.HasBeenAppealed
                    && r.DiscordId != r.InitiatedUserDiscordId);

            if (report is null)
            {
                // Determine why no eligible report was found
                var mostRecentReport = await _dbContext.Set<UserReport>()
                    .OrderByDescending(r => r.Id)
                    .FirstOrDefaultAsync(r => r.DiscordId == userDiscordId);

                if (mostRecentReport is null)
                {
                    // No reports at all — penalty: add 10 "DU" reports, do NOT create AppealRecord
                    for (var i = 0; i < 10; i++)
                    {
                        _dbContext.Set<UserReport>().Add(new UserReport(
                            userDiscordId, userName,
                            userDiscordId, userName,
                            true, "DU"));
                    }

                    await _dbContext.SaveChangesAsync();

                    return Result.Success(new AppealOutcome(
                        Won: false,
                        AppealWins: 0,
                        AppealAttempts: 0,
                        HadNoReports: true,
                        PenaltyReportsAdded: 10));
                }

                // Has reports but none eligible — check the most recent to determine why
                var isSelfReport = mostRecentReport.DiscordId == mostRecentReport.InitiatedUserDiscordId
                    && !mostRecentReport.HasBeenAppealed;

                var rejectionReason = isSelfReport
                    ? AppealRejectionReason.OnlySelfReports
                    : AppealRejectionReason.AllAppealed;

                return Result.Success(new AppealOutcome(
                    Won: false,
                    AppealWins: 0,
                    AppealAttempts: 0,
                    HadNoReports: false,
                    PenaltyReportsAdded: 0,
                    RejectionReason: rejectionReason));
            }

            var coinToss = _random.Next(0, 100);

            var appealRecord = await _dbContext.Set<AppealRecord>()
                .FirstOrDefaultAsync(a => a.DiscordId == userDiscordId);

            if (appealRecord is null)
            {
                appealRecord = new AppealRecord(userDiscordId, userName);
                _dbContext.Set<AppealRecord>().Add(appealRecord);
            }

            if (coinToss > 49)
            {
                // Win — mark the report as appealed (preserve history)
                appealRecord.AppealWins++;
                appealRecord.AppealAttempts++;
                report.HasBeenAppealed = true;
                await _dbContext.SaveChangesAsync();

                return Result.Success(new AppealOutcome(
                    Won: true,
                    AppealWins: appealRecord.AppealWins,
                    AppealAttempts: appealRecord.AppealAttempts,
                    HadNoReports: false,
                    PenaltyReportsAdded: 0));
            }
            else
            {
                // Loss — mark as appealed and add 1 penalty report (pre-marked as appealed)
                appealRecord.AppealAttempts++;
                report.HasBeenAppealed = true;

                _dbContext.Set<UserReport>().Add(new UserReport(
                    userDiscordId, userName,
                    userDiscordId, userName,
                    false, report.Description,
                    hasBeenAppealed: true));

                await _dbContext.SaveChangesAsync();

                return Result.Success(new AppealOutcome(
                    Won: false,
                    AppealWins: appealRecord.AppealWins,
                    AppealAttempts: appealRecord.AppealAttempts,
                    HadNoReports: false,
                    PenaltyReportsAdded: 1));
            }
        }
        catch (Exception ex)
        {
            return Result.Failure<AppealOutcome>(ex.Message);
        }
    }

    public async Task<Result<AppealStats>> GetAppealStats(ulong userDiscordId)
    {
        try
        {
            var appealRecord = await _dbContext.Set<AppealRecord>()
                .FirstOrDefaultAsync(a => a.DiscordId == userDiscordId);

            var wins = appealRecord?.AppealWins ?? 0;
            var attempts = appealRecord?.AppealAttempts ?? 0;
            var rate = attempts > 0 ? (int)Math.Round((double)wins / attempts * 100) : 0;

            return Result.Success(new AppealStats(wins, attempts, rate));
        }
        catch (Exception ex)
        {
            return Result.Failure<AppealStats>(ex.Message);
        }
    }
}
