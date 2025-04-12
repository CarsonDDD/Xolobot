using Discord;
using Discord.WebSocket;
using Hackathon.DomainObjects;
using Hackathon.Entities;
using Hackathon.Managers.Inventory;
using Hackathon.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Hackathon.Managers.Shop;

public class ShopManager
{
	public enum SHOP_RESULT
	{
		SUCCESS,
		INSUFFICIENT_QUANITY,
		INSUFFICIENT_FUNDS,
		UNAVAILABLE,
	}

	private static ShopManager _instance;
	private ShopManager() { }
	public static ShopManager Instance => _instance ??= new ShopManager();

	private const int SHOP_DB_ID = 2; // Fake player ID---xolobots id
	public static ulong SHOP_DISCORD_ID = 1190800169411809360; // this is ugly

	private const int ITEMS_PER_SHOP_PAGE = 3;
	private const string SHOP_NAME = "**Magic store**";
	private const string BUY_HELP_TEXT = "To view an item to purchase, use /shop view <item>";

	// Near identical to the inventory page. However, in the future we will change it....maybe
	public (Embed embed, MessageComponent components)? BuildShopPage(
		IUser user,
		int pageIndex,
		PlayerProfileService profileService,
		PlayerService playerService,
		bool detailed,
		string? filter = null)
	{
		var seller = playerService.GetByDiscordId(user.Id.ToString());
		if (seller == null) return null;

		var sellerProfile = profileService.GetProfile(seller.Id);
		if (sellerProfile?.Inventory == null || sellerProfile.Inventory.Items.Count == 0) return null;

		var items = sellerProfile.Inventory.Items;

		// Decode filter
		if (!string.IsNullOrWhiteSpace(filter))
		{
			filter = filter.Replace("_", "");// sanitize

			// You can instead convert your filter string into tokens...
			var terms = filter
				.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(t => t.Trim())
				.ToArray();
			items = sellerProfile.Inventory.FilterList(terms);
		}

		if (items.Count == 0) return (ItemManager.Instance.CreateItemNotFound().Build(), new ComponentBuilder().Build());

		// Regardless of filtering, detailed view is only when detailed=true.
		int itemsPerPage = detailed ? 1 : InventoryManager.ITEMS_PER_PAGE;
		int totalPages = (int)Math.Ceiling(items.Count / (double)itemsPerPage);
		pageIndex = Math.Clamp(pageIndex, 0, totalPages - 1);

		var pagedItems = items.Skip(pageIndex * itemsPerPage).Take(itemsPerPage);
		ItemStack? displayedItem = pagedItems.First();

		string footer = string.IsNullOrWhiteSpace(filter) ?
		$"Page {pageIndex + 1} of {totalPages}" :
		$"Page {pageIndex + 1} of {totalPages} — for '{filter}'";

		string authorName = ((user as IGuildUser)?.Nickname ?? user.Username) + "'s Shop";
		var embed = ItemManager.Instance.CreateDetailedDisplay(user, displayedItem, authorName, footer);

		// shop_{isDetailed}_{DiscordID}_{destinationPage}_{filter}
		string baseId = $"shop_{detailed}_{user.Id}";

		var builder = new ComponentBuilder();

		// Nav
		builder.WithButton("🡄", customId: $"{baseId}_{pageIndex - 1}_{filter}"/*, emote: new Emoji("\u2B05")*/, style: ButtonStyle.Secondary, disabled: pageIndex == 0);
		builder.WithButton("🡆", customId: $"{baseId}_{pageIndex + 1}_{filter}"/*, emote: new Emoji("\u27A1")*/, style: ButtonStyle.Secondary, disabled: pageIndex == totalPages - 1);

		// Buy
		if (detailed)
		{
			// openbuy_{non-componentInteractorDisocrdID}_{itemLedgerID}. --- Buyer if determined ONLY on press interact
			int startingAmount = 0;
			builder.WithButton("Select", customId: $"openbuy_{seller.DiscordId}_{displayedItem.Item.DbReference.Id}_{startingAmount}_{filter}", style: ButtonStyle.Success, emote: new Emoji("🏷️"));
		}

		return (embed.Build(), builder.Build());
	}


