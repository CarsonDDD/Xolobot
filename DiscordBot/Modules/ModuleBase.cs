using Discord.Interactions;
using Discord.WebSocket;
using Hackathon.Services;
using Microsoft.Extensions.Logging;

namespace Hackathon.Modules;

public abstract class ModuleBase(
    ILogger<ModuleBase> logger,
    DatabaseService sqliteDbService,
    PlayerService playerService,
    PlayerProfileService profileService,
    OpenAIService openAIService,
    DiscordSocketClient client,
    InteractionHandler interaction
) : InteractionModuleBase<SocketInteractionContext>
{
    protected readonly ILogger<ModuleBase> _logger = logger;
    protected readonly OpenAIService _openAI = openAIService;
    protected readonly DiscordSocketClient _client = client;
    protected readonly InteractionHandler _interaction = interaction;

    protected readonly DatabaseService _sqliteDatabase = sqliteDbService;
    protected readonly PlayerService _playerService = playerService;
    protected readonly PlayerProfileService _profileService = profileService;

    /*[SlashCommand("test", "Just a test command")]
    public async Task TestCommand()
    {
        await RespondAsync("Hello There");
    }*/
}
