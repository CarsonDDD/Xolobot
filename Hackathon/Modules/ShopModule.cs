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



	#region HELPER FUNCTIONS

	/*private async void OpenShop(ISocketMessageChannel location)
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
	}*/

	/*public async Task<string> ProcessBuyItem(MongoDBService database, string itemName, int quantity, SocketUser user)
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
	}*/


	private async Task ProcessSell(ISocketMessageChannel location, string itemName, int quantity, SocketUser user)
	{

	}
	private void ProcessHagle() { }

	#endregion
}