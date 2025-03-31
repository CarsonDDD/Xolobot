using Discord.WebSocket;
using Discord;
using Hackathon.DataObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hackathon.Services;
using System.Net.Mime;

// Singleton
namespace Hackathon.Managers.Shop;
public class ShopManager
{
	private static ShopManager _instance;
	private ShopManager() { }
	public static ShopManager Instance
	{
		get
		{
			if (_instance == null)
			{
				_instance = new ShopManager();
			}
			return _instance;
		}
	}

	private const int ITEMS_PER_SHOP_PAGE = 3;
	private const string SHOP_NAME = "**Magic store**";

	private const string BUY_HELP_TEXT = "To view an item to purchase, use /shop view <item>";

	// Similear to show shop, however just for search term items and shows more data `shop_page_<searchterm>_#`
	// ONLY SHOWS 1 Item per page
	public async Task ShowItemPage(ISocketMessageChannel location, String searchTerm, int pageIndex, List<Item> items, IUser userInteractor, IUserMessage existingMessage = null)
	{
		if (searchTerm.Contains("_"))
		{
			await location.SendMessageAsync("Cannot have '_' in search term!");
			return;
		}

		int totalPages = items.Count;
		Item item = items[pageIndex];

		EmbedBuilder window = new EmbedBuilder()
			.WithTitle($"**{item.name}**\n**{item.cost}** ***gp***")
			.WithDescription($"> *{item.TagsToString()}*")
			.WithFooter(footer => footer.Text = $"{items.Count} results for: '{searchTerm}'\nPage {pageIndex + 1} of {totalPages}")
			.WithImageUrl(item.imgUrl ?? "");// This makes it only possible for 1 item at a time

		window.AddField($"**Description**:", $"{item.longdescription}");

		// nav buttons
		// Page switching logic is inside InteractionHandler......yes I know.
		var component = new ComponentBuilder()
			.WithButton("***BUY NOW!***", customId: $"shop_buy_{item.name}_{userInteractor.Id.ToString()}");

		if (items.Count > 1)
		{
			component
				.WithButton(emote: new Emoji("\u2B05"), customId: $"item_page_{searchTerm}_{pageIndex - 1}", disabled: pageIndex == 0)
			.WithButton(emote: new Emoji("\u27A1"), customId: $"item_page_{searchTerm}_{pageIndex + 1}", disabled: pageIndex == totalPages - 1);
		}


		// Edit existing shop menu, so it doesnt spam.
		if (existingMessage != null)
		{
			await existingMessage.ModifyAsync(msg =>
			{
				msg.Embed = window.Build();
				msg.Components = component.Build();
			});
		}
		else
		{
			await location.SendMessageAsync(embed: window.Build(), components: component.Build());
		}
	}

	// a menu which has pages and shows item names
	public async Task ShowShopPage(ISocketMessageChannel location, int pageIndex, List<Item> items, IUserMessage existingMessage = null)
	{

		int totalPages = (int)Math.Ceiling(items.Count / (double)ITEMS_PER_SHOP_PAGE);

		EmbedBuilder window = new EmbedBuilder()
			.WithTitle(SHOP_NAME)
			.WithFooter(footer => footer.Text = $"Page {pageIndex + 1} of {totalPages}");

		// Add items to the window
		for (int i = pageIndex * ITEMS_PER_SHOP_PAGE; i < Math.Min((pageIndex + 1) * ITEMS_PER_SHOP_PAGE, items.Count); i++)
		{
			window.AddField($"\n**{items[i].name}**: ***{items[i].cost}***gp", $"> *{items[i].TagsToString()}*\n\n{items[i].shortdescription} \n\n──────────\n\n");
		}

		// nav buttons
		// Page switching logic is inside InteractionHandler......yes I know.
		// custom id is used to determine the logic for what is interacted with. We are using shop_page_#, for a button and once pressed will show that shop page
		MessageComponent component = new ComponentBuilder()
			.WithButton(emote: new Emoji("\u2B05"), customId: $"shop_page_{pageIndex - 1}", disabled: pageIndex == 0)
			.WithButton(emote: new Emoji("\u27A1"), customId: $"shop_page_{pageIndex + 1}", disabled: pageIndex == totalPages - 1)
			.Build();


		// Edit existing shop menu, so it doesnt spam.
		if (existingMessage != null)
		{
			await existingMessage.ModifyAsync(msg =>
			{
				msg.Content = BUY_HELP_TEXT;
				msg.Embed = window.Build();
				msg.Components = component;
			});
		}
		else
		{
			await location.SendMessageAsync(text: BUY_HELP_TEXT, embed: window.Build(), components: component);
		}
	}

	public async void BuyItem(SocketMessageComponent caller, string itemName, MongoDBService databaseReference)
	{
		int result = await databaseReference.BuyItem(caller.User.Id.ToString(), itemName);

		if (result == -1)
		{
			await caller.RespondAsync("item or player is null in database", ephemeral: true);
		}
		else if (result == 0)// This should never get called
		{
			await caller.RespondAsync("You are too poor ;(", ephemeral: true);
		}
		else if (result == 1)
		{
			await caller.RespondAsync($"<@{caller.User.Id}>, is now the owner of: {itemName}");
		}
	}
}
