using Discord.WebSocket;
using Serilog;
using Reported.Services;

namespace Reported;

public sealed class BotApplication
{
    public ILogger Logger { get; }
    public IRandomProvider RandomProvider { get; }
    public DiscordSocketClient Client { get; }

    public BotApplication(ILogger logger, IRandomProvider randomProvider, DiscordSocketClient client)
    {
        Logger = logger;
        RandomProvider = randomProvider;
        Client = client;
    }
}
