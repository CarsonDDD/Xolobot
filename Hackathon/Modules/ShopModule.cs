using Discord.Interactions;
using Discord.WebSocket;
using Hackathon.Services;
using Microsoft.Extensions.Logging;
using Hackathon.Managers.Shop;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text;
using Discord;
using Hackathon.DomainObjects;
using Hackathon.Utils;

namespace Hackathon.Modules;

[Group("shop", "commands for interacting with xolobot")]
public class ShopModule : ModuleBase
{
	public ShopModule(ILogger<ModuleBase> logger, DatabaseService sqliteDbService, PlayerService playerService, PlayerProfileService profileService, OpenAIService openAIService, DiscordSocketClient client, InteractionHandler interaction) : base(logger, sqliteDbService, playerService, profileService, openAIService, client, interaction)
	{
	}

	[SlashCommand("buy", "Buy item from the shop")]
	public async Task Buy(string? searchTerm = null)
	{
		// open buy menu. No raw commands!
		// verify item, if not exist, tell user and default to first item

		await DeferAsync(ephemeral: true);

		ItemStack? startingItem = null;
		var shopKeeper = _profileService.GetProfileByDiscordId(ShopManager.SHOP_DISCORD_ID.ToString());
		if (shopKeeper == null || shopKeeper.Inventory == null)
		{
			await FollowupAsync("Shopkeeper not found or shop is empty.", ephemeral: true);
			return;
		}

		var player = _playerService.GetByDiscordId(Context.User.Id.ToString());
		if (player == null)
		{
			await FollowupAsync("You are not registered.", ephemeral: true);
			return;
		}

		var startingAmount = 0;

		string[] filter = Utils.Utils.Instance.DecodeTagFilter(searchTerm!);

		// Build the shop page using page index 0, compact view (detailed=false), no filter.
		var result = ShopManager.Instance.BuildBuyInteract(_client, player, startingItem, shopKeeper, filter, startingAmount);

		var (embed, components) = result.Value;
		await FollowupAsync(embed: embed, components: components, ephemeral: true);
	}

	[SlashCommand("sell", "Sell an item from your inventory")]
	public async Task Sell(string? searchTerm = null)
	{
		// open buy menu. No raw commands!
		// verify item, if not exist, tell user and default to first item

		await DeferAsync(ephemeral: true);

		ItemStack? startingItem = null;
		var playerSeller = _profileService.GetProfileByDiscordId(Context.User.Id.ToString());
		if (playerSeller == null || playerSeller.Inventory == null)
		{
			await FollowupAsync("You are not registered.", ephemeral: true);
			return;
		}

		var buyer = _playerService.GetByDiscordId(ShopManager.SHOP_DISCORD_ID.ToString());
		if (buyer == null)
		{
			await FollowupAsync("Buyer not found.", ephemeral: true);
			return;
		}

		var startingAmount = 0;

		string[] filter = Utils.Utils.Instance.DecodeTagFilter(searchTerm!);

		// Build the shop page using page index 0, compact view (detailed=false), no filter.
		var result = ShopManager.Instance.BuildSellInteract(_client, buyer, startingItem, playerSeller, filter, startingAmount);

		var (embed, components) = result.Value;
		await FollowupAsync(embed: embed, components: components, ephemeral: true);
	}

	[SlashCommand("open", "Open the shop interface")]
	public async Task OpenShop(string? filter = null)
	{
		await DeferAsync();

		var shopkeeper = _client.GetUser(ShopManager.SHOP_DISCORD_ID);

		string filterParam = !string.IsNullOrWhiteSpace(filter) ? filter : "";

		// Build the shop page using page index 0, compact view (detailed=false), no filter.
		var result = ShopManager.Instance.BuildShopPage(shopkeeper, 0, _profileService, _playerService, true, filterParam);
		if (result == null)
		{
			await FollowupAsync("The shop is currently empty.");
			return;
		}

		var (embed, components) = result.Value;
		await FollowupAsync(embed: embed, components: components);
	}

}