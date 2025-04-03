using Discord.Interactions;
using Discord.WebSocket;
using Hackathon.Services;
using Microsoft.Extensions.Logging;
using Hackathon.Managers.Shop;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text;
using Discord;

namespace Hackathon.Modules;

[Group("shop", "commands for interacting with xolobot")]
public class ShopModule : ModuleBase
{
	public ShopModule(ILogger<ModuleBase> logger, DatabaseService sqliteDbService, PlayerService playerService, PlayerProfileService profileService, OpenAIService openAIService, DiscordSocketClient client, InteractionHandler interaction) : base(logger, sqliteDbService, playerService, profileService, openAIService, client, interaction)
	{
	}



	[SlashCommand("buy", "Buy item from the shop")]
	public async Task Buy(string itemName, int quantity = 1, int? priceOverride = null) { /*...*/ }

	[SlashCommand("sell", "Sell item to the shop")]
	public async Task Sell(string itemName, int quantity = 1, int? priceOverride = null) { /*...*/ }

	[SlashCommand("open", "Open the shop interface")]
	public async Task OpenShop(bool list = true, string? filter = null) { /*...*/ }
}