using System.Text;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Reported.External;
using Reported.Persistence;
using Serilog;
using Serilog.Formatting.Elasticsearch;

namespace Reported;

public static class Program
{
    private static DiscordSocketClient _client = null!;
    private const string AxiomApiUrl = "https://api.axiom.co/v1/datasets";
    private static ILogger? _logger;
    private static Random? _random;

    public static async Task Main()
    {
        _logger = await InitializeLogger();
        await InitializeDatabase();
        _random = new Random();

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

        if (string.IsNullOrWhiteSpace(axiomToken) || string.IsNullOrWhiteSpace(axiomDataSet))
        {
            throw new InvalidOperationException("Axiom environment variables not set");
        }

        return Task.FromResult(Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.Http(
                requestUri: $"{AxiomApiUrl}/{axiomDataSet}/ingest",
                queueLimitBytes: null,
                textFormatter: new ElasticsearchJsonFormatter(renderMessageTemplate: false, inlineFields: true),
                httpClient: new AxiomHttpClient(axiomToken!))
            .CreateLogger());
    }

    private static async Task InitializeDatabase()
    {
        var dbContext = new ReportedDbContext();
        await dbContext.Database.EnsureCreatedAsync();

        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
        {
            _logger!.Information("Applying database migrations...");
            await dbContext.Database.MigrateAsync();
            _logger.Information("Migrations applied");
        }
        else
        {
            _logger!.Information("No pending migrations.");
        }
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
            default:
                _logger!.Error($"Unexpected command name received: {command.CommandName} Investigate");
                break;
        }
    }

    private static async Task HandleAppeal(ReportedDbContext dbContext, SocketSlashCommand command)
    {
        var user = command.User;
        var report = await dbContext.Set<UserReport>().FirstOrDefaultAsync(r => r.DiscordId == user.Id);

        if (report is null)
        {
            for (var i = 0; i < 10; i++)
            {
                var userReport = new UserReport(user.Id,
                    user.Mention,
                    user.Id,
                    user.Mention,
                    true,
                    "DU");
                dbContext.Set<UserReport>().Add(userReport);
            }
            
            await command.RespondAsync(
                $"{user.Mention}, LOL you didn't have any reports to appeal. Here is 10 to get you started");
        }
        else
        {
            var random = new Random();
            var coinToss = random.Next(0, 100);
            if (coinToss > 49)
            {
                dbContext.Set<UserReport>().Remove(report);
                await dbContext.SaveChangesAsync();
                await command.RespondAsync(
                    $"{user.Mention}, you have been treated poorly. Appeal approved :white_check_mark:");
            }
            else
            {
                await command.RespondAsync(
                    $"{user.Mention}, no you deserved that report. Appeal denied :no_entry_sign: ");
            }
        }
    }

    private static async Task HandleWhyReportedCommand(ReportedDbContext dbContext, SocketSlashCommand command)
    {
        IUser? user = command.User;

        var reportsByReason = dbContext.Set<UserReport>().Where(ur => ur.DiscordId == user.Id)
            .GroupBy(ur => ur.Description);

        var stringBuilder = new StringBuilder();
        foreach (var reportGroup in reportsByReason)
        {
            var count = reportGroup.Count();
            if (string.IsNullOrWhiteSpace(reportGroup.Key))
            {
                stringBuilder.AppendLine($"Unknown Reason: {count} {(count > 1 ? "times" : "time")}");
            }
            else
            {
                stringBuilder.AppendLine(
                    $"{Constants.ReportReasons[reportGroup.Key]},: {count} {(count > 1 ? "times" : "time")}");
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

        var reportsByUser = dbContext.Set<UserReport>().Where(ur => ur.DiscordId == user.Id)
            .GroupBy(ur => ur.InitiatedDiscordName);

        var stringBuilder = new StringBuilder();
        foreach (var userReports in reportsByUser)
        {
            var count = userReports.Count();

            stringBuilder.AppendLine($"{userReports.Key}: {count} {(count > 1 ? "times" : "time")}");
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

        var userReports = dbContext.Set<UserReport>().Where(t => t.DiscordId == guildUser.Id);

        var random = _random!.Next(0, 100);
        if (random < 5)
        {
            for (var i = 0; i < 5; i++)
            {
                var userReport = new UserReport(initiatedUser.Id,
                    initiatedUser.Mention, initiatedUser.Id,
                    initiatedUser.Mention,
                    true,
                    reason);
                dbContext.Set<UserReport>().Add(userReport);
            }

            await dbContext.SaveChangesAsync();

            await command.RespondAsync(
                $"Oof, {initiatedUser.Mention} has hurt themselves in their confusion and has reported themselves 5 times");
        }
        else
        {
            var criticalRoll = _random!.Next(0, 100);
            int times = criticalRoll == 1 ? 2 : 1;
            var count = await userReports.CountAsync();
            var countOfThisType = await userReports.CountAsync(t => t.Description == reason);
            for (var i = 0; i < times; i++)
            {

                var userReport = new UserReport(guildUser.Id,
                    guildUser.Mention,
                    initiatedUser.Id,
                    initiatedUser.Mention,
                    false,
                    reason);
                dbContext.Set<UserReport>().Add(userReport);

                await dbContext.SaveChangesAsync();
            }

            var reasonExplained = Constants.ReportReasons[reason];
            await command.RespondAsync(
                $"{guildUser.Mention} has been reported for {reasonExplained}" +
                $"{Environment.NewLine}They have been reported for {reasonExplained} {countOfThisType + times} {(countOfThisType > 0 ? "times" : "time")}" +
                $"{Environment.NewLine}They have been reported {count + times} {(count > 0 ? "times" : "time")}.");
            if (criticalRoll == 1)
            {
                await command.FollowupAsync($"Critical hit! {guildUser.Mention} has been reported twice!");
            }
        }
    }
}