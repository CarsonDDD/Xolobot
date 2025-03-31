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
	[SlashCommand("open", "Opens Shop")]
	public async Task ShopCommand()
	{
		await DeferAsync();// stops error messages when there isnt an error
		OpenShop(Context.Channel);
		await FollowupAsync("hmmmmmmmmmmmm");// stops the indefinate "* * * xolobot is thinking..."
	}

	[SlashCommand("view", "same as search")]
	public async Task ViewCommand(
		[Summary("query", "items with names and tags containing")]
		string searchTerm)
	{
		await DeferAsync();// stops error messages when there isnt an error
		OpenItemPage(Context.Channel, searchTerm);
		await FollowupAsync("hmmmmmmmmmmmm");// stops the indefinate "* * * xolobot is thinking..."
	}

	[SlashCommand("search", "search specifc items")]
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

	}

	[SlashCommand("sell", "Sell item(s) directly")]
	public async Task SellCommand(
		[Summary("item", "The name of the item to sell")] string itemName,
		[Summary("quantity", "The number of items to sell")] int quantity)
	{

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
		await ShopManager.Instance.ShowItemPage(location, searchTerm, 0, items, Context.User);
	}

	private void ProcessBuy() { }
	private void ProcessSell() { }
	private void ProcessHagle() { }

	#endregion
}