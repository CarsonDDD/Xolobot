using System.Text;
using Discord;
using Discord.WebSocket;
using Hackathon.DomainObjects;
using Hackathon.Entities;
using Hackathon.Managers.Shop;
using Hackathon.Services;
using Hackathon.Utility;

namespace Hackathon.Managers.Inventory;

public class InventoryManager
{
    private static InventoryManager _instance;

    private InventoryManager() { }

    public static InventoryManager Instance => _instance ??= new InventoryManager();

    public static readonly int ITEMS_PER_PAGE = 5;

    public (Embed embed, MessageComponent components)? BuildInventoryPage(
        IUser user,
        int pageIndex,
        PlayerProfileService profileService,
        PlayerService playerService,
        bool detailed,
        string? filter = null
    )
    {
        var player = playerService.GetByDiscordId(user.Id.ToString());
        if (player == null)
            return null;

        var profile = profileService.GetProfile(player.Id);
        if (profile?.Inventory == null || profile.Inventory.Items.Count == 0)
            return null;

        // Get list of items depending on filter.
        List<ItemStack> items = !string.IsNullOrWhiteSpace(filter)
            ? profile.Inventory.FilterList(Utils.DecodeFilter(filter))
            : profile.Inventory.Items;
        if (items.Count == 0)
            return (
                ItemManager.Instance.CreateItemNotFound().Build(),
                new ComponentBuilder().Build()
            );

        // Use the detailed parameter solely to control view style:
        // If detailed is true, show one item per page regardless of filtering.
        int itemsPerPage = detailed ? 1 : ITEMS_PER_PAGE;
        var pagedItems = items.Skip(pageIndex * itemsPerPage).Take(itemsPerPage);
        ItemStack? displayedItem = pagedItems.First();
        string authorName = ((user as IGuildUser)?.Nickname ?? user.Username) + "'s Inventory";
        string baseId = $"inventory_{detailed}_{user.Id}";

        ButtonBuilder selectButton = new ButtonBuilder(
            $"Show {playerService.GetByDiscordId(ShopManager.SHOP_DISCORD_ID.ToString()).Name}",
            customId: $"opensell_{ShopManager.SHOP_DISCORD_ID}_{displayedItem.Item.DbReference.Id}_1_{filter}",
            style: ButtonStyle.Success,
            emote: new Emoji("🏚️")
        );
        var builders = ItemManager.Instance.BuildItemDisplayPage(
            user,
            items,
            displayedItem,
            pageIndex,
            itemsPerPage,
            detailed,
            authorName,
            filter,
            baseId,
            selectButton
        );
        builders.embed.WithColor(Color.Blue);

        // Delete Button.
        // ALL THIS DOES IS OPEN THE CONFIRMATION MENU
        builders.components.WithButton(
            label: " ",
            customId: $"opendelete_{displayedItem.DbMeta.Id}_0",
            style: ButtonStyle.Danger,
            emote: new Emoji("🗑️")
        );

        return (builders.embed.Build(), builders.components.Build());
    }

    public (Embed embed, MessageComponent components)? BuildInventoryList(
        IUser user,
        string filter,
        PlayerProfileService profileService,
        PlayerService playerService
    )
    {
        var player = playerService.GetByDiscordId(user.Id.ToString());
        if (player == null)
            return null;

        var profile = profileService.GetProfile(player.Id);
        if (profile?.Inventory == null || profile.Inventory.Items.Count == 0)
            return null;

        // Get list of items depending on filter.
        List<ItemStack> items = !string.IsNullOrWhiteSpace(filter)
            ? profile.Inventory.FilterList(Utils.DecodeFilter(filter))
            : profile.Inventory.Items;
        if (items.Count == 0)
        {
            return (
                ItemManager.Instance.CreateItemNotFound().Build(),
                new ComponentBuilder().Build()
            );
        }

        EmbedBuilder embedBuilder = new EmbedBuilder()
            .WithTitle($"{user.Username}'s Inventory")
            .WithDescription("Here are the items:")
            .WithColor(Color.DarkBlue);

        if (!string.IsNullOrWhiteSpace(filter))
            embedBuilder.WithFooter("Showing results for: '" + filter + "'");

        StringBuilder itemListBuilder = new StringBuilder();

        foreach (var item in items)
        {
            itemListBuilder.AppendLine($"- **{item.Item.DbReference.Name}** x{item.DbMeta.Amount}");
        }

        embedBuilder.AddField("Items:", itemListBuilder.ToString());

        MessageComponent components = new ComponentBuilder().Build();

        return (embedBuilder.Build(), components);
    }

    public (Embed embed, MessageComponent component)? BuildDeleteMenu(
        InventoryItem item,
        int currentAmountSelected = 0
    )
    {
        EmbedBuilder embedBuilder = new EmbedBuilder()
            .WithTitle($"Are you sure you want to delete this?")
            .WithDescription("Here are the items:")
            .WithColor(Color.Red);

        SelectMenuBuilder smb = ItemManager.Instance.CreateQuantitySelector(
            "deletemenu_quantselector",
            $"opendelete_{item.Id}_{{i}}",
            item.Amount,
            currentAmountSelected
        );

        ComponentBuilder menus = new ComponentBuilder()
            .WithSelectMenu(smb)
            .WithButton(
                label: currentAmountSelected > 0
                    ? $"DELETE **{currentAmountSelected}** ITEMS NOW!"
                    : "Select Quantity",
                customId: $"delete_{item.Id}_{item.Amount}_{currentAmountSelected}",
                /*emote: new Emoji("❌"),*/
                style: ButtonStyle.Danger,
                disabled: currentAmountSelected <= 0
            );

        return (embedBuilder.Build(), menus.Build());
    }
}