	public (Embed embed, MessageComponent components)? BuildBuyInteract(
		DiscordSocketClient client,
		Player player, // Player calling this function
		ItemStack? item,
		PlayerProfile shopKeeper, // The shopkeeper
		string[] filter,
		int currentAmountSelected = 0
	)
	{
		// At the end, we must somehow delete the shop message or something. Or have a retry if fail saying either the item no longer exists/already bought or the quanity changed.
		if (shopKeeper == null) throw new ArgumentNullException(nameof(shopKeeper));

		InventoryWithItems shopItems = shopKeeper.Inventory.FilteredInventory(filter);

		if (shopItems.Items.Count == 0)
		{
			return (ItemManager.Instance.CreateItemNotFound()
				.WithDescription("There are no items with the specified search criteria in the shop.\nPlease try a different search or check back later.").Build(),
				new ComponentBuilder().Build()
			);
		}

		//default to first is none was selected
		if (item == null) item = shopItems.Items[0];
		if (currentAmountSelected > item.DbMeta.Amount) currentAmountSelected = item.DbMeta.Amount;

		int totalCost = item.DbMeta.ActualCost * Math.Max(0, currentAmountSelected);


		var shopOwner = client.GetUser(ulong.Parse(shopKeeper.Player.DiscordId));
		string authorName = ((shopOwner as IGuildUser)?.Nickname ?? shopOwner.Username) + "'s Shop";
		var embed = ItemManager.Instance.CreateSmallDisplay(shopOwner, authorName, item, currentAmountSelected);
		embed.WithColor(Color.Purple);
		embed.WithFooter($"Your Gold Available: {player.Gold} gp");
		embed.Fields[1].Name = embed.Fields[1].Name.Replace("{total}", "Total Cost");


		// Prepare a safe string representation of the filter by joining tokens with hyphen.
		string filterParam = filter.Length > 0 ? string.Join("-", filter) : "";

		ComponentBuilder builder = CreateItemSelector(shopItems, item,
		"buymenu_itemselector", $"openbuy_{shopKeeper.Player.DiscordId}_{{i}}_1_{filterParam}",
		"buymenu_quantselector", $"openbuy_{shopKeeper.Player.DiscordId}_{item.Item.DbReference.Id}_{{i}}_{filterParam}",
		currentAmountSelected
		);

		// Interact Buttons
		string buyButtonText = null;
		Emoji buyEmoji = null;
		if (currentAmountSelected <= 0)
		{
			buyButtonText = "Select a quantity";
			buyEmoji = new Emoji("📄");
		}
		else if (currentAmountSelected > 0)
		{
			if (player.Gold < totalCost)
			{
				buyButtonText = "You cannot afford this";
				buyEmoji = new Emoji("😬");
			}
			else
			{
				if (currentAmountSelected == 1) buyButtonText = $"Buy {currentAmountSelected} {item.Item.DbReference.Name}";
				else buyButtonText = $"Buy {currentAmountSelected} {item.Item.DbReference.Name}'s";

				buyEmoji = new Emoji("🎁");
			}
		}


		bool canTransact = player.Gold > totalCost;
		// transaction_{string:type}_{giverDiscordID}_{takerDiscordID}_{itemID}_{quanity}_{component filters}
		builder.WithButton(buyButtonText, customId: $"transaction_buy_{shopKeeper.Player.DiscordId}_{player.DiscordId}_{item.Item.DbReference.Id}_{currentAmountSelected}_{filterParam}", emote: buyEmoji, style: canTransact ? ButtonStyle.Success : ButtonStyle.Secondary, disabled: !canTransact || (currentAmountSelected <= 0));

		if (canTransact && currentAmountSelected > 0)
		{
			builder.WithButton("Haggle", customId: $"buymenu_haggle", emote: new Emoji("🤌"), style: ButtonStyle.Primary, disabled: !canTransact || (currentAmountSelected <= 0));
		}

		return (embed.Build(), builder.Build());
	}

