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

    public static SlashCommandProperties AppealCountCommand() =>
        new SlashCommandBuilder()
            .WithName("appeal-count")
            .WithDescription("See how many appeals you've won")
            .WithContextTypes(InteractionContextType.PrivateChannel,
                InteractionContextType.BotDm,
                InteractionContextType.Guild)
            .Build();

    public static SlashCommandProperties SetAppealGifCommand() =>
        new SlashCommandBuilder()
            .WithName("set-appeal-gif")
            .WithDescription("Set your personal Tenor GIF for appeal outcomes")
            .WithContextTypes(InteractionContextType.PrivateChannel,
                InteractionContextType.BotDm,
                InteractionContextType.Guild)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("type")
                .WithDescription("Which appeal outcome the GIF is for")
                .WithRequired(true)
                .WithType(ApplicationCommandOptionType.String)
                .AddChoice("success", "success")
                .AddChoice("failure", "failure"))
            .AddOption("url", ApplicationCommandOptionType.String,
                "Tenor URL (tenor.com/view/... or media.tenor.com/...), or 'clear' to remove",
                isRequired: true)
            .Build();

    public static SlashCommandProperties RegisterBirthdayCommand() =>
        new SlashCommandBuilder()
            .WithName("register-birthday")
            .WithDescription("Register your birthday for report immunity (one-time, no take-backs)")
            .WithContextTypes(InteractionContextType.PrivateChannel,
                InteractionContextType.BotDm,
                InteractionContextType.Guild)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("month")
                .WithDescription("Birthday month (1-12)")
                .WithRequired(true)
                .WithType(ApplicationCommandOptionType.Integer)
                .WithMinValue(1)
                .WithMaxValue(12))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("day")
                .WithDescription("Birthday day (1-31)")
                .WithRequired(true)
                .WithType(ApplicationCommandOptionType.Integer)
                .WithMinValue(1)
                .WithMaxValue(31))
            .Build();
}