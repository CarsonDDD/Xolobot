using Discord.Interactions;
using Discord.WebSocket;
using Hackathon.Services;
using Microsoft.Extensions.Logging;

namespace Hackathon.Modules;
public abstract class ModuleBase : InteractionModuleBase<SocketInteractionContext>
{
	protected readonly ILogger<ModuleBase> _logger;
	protected readonly OpenAIService _openAI;
	protected readonly DiscordSocketClient _client;
	protected readonly InteractionHandler _interaction;

	protected readonly DatabaseService _sqliteDatabase;
	protected readonly PlayerService _playerService;
	protected readonly PlayerProfileService _profileService;

	public ModuleBase(
		ILogger<ModuleBase> logger,
		DatabaseService sqliteDbService,
		PlayerService playerService,
		PlayerProfileService profileService,
		OpenAIService openAIService,
		DiscordSocketClient client,
		InteractionHandler interaction)
	{
		_logger = logger;
		_sqliteDatabase = sqliteDbService;
		_playerService = playerService;
		_profileService = profileService;
		_openAI = openAIService;
		_client = client;
		_interaction = interaction;
	}

	/*[SlashCommand("test", "Just a test command")]
	public async Task TestCommand()
	{
		await RespondAsync("Hello There");
	}*/
}
