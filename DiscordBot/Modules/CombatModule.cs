using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using Hackathon.Services;
using Microsoft.Extensions.Logging;

namespace Hackathon.Modules;

internal class CombatModule(
    ILogger<ModuleBase> logger,
    DatabaseService sqliteDbService,
    PlayerService playerService,
    PlayerProfileService profileService,
    OpenAIService openAIService,
    DiscordSocketClient client,
    InteractionHandler interaction
)
    : ModuleBase(
        logger,
        sqliteDbService,
        playerService,
        profileService,
        openAIService,
        client,
        interaction
    ) { }
