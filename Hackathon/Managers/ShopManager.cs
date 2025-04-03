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

	public List<ItemWithTags> GetShopInventory(PlayerProfileService playerProfileService, string? filter = null)
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
		var player = playerService.GetByDiscordId(user.Id.ToString());
		if (player == null) return null;

		var profile = profileService.GetProfile(player.Id);
		if (profile?.Inventory == null || profile.Inventory.Items.Count == 0) return null;

		var items = profile.Inventory.Items;

		// Apply multi-term filter if present
		if (!string.IsNullOrWhiteSpace(filter))
		{
			if (filter.Contains("_")) return null; // Prevent invalid identifiers

			var terms = filter.ToLowerInvariant()
							  .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

			items = items.Where(i => terms.All(term =>
				i.Item.Name.ToLower().Contains(term) ||
				i.Tags.Any(t => t.Label.ToLower().Contains(term))
			)).ToList();
		}

		if (items.Count == 0) return null;

		// Regardless of filtering, detailed view is only when detailed=true.
		int itemsPerPage = detailed ? 1 : InventoryManager.ITEMS_PER_PAGE;
		int totalPages = (int)Math.Ceiling(items.Count / (double)itemsPerPage);
		pageIndex = Math.Clamp(pageIndex, 0, totalPages - 1);

		var pagedItems = items.Skip(pageIndex * itemsPerPage).Take(itemsPerPage);

		// For compact view, use a generic title; detailed view is set per item.
		var embed = new EmbedBuilder()
			.WithAuthor(user)
			.WithTitle(detailed ? "" : $"{profile.Player.Name}'s Shop Inventory")
			.WithFooter($"Page {pageIndex + 1} of {totalPages}")
			.WithColor(Color.DarkGreen);

		foreach (var item in pagedItems)
		{
			string tags = item.Tags.Any() ? string.Join(", ", item.Tags.Select(t => t.Label)) : "None";

			if (detailed)
			{
				// Detailed (big) view shows one item with full info.
				embed.Title = item.Item.Name;
				embed.Description = item.Item.LongDescription ?? "No description.";
				embed.WithImageUrl(item.Item.ImgUrl ?? "");
				embed.AddField("Cost", $"{item.Item.BaseCost} gp", true);
				embed.AddField("Weight", item.Item.Weight.ToString(), true);
				embed.AddField("Tags", tags, false);
			}
			else
			{
				// Compact view: list items with basic info.
				embed.AddField(item.Item.Name,
					$"Cost: {item.Item.BaseCost} | Weight: {item.Item.Weight}\nTags: {tags}", false);
			}
		}

		// Incorporate the detailed flag into the custom ID.
		string baseId = !string.IsNullOrWhiteSpace(filter)
			? $"shop_filtered_{filter}_{(detailed ? "detailed" : "compact")}_{user.Id}"
			: $"shop_page_{(detailed ? "detailed" : "compact")}_{user.Id}";

		var builder = new ComponentBuilder();


		builder.WithButton("", customId: $"{baseId}_{pageIndex - 1}", emote: new Emoji("\u2B05"), disabled: pageIndex == 0);
		builder.WithButton("", customId: $"{baseId}_{pageIndex + 1}", emote: new Emoji("\u27A1"), disabled: pageIndex == totalPages - 1);

		if (detailed)
			builder.WithButton("", customId: $"{baseId}_buy", emote: new Emoji("\uD83D\uDC4C"));

		return (embed.Build(), builder.Build());
	}

}