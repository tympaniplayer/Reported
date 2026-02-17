using System.Text;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;
using Reported.External;
using Reported.Models;
using Reported.Persistence;
using Reported.Services;
using Serilog;
using Serilog.Formatting.Elasticsearch;

namespace Reported;

public static class Program
{
    private static DiscordSocketClient _client = null!;
    private const string AxiomApiUrl = "https://api.axiom.co/v1/datasets";
    private static ILogger? _logger;
    private static IRandomProvider _randomProvider = null!;

    public static async Task Main()
    {
        _logger = await InitializeLogger();
        await InitializeDatabase();
        _randomProvider = new RandomProvider();

        var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN")
                    ?? throw new InvalidOperationException("Discord token environment variable not set");
        _client = new DiscordSocketClient();
        _client.Log += message =>
        {
            _logger.Information(message.ToString());
            return Task.CompletedTask;
        };
        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();
        _client.Ready += ClientReady;
        _client.SlashCommandExecuted += SlashCommandHandler;

        // Block this task until the program is closed.
        await Task.Delay(-1);
    }

    private static Task<ILogger> InitializeLogger()
    {
        var axiomToken = Environment.GetEnvironmentVariable("AXIOM_TOKEN");
        var axiomDataSet = Environment.GetEnvironmentVariable("AXIOM_DATASET");

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console();

        if (!string.IsNullOrWhiteSpace(axiomToken) && !string.IsNullOrWhiteSpace(axiomDataSet))
        {
            loggerConfig = loggerConfig.WriteTo.Http(
                requestUri: $"{AxiomApiUrl}/{axiomDataSet}/ingest",
                queueLimitBytes: null,
                textFormatter: new ElasticsearchJsonFormatter(renderMessageTemplate: false, inlineFields: true),
                httpClient: new AxiomHttpClient(axiomToken));
        }

        var logger = loggerConfig.CreateLogger();

        if (string.IsNullOrWhiteSpace(axiomToken) || string.IsNullOrWhiteSpace(axiomDataSet))
        {
            logger.Warning("Axiom environment variables not set - remote logging disabled");
        }

        return Task.FromResult(Log.Logger = logger);
    }

    private static async Task InitializeDatabase()
    {
        await ReportedDbContext.InitializeDatabaseAsync();
    }

    private static async Task ClientReady()
    {
        try
        {
            await _client.CreateGlobalApplicationCommandAsync(Commands.ReportCommand());
            await _client.CreateGlobalApplicationCommandAsync(Commands.WhoReportedCommand());
            await _client.CreateGlobalApplicationCommandAsync(Commands.AliasListCommand());
            await _client.CreateGlobalApplicationCommandAsync(Commands.WhyReportedCommand());
            await _client.CreateGlobalApplicationCommandAsync(Commands.AppealCommand());
            await _client.CreateGlobalApplicationCommandAsync(Commands.AppealCountCommand());
        }
        catch (HttpException exception)
        {
            var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
            Console.WriteLine(json);
        }
    }

    private static async Task SlashCommandHandler(SocketSlashCommand command)
    {
        await using var dbContext = new ReportedDbContext();
        switch (command.CommandName)
        {
            case "report":
                await HandleReportCommand(dbContext, command);
                break;
            case "who-reported":
                await HandleWhoReportedCommand(dbContext, command);
                break;
            case "alias-list":
                await HandleAliasListCommand(command);
                break;
            case "why-reported":
                await HandleWhyReportedCommand(dbContext, command);
                break;
            case "appeal":
                await HandleAppeal(dbContext, command);
                break;
            case "appeal-count":
                await HandleAppealCount(dbContext, command);
                break;
            default:
                _logger!.Error($"Unexpected command name received: {command.CommandName} Investigate");
                break;
        }
    }

    private static async Task HandleAppeal(ReportedDbContext dbContext, SocketSlashCommand command)
    {
        var user = command.User;
        var appealService = new AppealService(dbContext, _randomProvider);
        var result = await appealService.ProcessAppeal(user.Id, user.Mention);

        if (result.IsFailure)
        {
            _logger!.Error("Appeal failed for {DiscordId}: {Error}", user.Id, result.Error);
            return;
        }

        var outcome = result.Value;

        if (outcome.HadNoReports)
        {
            await command.RespondAsync(
                $"{user.Mention}, LOL you didn't have any reports to appeal. Here is 10 to get you started");
        }
        else if (outcome.Won)
        {
            _logger!.Information(
                "Appeal outcome for {DiscordId}: {AppealOutcome}, total wins: {TotalWins}, total attempts: {TotalAttempts}",
                user.Id, "won", outcome.AppealWins, outcome.AppealAttempts);

            await command.RespondAsync(
                $"{user.Mention}, you have been treated poorly. Appeal approved :white_check_mark:");
            await command.FollowupAsync(
                "https://tenor.com/view/tiger-woods-stare-we-can-do-it-gif-11974968");
        }
        else
        {
            _logger!.Information(
                "Appeal outcome for {DiscordId}: {AppealOutcome}, total wins: {TotalWins}, total attempts: {TotalAttempts}",
                user.Id, "lost", outcome.AppealWins, outcome.AppealAttempts);

            await command.RespondAsync(
                $"{user.Mention}, no you deserved that report. Appeal denied :no_entry_sign: ");
        }
    }

    private static async Task HandleAppealCount(ReportedDbContext dbContext, SocketSlashCommand command)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var user = command.User;

        var appealService = new AppealService(dbContext, _randomProvider);
        var result = await appealService.GetAppealStats(user.Id);

