using Discord;
using Discord.WebSocket;
using Hackathon.DomainObjects;
using Hackathon.Entities;
using Hackathon.Managers.Inventory;
using Hackathon.Services;
using System;
using System.Collections.Generic;
using System.Linq;
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

	public PlayerProfile? GetShopkeeper(PlayerProfileService playerProfileService)
	{
		return playerProfileService.GetProfile(SHOP_DB_ID);
	}

	public List<ItemStack> GetShopInventory(PlayerProfileService playerProfileService, string? filter = null)
	{
		var inv = GetShopkeeper(playerProfileService).Inventory;
		// Filter goes here

		return inv.Items;
	}

	public async Task<SHOP_RESULT> TryBuyItem(
		string buyerDiscordId,
		string itemName,
		int quantity,
		PlayerService playerService)
	{
		if (quantity <= 0) return SHOP_RESULT.INSUFFICIENT_QUANITY;


		return SHOP_RESULT.SUCCESS;
	}

	public async Task<SHOP_RESULT> TrySellItem(
		string sellerDiscordId,
		string itemName,
		int quantity,
		PlayerService playerService)
	{
		if (quantity <= 0) return SHOP_RESULT.INSUFFICIENT_QUANITY;


		return SHOP_RESULT.SUCCESS;
	}

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

		// Apply multi-term filter if present
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

		if (items.Count == 0)
		{
			var emptyEmbed = new EmbedBuilder()
				.WithTitle("Nothing Available!")
				.WithDescription("There are no items available with the specified search criteria.\nPlease try a different search or check back later.")
				.WithColor(Color.DarkRed)
				.Build();

			return (emptyEmbed, new ComponentBuilder().Build());
		}

		// Regardless of filtering, detailed view is only when detailed=true.
		int itemsPerPage = detailed ? 1 : InventoryManager.ITEMS_PER_PAGE;
		int totalPages = (int)Math.Ceiling(items.Count / (double)itemsPerPage);
		pageIndex = Math.Clamp(pageIndex, 0, totalPages - 1);

		var pagedItems = items.Skip(pageIndex * itemsPerPage).Take(itemsPerPage);

		// For compact view, use a generic title; detailed view is set per item.
		var embed = new EmbedBuilder()
			.WithAuthor(user)
			.WithTitle(detailed ? "" : $"{sellerProfile.Player.Name}'s Shop Inventory")
			.WithFooter($"Page {pageIndex + 1} of {totalPages}")
			.WithColor(Color.DarkGreen);

		ItemStack? displayedItem = null;// If we are on detailed view, we will store the item here
										// If this is false, we are either on list view, OR its an empty inventory
										// But the empty inventory is NEVER the case, as we check for that in our return clauses

		// Note while this while loop may seem EXTREMELY bad. It should be noted, on detail view, the pagedItem.Len is 1
		// So this only loops once in that case
		foreach (var item in pagedItems)
		{
			string tags = item.Item.Tags.Any() ? string.Join(", ", item.Item.Tags) : "None";

			if (detailed)
			{
				// Detailed (big) view shows one item with full info.
				embed.Title = item.Item.DbReference.Name;
				embed.Description = item.Item.DbReference.LongDescription ?? "No description.";
				embed.WithImageUrl(item.Item.DbReference.ImgUrl ?? "");
				embed.AddField("Amount:", item.DbMeta.Amount, true);
				embed.AddField("Cost", $"{item.DbMeta.ActualCost} gp", true);
				embed.AddField("Weight", item.Item.DbReference.Weight.ToString(), true);
				embed.AddField("Tags", tags, false);

				displayedItem = item;
			}
			else
			{
				// Compact view: list items with basic info.
				embed.AddField(item.Item.DbReference.Name,
					$"Cost: {item.DbMeta.ActualCost} | Weight: {item.Item.DbReference.Weight}\nTags: {tags}", true);
			}
		}

		// Create a safe string representation of the filter.
		// (If no filter is provided, this will be an empty string.)
		string filterParam = !string.IsNullOrWhiteSpace(filter) ? filter : "";

		// Incorporate the detailed flag into the custom ID.
		string baseId = !string.IsNullOrWhiteSpace(filter)
			? $"shop_filtered_{filter}_{(detailed ? "detailed" : "compact")}_{user.Id}"
			: $"shop_page_{(detailed ? "detailed" : "compact")}_{user.Id}";

		var builder = new ComponentBuilder();

		// Nav
		builder.WithButton("🡄", customId: $"{baseId}_{pageIndex - 1}"/*, emote: new Emoji("\u2B05")*/, style: ButtonStyle.Secondary, disabled: pageIndex == 0);
		builder.WithButton("🡆", customId: $"{baseId}_{pageIndex + 1}"/*, emote: new Emoji("\u27A1")*/, style: ButtonStyle.Secondary, disabled: pageIndex == totalPages - 1);

		// Buy
		if (detailed)
		{
			// openbuy_{sellerDiscordID}_{itemLedgerID}. --- Buyer if determined ONLY on press interact
			int startingAmount = 0;
			builder.WithButton("Select", customId: $"openbuy_{seller.DiscordId}_{displayedItem.Item.DbReference.Id}_{startingAmount}_{filterParam}", style: ButtonStyle.Success, emote: new Emoji("🏷️"));
		}

		return (embed.Build(), builder.Build());
	}


	public (Embed embed, MessageComponent components)? BuildBuyInteract(
		DiscordSocketClient client,
		Player player,
		ItemStack? item,
		PlayerProfile shopKeeper,
		string[] filter,
		int currentAmountSelected = 0
	)
	{
		// At the end, we must somehow delete the shop message or something. Or have a retry if fail saying either the item no longer exists/already bought or the quanity changed.
		if (shopKeeper == null) throw new ArgumentNullException(nameof(shopKeeper));

		InventoryWithItems shopItems = shopKeeper.Inventory.FilteredInventory(filter);

		if (shopItems.Items.Count == 0)
		{
			var emptyEmbed = new EmbedBuilder()
				.WithTitle("Nothing Available!")
				.WithDescription("There are no items available with the specified search criteria.\nPlease try a different search or check back later.\nfilter: " + string.Join(", ", filter))
				.WithColor(Color.DarkRed)
				.Build();

			return (emptyEmbed, new ComponentBuilder().Build());
		}

		var shopUser = client.GetUser(shopKeeper.Player.DiscordId);

		//default to first is none was selected
		if (item == null)
		{
			item = shopItems.Items[0];
		}

		if (currentAmountSelected > item.DbMeta.Amount) currentAmountSelected = item.DbMeta.Amount;

		int totalCost = item.DbMeta.ActualCost * Math.Max(0, currentAmountSelected);

		var embed = new EmbedBuilder()
			/*with author*/
			.WithTitle("Buy: " + item.Item.DbReference.Name)
			.WithDescription(item.Item.DbReference.LongDescription ?? "No description.")
			.WithThumbnailUrl(item.Item.DbReference.ImgUrl ?? "")
			.WithFooter($"Your Gold Available: {player.Gold} gp")
			.WithColor(Color.Blue);


		embed.AddField("Price-Per-Unit:", item.DbMeta.ActualCost + "gp", true);
		embed.AddField("Total Cost:", totalCost + "gp", true);


		var builder = new ComponentBuilder();

		// Prepare a safe string representation of the filter by joining tokens with hyphen.
		string filterParam = filter.Length > 0 ? string.Join("-", filter) : "";

		// item selector
		var itemSelector = new List<SelectMenuOptionBuilder>();
		int maxItem = Math.Min(25, shopItems.Items.Count);// 25 is max
		for (int i = 0; i < maxItem; i++)
		{
			string itemName = shopItems.Items[i].Item.DbReference.Name;
			int itemId = shopItems.Items[i].Item.DbReference.Id;
			//openbuy_{sellerDiscordId}_{itemId}_0_{filterParam} // 0 as starting amount
			itemSelector.Add(new SelectMenuOptionBuilder(
				label: itemName,
				description: string.Join(", ", shopItems.Items[i].Item.Tags),
				value: $"openbuy_{shopKeeper.Player.DiscordId}_{itemId}_0_{filterParam}"
			));
		}
		builder.WithSelectMenu(
				customId: "buymenu_itemselector",
				options: itemSelector,
				placeholder: item.Item.DbReference.Name
		);


		// generate amount list.
		var quantityOptions = new List<SelectMenuOptionBuilder>();
		int maxQuant = Math.Min(25, item.DbMeta.Amount);// 25 is max
		for (int i = 0; i < maxQuant; i++)
		{
			//openbuy_{sellerDiscordId}_{itemId}_{quantity}_{filterParam}
			quantityOptions.Add(new SelectMenuOptionBuilder(
				label: (i + 1).ToString(),
				value: $"openbuy_{shopKeeper.Player.DiscordId}_{item.Item.DbReference.Id}_{i + 1}_{filterParam}"
			));
		}

		string quantityPlaceholder = currentAmountSelected > 0 ? currentAmountSelected.ToString() : "Select Quantity";

		builder.WithSelectMenu(
				customId: "buymenu_quantselector",
				options: quantityOptions,
				placeholder: quantityPlaceholder
		);

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

		// emojis: select quant
		// buy
		// poor

		bool canTransact = player.Gold > totalCost;

		// transaction_{string:type}_{giverDiscordID}_{takerDiscordID}_{itemID}_{quanity}_{component filters}

		builder.WithButton(buyButtonText, customId: $"transaction_buy_{shopKeeper.Player.DiscordId}_{player.DiscordId}_{item.Item.DbReference.Id}_{currentAmountSelected}_{filterParam}", emote: buyEmoji, style: canTransact ? ButtonStyle.Success : ButtonStyle.Secondary, disabled: !canTransact || (currentAmountSelected <= 0));

		if (canTransact && currentAmountSelected > 0)
		{
			builder.WithButton("Haggle", customId: $"buymenu_haggle", emote: new Emoji("🤌"), style: ButtonStyle.Primary, disabled: !canTransact || (currentAmountSelected <= 0));
		}
		//builder.WithButton("✘ Cancel", customId: $"buymenu_cancel", emote: new Emoji("🙅‍♂️"), style: ButtonStyle.Danger);

		return (embed.Build(), builder.Build());
	}

	public (Embed embed, MessageComponent components)? BuildSellInteract(
		DiscordSocketClient client,
		Player buyer,
		ItemStack? currentItem,
		PlayerProfile seller,
		string[] filter,
		int currentAmountSelected = 0
	)
	{
		// At the end, we must somehow delete the shop message or something. Or have a retry if fail saying either the item no longer exists/already bought or the quanity changed.
		if (seller == null) throw new ArgumentNullException(nameof(seller));

		InventoryWithItems sellInventory = seller.Inventory.FilteredInventory(filter);

		if (sellInventory.Items.Count == 0)
		{
			var emptyEmbed = new EmbedBuilder()
				.WithTitle("Nothing Found!")
				.WithDescription("You have no items with the specified search criteria.\nPlease try a different search or check back later.")
				.WithColor(Color.DarkRed)
				.Build();

			return (emptyEmbed, new ComponentBuilder().Build());
		}

		/*var shopUser = client.GetUser(seller.Player.DiscordId);
		if (shopUser == null)
		{
			throw new Exception("SHOP OWNER NULL: " + seller.Player.DiscordId);
		}*/

		//default to first is none was selected
		if (currentItem == null)
		{
			currentItem = sellInventory.Items[0];
		}

		if (currentAmountSelected > currentItem.DbMeta.Amount) currentAmountSelected = currentItem.DbMeta.Amount;

		int totalCost = currentItem.DbMeta.ActualCost * Math.Max(0, currentAmountSelected);

		var embed = new EmbedBuilder()
			.WithAuthor(client.GetUser(ulong.Parse(seller.Player.DiscordId)))
			.WithTitle("Sell: " + currentItem.Item.DbReference.Name)
			.WithDescription(currentItem.Item.DbReference.LongDescription ?? "No description.")
			.WithThumbnailUrl(currentItem.Item.DbReference.ImgUrl ?? "")
			.WithFooter($"{buyer.Name}'s Total Available Gold: {buyer.Gold} gp")
			.WithColor(Color.Blue);


		embed.AddField("Price-Per-Unit:", currentItem.DbMeta.ActualCost + "gp", true);
		embed.AddField("Total Gain:", totalCost + "gp", true);


		var builder = new ComponentBuilder();

		// Prepare a safe string representation of the filter by joining tokens with hyphen.
		string filterParam = filter.Length > 0 ? string.Join("-", filter) : "";

		// item selector
		var itemSelector = new List<SelectMenuOptionBuilder>();
		int maxItem = Math.Min(25, sellInventory.Items.Count);// 25 is max
		for (int i = 0; i < maxItem; i++)
		{
			string itemName = sellInventory.Items[i].Item.DbReference.Name;
			int itemId = sellInventory.Items[i].Item.DbReference.Id;
			//opensell_{buyerDBId}_{itemId}_0_{filterParam} // 0 as starting amount
			itemSelector.Add(new SelectMenuOptionBuilder(
				label: itemName,
				description: string.Join(", ", sellInventory.Items[i].Item.Tags),
				value: $"opensell_{seller.Player.DiscordId}_{itemId}_0_{filterParam}"
			));
		}
		builder.WithSelectMenu(
				customId: "sellmenu_itemselector",
				options: itemSelector,
				placeholder: currentItem.Item.DbReference.Name
		);


		// generate amount list.
		var quantityOptions = new List<SelectMenuOptionBuilder>();
		int maxQuant = Math.Min(25, currentItem.DbMeta.Amount);// 25 is max
		for (int i = 0; i < maxQuant; i++)
		{
			//opensell_{buyerDBId}_{itemId}_{quantity}_{filterParam}
			quantityOptions.Add(new SelectMenuOptionBuilder(
				label: (i + 1).ToString(),
				value: $"opensell_{seller.Player.DiscordId}_{currentItem.Item.DbReference.Id}_{i + 1}_{filterParam}"
			));
		}

		string quantityPlaceholder = currentAmountSelected > 0 ? currentAmountSelected.ToString() : "Select Quantity";

		builder.WithSelectMenu(
				customId: "sellmenu_quantselector",
				options: quantityOptions,
				placeholder: quantityPlaceholder
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

		// emojis: select quant
		// sell
		// poor
		bool canTransact = buyer.Gold > totalCost;
		// transaction_{string:type}_{giverDiscordID}_{takerDiscordID}_{itemID}_{quanity}_{component filters}
		builder.WithButton(sellButtonText, customId: $"transaction_sell_{seller.Player.DiscordId}_{ShopManager.SHOP_DISCORD_ID}_{currentItem.Item.DbReference.Id}_{currentAmountSelected}_{filterParam}", emote: sellEmoji, style: canTransact ? ButtonStyle.Success : ButtonStyle.Secondary, disabled: !canTransact || (currentAmountSelected <= 0));

		if (currentAmountSelected > 0 && canTransact)
		{
			builder.WithButton("Haggle", customId: $"sellmenu_haggle", emote: new Emoji("🤌"), style: ButtonStyle.Primary, disabled: !canTransact || (currentAmountSelected <= 0));
		}
		//builder.WithButton("✘ Cancel", customId: $"sellmenu_cancel", emote: new Emoji("🙅‍♂️"), style: ButtonStyle.Danger);

		return (embed.Build(), builder.Build());
	}

	public static async Task<ShopService.ShopResult> ExecuteTransaction(ShopService shopService, string type, string giverDiscordId, string takerDiscordId, int itemId, int quantity)
	{

		//await Task.CompletedTask;
		//return ShopService.ShopResult.Success;
		return await shopService.ExecuteTransaction(type, giverDiscordId, takerDiscordId, itemId, quantity);
	}

}