	public (Embed embed, MessageComponent components)? BuildSellInteract(
		DiscordSocketClient client,
		Player buyer, // Shop keeper
		ItemStack currentItem,
		PlayerProfile seller, //player calling this function
		string[] filter,
		int currentAmountSelected = 0
	)
	{
		// At the end, we must somehow delete the shop message or something. Or have a retry if fail saying either the item no longer exists/already bought or the quanity changed.
		if (seller == null) throw new ArgumentNullException(nameof(seller));

		InventoryWithItems sellInventory = seller.Inventory.FilteredInventory(filter);


		if (sellInventory.Items.Count == 0)
		{
			return (ItemManager.Instance.CreateItemNotFound()
				.WithDescription("You have no items with the specified search criteria.\nPlease try a different search or check back later.").Build(),
				new ComponentBuilder().Build()
			);
		}

		//default to first is none was selected
		if (currentItem == null) currentItem = sellInventory.Items[0];
		if (currentAmountSelected > currentItem.DbMeta.Amount) currentAmountSelected = currentItem.DbMeta.Amount;

		int totalCost = currentItem.DbMeta.ActualCost * Math.Max(0, currentAmountSelected);

		var shopOwner = client.GetUser(ulong.Parse(seller.Player.DiscordId));
		string authorName = ((shopOwner as IGuildUser)?.Nickname ?? shopOwner.Username) + "'s Inventory";
		var embed = ItemManager.Instance.CreateSmallDisplay(shopOwner, authorName, currentItem, currentAmountSelected);
		embed.Fields[1].Name = embed.Fields[1].Name.Replace("{total}", "Total Gain");
		embed.WithColor(Color.Blue);
		embed.WithFooter($"{buyer.Name}'s Total Available Gold: {buyer.Gold} gp");

		//var builder = new ComponentBuilder();

		// Prepare a safe string representation of the filter by joining tokens with hyphen.
		string filterParam = filter.Length > 0 ? string.Join("-", filter) : "";

		ComponentBuilder builder = CreateItemSelector(sellInventory, currentItem,
		"sellmenu_itemselector", $"opensell_{buyer.DiscordId}_{{i}}_1_{filterParam}",
		"sellmenu_quantselector", $"opensell_{buyer.DiscordId}_{currentItem.Item.DbReference.Id}_{{i}}_{filterParam}",
		currentAmountSelected
		);

		string sellButtonText = null;
		Emoji sellEmoji = null;
		if (currentAmountSelected <= 0)
		{
			sellButtonText = "Select a quantity";
			sellEmoji = new Emoji("📄");
		}
		else if (currentAmountSelected > 0)
		{
			if (buyer.Gold < totalCost)
			{
				sellButtonText = "I cannot afford this";
				sellEmoji = new Emoji("😬");
			}
			else
			{
				if (currentAmountSelected == 1) sellButtonText = $"Sell {currentAmountSelected} {currentItem.Item.DbReference.Name}";
				else sellButtonText = $"Sell {currentAmountSelected} {currentItem.Item.DbReference.Name}'s";

				sellEmoji = new Emoji("📦");
			}
		}


		bool canTransact = buyer.Gold > totalCost;
		// transaction_{string:type}_{giverDiscordID}_{takerDiscordID}_{itemID}_{quanity}_{component filters}
		builder.WithButton(sellButtonText, customId: $"transaction_sell_{seller.Player.DiscordId}_{ShopManager.SHOP_DISCORD_ID}_{currentItem.Item.DbReference.Id}_{currentAmountSelected}_{filterParam}", emote: sellEmoji, style: canTransact ? ButtonStyle.Success : ButtonStyle.Secondary, disabled: !canTransact || (currentAmountSelected <= 0));

		if (currentAmountSelected > 0 && canTransact)
		{
			builder.WithButton("Haggle", customId: $"sellmenu_haggle", emote: new Emoji("🤌"), style: ButtonStyle.Primary, disabled: !canTransact || (currentAmountSelected <= 0));
		}

		return (embed.Build(), builder.Build());
	}

	public ComponentBuilder CreateItemSelector(InventoryWithItems inventory, ItemStack currentItem,
		string itemSelectorMenuCustomId, string itemSelectorCustomId,
		string quanitySelectorMenuCustomId, string quanitySelectorCustomId, int currentQuantity)
	{
		ComponentBuilder menus = new ComponentBuilder();

		// item selector
		var itemSelector = new List<SelectMenuOptionBuilder>();
		int maxItem = Math.Min(25, inventory.Items.Count);// 25 is max
		for (int i = 0; i < maxItem; i++)
		{
			string itemName = inventory.Items[i].Item.DbReference.Name;
			int itemId = inventory.Items[i].Item.DbReference.Id;
			//opensell_{non-componentInteractorDisocrdID}_{itemId}_0_{filterParam} // 0 as starting amount
			itemSelector.Add(new SelectMenuOptionBuilder(
				label: itemName,
				description: string.Join(", ", inventory.Items[i].Item.Tags),
				value: itemSelectorCustomId.Replace("{i}", itemId.ToString())
			));
		}
		menus.WithSelectMenu(
				customId: itemSelectorMenuCustomId,
				options: itemSelector,
				placeholder: currentItem.Item.DbReference.Name
		);

		// generate amount list.
		var quantityOptions = new List<SelectMenuOptionBuilder>();
		int maxQuant = Math.Min(25, currentItem.DbMeta.Amount);// 25 is max
		for (int i = 0; i < maxQuant; i++)
		{
			//opensell_{non-componentInteractorDisocrdID}_{itemId}_{quantity}_{filterParam}
			quantityOptions.Add(new SelectMenuOptionBuilder(
				label: (i + 1).ToString(),
				value: quanitySelectorCustomId.Replace("{i}", (i + 1).ToString())
			));
		}
		menus.WithSelectMenu(
				customId: quanitySelectorMenuCustomId,
				options: quantityOptions,
				placeholder: currentQuantity > 0 ? currentQuantity.ToString() : "Select Quantity"
		);

		return menus;
	}

	public static async Task<ShopService.ShopResult> ExecuteTransaction(ShopService shopService, string type, string giverDiscordId, string takerDiscordId, int itemId, int quantity)
	{
		return await shopService.ExecuteTransaction(type, giverDiscordId, takerDiscordId, itemId, quantity);
	}
}