        stopwatch.Stop();
        _logger!.Information(
            "Appeal count query for {DiscordId} completed in {ElapsedMs}ms",
            user.Id, stopwatch.ElapsedMilliseconds);

        if (result.IsFailure)
        {
            _logger.Error("Appeal count failed for {DiscordId}: {Error}", user.Id, result.Error);
            return;
        }

        var stats = result.Value;

        string message;
        if (stats.Attempts == 0)
        {
            message = $"{user.Mention}, you haven't even tried to appeal yet. Coward.";
        }
        else if (stats.Wins == 0)
        {
            message = $"{user.Mention}, you've won 0 out of {stats.Attempts} appeals (0%). Yikes.";
        }
        else if (stats.WinRate < 30)
        {
            message = $"{user.Mention}, you've won {stats.Wins} out of {stats.Attempts} appeals ({stats.WinRate}%). This bot sucks.";
        }
        else if (stats.WinRate > 64)
        {
            message = $"{user.Mention}, you've won {stats.Wins} out of {stats.Attempts} appeals ({stats.WinRate}%). The bot smiles on you";
        }
        else
        {
            message = $"{user.Mention}, you've won {stats.Wins} out of {stats.Attempts} appeals ({stats.WinRate}%).";
        }

        await command.RespondAsync(message);
    }

    private static async Task HandleWhyReportedCommand(ReportedDbContext dbContext, SocketSlashCommand command)
    {
        IUser? user = command.User;
        var reportingService = new ReportingService(dbContext, _randomProvider);
        var result = await reportingService.GetReportsByReason(user.Id);

        if (result.IsFailure)
        {
            _logger!.Error("Why-reported failed for {DiscordId}: {Error}", user.Id, result.Error);
            return;
        }

        var stringBuilder = new StringBuilder();
        foreach (var group in result.Value)
        {
            if (group.GroupKey == "")
            {
                stringBuilder.AppendLine($"Unknown Reason: {group.Count} {(group.Count > 1 ? "times" : "time")}");
            }
            else
            {
                stringBuilder.AppendLine(
                    $"{group.DisplayName},: {group.Count} {(group.Count > 1 ? "times" : "time")}");
            }
        }

        var builder = new EmbedBuilder()
            .WithTitle("This is a list of reasons you have been reported")
            .WithDescription(stringBuilder.ToString())
            .WithColor(Color.Red)
            .WithCurrentTimestamp();
        await command.RespondAsync(embed: builder.Build(), ephemeral: true);
    }

    private static async Task HandleAliasListCommand(SocketSlashCommand command)
    {
        var stringBuilder = new StringBuilder();
        foreach (var keyValuePair in Constants.ReportReasons.Where(keyValuePair => keyValuePair.Key != "DU"))
        {
            stringBuilder.AppendLine($"{keyValuePair.Key}: {keyValuePair.Value}");
        }

        var builder = new EmbedBuilder()
            .WithTitle("Alias List of Report Reasons (Alias:Reason)")
            .WithDescription(stringBuilder.ToString())
            .WithColor(Color.Red);

        await command.RespondAsync(embed: builder.Build());
    }

    private static async Task HandleWhoReportedCommand(ReportedDbContext dbContext, SocketSlashCommand command)
    {
        IUser? user = command.User;
        var reportingService = new ReportingService(dbContext, _randomProvider);
        var result = await reportingService.GetReportsByReporter(user.Id);

        if (result.IsFailure)
        {
            _logger!.Error("Who-reported failed for {DiscordId}: {Error}", user.Id, result.Error);
            return;
        }

        var stringBuilder = new StringBuilder();
        foreach (var group in result.Value)
        {
            stringBuilder.AppendLine($"{group.DisplayName}: {group.Count} {(group.Count > 1 ? "times" : "time")}");
        }

        var builder = new EmbedBuilder();
        builder
            .WithTitle("This is who has reported you")
            .WithDescription(stringBuilder.ToString())
            .WithColor(Color.Default)
            .WithCurrentTimestamp();

        await command.RespondAsync(embed: builder.Build(), ephemeral: true);
    }

    private static async Task HandleReportCommand(ReportedDbContext dbContext, SocketSlashCommand command)
    {
        var guildUser = (IUser)command.Data.Options.First().Value;
        IUser? initiatedUser = command.User;
        var reason = (string)command.Data.Options.First(o => o.Name == "reason").Value;

        var reportingService = new ReportingService(dbContext, _randomProvider);
        var result = await reportingService.CreateReport(
            guildUser.Id, guildUser.Mention,
            initiatedUser.Id, initiatedUser.Mention,
            reason);

        if (result.IsFailure)
        {
            _logger!.Error("Report failed: {Error}", result.Error);
            return;
        }

        var outcome = result.Value;

        if (outcome.IsSelfReport)
        {
            await command.RespondAsync(
                $"Oof, {initiatedUser.Mention} has hurt themselves in their confusion and has reported themselves 5 times");
        }
        else
        {
            await command.RespondAsync(
                $"{guildUser.Mention} has been reported for {outcome.ReasonDescription}" +
                $"{Environment.NewLine}They have been reported for {outcome.ReasonDescription} {outcome.TotalReportsOfThisType} {(outcome.TotalReportsOfThisType > 1 ? "times" : "time")}" +
                $"{Environment.NewLine}They have been reported {outcome.TotalReportsOnTarget} {(outcome.TotalReportsOnTarget > 1 ? "times" : "time")}.");
            if (outcome.IsCriticalHit)
            {
                await command.FollowupAsync($"Critical hit! {guildUser.Mention} has been reported twice!");
            }
        }
    }
}
