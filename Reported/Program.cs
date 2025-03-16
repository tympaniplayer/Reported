using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Reported.Persistence;

namespace Reported;

public class Program
{
    private static DiscordSocketClient _client;

    public static async Task Main()
    {
        var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
        _client = new DiscordSocketClient();
        _client.Log += message =>
        {
            Console.WriteLine(message.ToString());
            return Task.CompletedTask;
        };
        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();
        _client.Ready += ClientReady;
        _client.SlashCommandExecuted += SlashCommandHandler;

        // Block this task until the program is closed.
        await Task.Delay(-1);
    }

    private static async Task ClientReady()
    {
        // Let's do our global command
        var globalCommand = new SlashCommandBuilder();
        globalCommand
            .WithName("report")
            .WithDescription("Report a user for being dingus")
            .WithContextTypes(InteractionContextType.PrivateChannel, InteractionContextType.BotDm, InteractionContextType.Guild)
            .AddOption("user", ApplicationCommandOptionType.User, "The user you want to report.");
        
        try
        {
            await _client.CreateGlobalApplicationCommandAsync(globalCommand.Build());
        }
        catch(HttpException exception)
        {
            var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
            Console.WriteLine(json);
        }
    }

    private static async Task SlashCommandHandler(SocketSlashCommand command)
    {
        var dbContext = new ReportedDbContext();
        
        var guildUser = (IUser)command.Data.Options.First().Value;

        var count = await dbContext.Set<UserReport>().CountAsync(t => t.DiscordId == guildUser.Id);
        var userReport = new UserReport(guildUser.Id);
        dbContext.Set<UserReport>().Add(userReport);
        
        await dbContext.SaveChangesAsync();
        
        await command.RespondAsync($"{guildUser.Mention} has been reported{Environment.NewLine}They have been reported {count + 1} {(count > 0 ? "times" : "time")}.");
    }
}