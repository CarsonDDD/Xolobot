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
	public async Task OpenShop(bool detailed = false, string? filter = null)
	{
		await DeferAsync();

		var shopkeeper = _client.GetUser(ShopManager.SHOP_DISCORD_ID);

		// Build the shop page using page index 0, compact view (detailed=false), no filter.
		var result = ShopManager.Instance.BuildShopPage(shopkeeper, 0, _profileService, _playerService, detailed, filter);
		if (result == null)
		{
			await FollowupAsync("The shop is currently empty.");
			return;
		}

		var (embed, components) = result.Value;
		await FollowupAsync(embed: embed, components: components);
	}

}