using Discord.WebSocket;
using Hackathon.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hackathon.Modules;
internal class CombatModule : ModuleBase
{
	public CombatModule(ILogger<ModuleBase> logger, DatabaseService sqliteDbService, PlayerService playerService, PlayerProfileService profileService, OpenAIService openAIService, DiscordSocketClient client, InteractionHandler interaction) : base(logger, sqliteDbService, playerService, profileService, openAIService, client, interaction)
	{
	}
}