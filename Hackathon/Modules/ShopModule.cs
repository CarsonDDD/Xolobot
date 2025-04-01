using Discord.Interactions;
using Discord.WebSocket;
using Hackathon.Services;
using Microsoft.Extensions.Logging;
using Hackathon.Managers.Shop;
using Hackathon.DataObjects;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text;
using Discord;

namespace Hackathon.Modules;

[Group("shop", "commands for interacting with xolobot")]
public class ShopModule : ModuleBase
{
	public ShopModule(ILogger<ModuleBase> logger, MongoDBService mongoDbService, OpenAIService openAIService, DiscordSocketClient client, InteractionHandler interaction) : base(logger, mongoDbService, openAIService, client, interaction) { }



	#region GUI INTERACTION
	[SlashCommand("open", "View a compact list of every item in the shop.")]
	public async Task ShopCommand()
	{
		await DeferAsync();// stops error messages when there isnt an error
		OpenShop(Context.Channel);
		await FollowupAsync("hmmmmmmmmmmmm");// stops the indefinate "* * * xolobot is thinking..."
	}

	[SlashCommand("view", "Search items in stock by category (same as search.)")]
	public async Task ViewCommand(
		[Summary("query", "items with names and tags containing")]
		string searchTerm)
	{
		await DeferAsync();// stops error messages when there isnt an error
		OpenItemPage(Context.Channel, searchTerm);
		await FollowupAsync("hmmmmmmmmmmmm");// stops the indefinate "* * * xolobot is thinking..."
	}

	[SlashCommand("search", "View items in stock by category (same as view.)")]
	public async Task SearchCommand(
	[Summary("query", "items with names and tags containing")]
		string searchTerm)
	{
		await DeferAsync();// stops error messages when there isnt an error
		OpenItemPage(Context.Channel, searchTerm);
		await FollowupAsync("hmmmmmmmmmmmm");// stops the indefinate "* * * xolobot is thinking..."
	}
	#endregion

	#region DIRECT INTERACTION
	[SlashCommand("buy", "Purchase item(s) directly")]
	public async Task BuyCommand(
	[Summary("item", "The name of the item to buy")] string itemName,
	[Summary("quantity", "The number of items to purchase")] int quantity)
	{
		await DeferAsync();
		await ProcessBuy(Context.Channel, itemName, quantity, Context.User);
		await FollowupAsync("Purchase processed!");
	}

	[SlashCommand("sell", "Sell item(s) directly")]
	public async Task SellCommand(
		[Summary("item", "The name of the item to sell")] string itemName,
		[Summary("quantity", "The number of items to sell")] int quantity)
	{
		await DeferAsync();
		await ProcessSell(Context.Channel, itemName, quantity, Context.User);
		await FollowupAsync("Sale processed!");
	}
	#endregion

	#region OTHER
	[SlashCommand("haggle", "Negotiate the price of an item using AI")]
	public async Task HaggleCommand(
		[Summary("item", "The name of the item to haggle for")] string itemName)
	{

	}
	#endregion


	#region HELPER FUNCTIONS

	private async void OpenShop(ISocketMessageChannel location)
	{
		// compact
		var items = await _database.GetShopItems();
		await ShopManager.Instance.ShowShopPage(location, 0, items);
	}

	private async void OpenItemPage(ISocketMessageChannel location, String searchTerm)
	{
		// specific
		var items = await _database.GetShopItems(searchTerm);
		if (items.Count == 0)
		{
			await location.SendMessageAsync($"No items found for '{searchTerm}'.");
			return;
		}
		await ShopManager.Instance.ShowItemPage(location, searchTerm, 0, items, Context.User);
	}

	public async Task<string> ProcessBuyItem(MongoDBService database, string itemName, int quantity, SocketUser user)
	{
		int successfulPurchases = 0;
		for (int i = 0; i < quantity; i++)
		{
			int result = await database.BuyItem(user.Id.ToString(), itemName);
			if (result == 1)
			{
				successfulPurchases++;
			}
			else if (result == 0)
			{
				return "Insufficient funds to purchase the item.";
			}
			else
			{
				return "An error occurred during the purchase process.";
			}
		}
		return $"<@{user.Id}> successfully purchased {quantity} of **{itemName}**.";
	}


	private async Task ProcessSell(ISocketMessageChannel location, string itemName, int quantity, SocketUser user)
	{

	}
	private void ProcessHagle() { }

	#endregion
}