using Discord.WebSocket;
using Hackathon.Services;
using Microsoft.Extensions.Logging;
using static Hackathon.Services.InteractionHandler;

namespace Hackathon.Modules;

public class TalkingModule : ModuleBase
{
    public TalkingModule(
        ILogger<ModuleBase> logger,
        DatabaseService sqliteDbService,
        PlayerService playerService,
        PlayerProfileService profileService,
        OpenAIService openAIService,
        DiscordSocketClient client,
        InteractionHandler interaction
    )
        : base(
            logger,
            sqliteDbService,
            playerService,
            profileService,
            openAIService,
            client,
            interaction
        )
    {
        interaction.OnPostBotMention += ScanAIResponse;
    }

    private async void ScanAIResponse(Object sender, BotResponseArgs args)
    {
        string response = args.Response!.ToLower();
        // scan output from ai
        // able to change it in args
        //await Console.Out.WriteLineAsync("YOOO");
    }
}
