using Discord;

namespace Reported;

public static class Commands
{
    public static SlashCommandProperties ReportCommand()
    {
        var builder = new SlashCommandBuilder()
            .WithName("report")
            .WithDescription("Report a user for being dingus")
            .WithContextTypes(InteractionContextType.PrivateChannel,
                InteractionContextType.BotDm,
                InteractionContextType.Guild)
            .AddOption("user", ApplicationCommandOptionType.User,
                "The user you want to report.",
                isRequired: true);

        var reportReasonBuilder = new SlashCommandOptionBuilder()
            .WithName("reason")
            .WithDescription("The reason for reporting")
            .WithRequired(true);
        foreach (var reportReason in Constants.ReportReasons.Where(keyValuePair => keyValuePair.Key != "DU"))
        {
            reportReasonBuilder.AddChoice(reportReason.Value, reportReason.Key);
        }

        reportReasonBuilder.WithType(ApplicationCommandOptionType.String);
        builder.AddOption(reportReasonBuilder);

        return builder.Build();
    }

    public static SlashCommandProperties WhoReportedCommand() =>
        new SlashCommandBuilder()
            .WithName("who-reported")
            .WithDescription("Stats on who reported you")
            .WithContextTypes(InteractionContextType.PrivateChannel,
                InteractionContextType.BotDm,
                InteractionContextType.Guild)
            .Build();

    public static SlashCommandProperties AliasListCommand() =>
        new SlashCommandBuilder()
            .WithName("alias-list")
            .WithDescription("A list of aliases that can be used when reporting")
            .WithContextTypes(InteractionContextType.PrivateChannel,
                InteractionContextType.BotDm,
                InteractionContextType.Guild)
            .Build();

    public static SlashCommandProperties WhyReportedCommand() =>
        new SlashCommandBuilder()
            .WithName("why-reported")
            .WithDescription("Stats on the reasons you were reported")
            .WithContextTypes(InteractionContextType.PrivateChannel,
                InteractionContextType.BotDm,
                InteractionContextType.Guild)
            .Build();

    public static SlashCommandProperties AppealCommand() =>
        new SlashCommandBuilder()
            .WithName("appeal")
            .WithDescription("Appeal to the bot to have a report removed")
            .WithContextTypes(InteractionContextType.PrivateChannel,
                InteractionContextType.BotDm,
                InteractionContextType.Guild)
            .Build();
}