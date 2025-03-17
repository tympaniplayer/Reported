﻿using System.Text;
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
    private static DiscordSocketClient? _client;
    private const string AxiomApiUrl = "https://api.axiom.co/v1/datasets";
    private static ILogger? _logger;
    private static Random? _random;

    public static async Task Main()
    {
        _logger = await InitializeLogger();
        await InitializeDatabase();
        _random = new Random();
        
        var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
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

        if (string.IsNullOrEmpty(axiomDataSet) || string.IsNullOrEmpty(axiomDataSet))
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
        if (!pendingMigrations.Any())
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
        // Let's do our global command
        var reportGlobalCommand = new SlashCommandBuilder();
        reportGlobalCommand
            .WithName("report")
            .WithDescription("Report a user for being dingus")
            .WithContextTypes(InteractionContextType.PrivateChannel, InteractionContextType.BotDm, InteractionContextType.Guild)
            .AddOption("user", ApplicationCommandOptionType.User, "The user you want to report.");

        var whoReportedGlobalCommand = new SlashCommandBuilder();
        whoReportedGlobalCommand
            .WithName("who-reported")
            .WithDescription("Stats on who reported you")
            .WithContextTypes(InteractionContextType.PrivateChannel, InteractionContextType.BotDm,
                InteractionContextType.Guild);
        
        try
        {
            await _client!.CreateGlobalApplicationCommandAsync(reportGlobalCommand.Build());
            await _client.CreateGlobalApplicationCommandAsync(whoReportedGlobalCommand.Build());
        }
        catch(HttpException exception)
        {
            var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
            Console.WriteLine(json);
        }
    }

    private static Task SlashCommandHandler(SocketSlashCommand command)
    {
        using var dbContext = new ReportedDbContext();
        switch (command.CommandName)
        {
            case "report":
                return HandleReportCommand(dbContext, command);
            case "who-reported":
                return HandleWhoReportedCommand(dbContext, command);
            default:
                _logger!.Error($"Unexpected command name received: {command.CommandName} Investigate");
                return Task.CompletedTask;
        }
    }

    private static async Task HandleWhoReportedCommand(ReportedDbContext dbContext, SocketSlashCommand command)
    {
        IUser? user = command.User;

        var reportsByUser = dbContext.Set<UserReport>().Where(ur => ur.DiscordId == user.Id)
            .GroupBy(ur => ur.InitiatedUserDiscordId);

        var stringBuilder = new StringBuilder();
        foreach (var userReports in reportsByUser)
        {
            var reportUser = _client!.GetUser(userReports.Key);
            var count = userReports.Count();

            stringBuilder.AppendLine($"{reportUser.Mention}: {count} {(count > 1 ? "times" : "time")}");
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

        var count = await dbContext.Set<UserReport>().CountAsync(t => t.DiscordId == guildUser.Id);

        var random = _random!.Next(0, 100);
        if (random < 5)
        {
            for (var i = 0; i < 5; i++)
            {
                var userReport = new UserReport(initiatedUser.Id, initiatedUser.Id, true);
                dbContext.Set<UserReport>().Add(userReport);
            }
            
            await dbContext.SaveChangesAsync();
            
            await command.RespondAsync(
                $"Oof, {initiatedUser.Mention} has hurt themselves in their confusion and has reported themselves 5 times");
        }
        else
        {
            var userReport = new UserReport(guildUser.Id, initiatedUser.Id);
            dbContext.Set<UserReport>().Add(userReport);

            await dbContext.SaveChangesAsync();

            await command.RespondAsync(
                $"{guildUser.Mention} has been reported{Environment.NewLine}They have been reported {count + 1} {(count > 0 ? "times" : "time")}.");
        }
    }
}