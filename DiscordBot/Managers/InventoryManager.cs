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
        if (displayedItem == null)
            return null;

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
        string discordName = ((user as IGuildUser)?.Nickname ?? user.Username) + "";
        EmbedBuilder embedBuilder = new EmbedBuilder()
            .WithTitle($"{profile.Player.Name}'s Inventory")
            .WithAuthor(author =>
            {
                author.IconUrl = user.GetAvatarUrl();
                author.Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
                author.Name = discordName;
            })
            /* .WithDescription("Here are the items:")*/
            .WithColor(Color.DarkBlue);

        if (!string.IsNullOrWhiteSpace(filter))
            embedBuilder.WithFooter("Showing results for: '" + filter + "'");

        // Display items. Max field len in 1024 chars, so we will take a good guess to split them
        int itemsPerField = 10;
        int totalItems = items.Count;
        int fieldCount = (int)Math.Ceiling(totalItems / (double)itemsPerField);

        for (int i = 0; i < fieldCount; i++)
        {
            var chunk = items.Skip(i * itemsPerField).Take(itemsPerField);

            var fieldText = new StringBuilder();
            foreach (var item in chunk)
            {
                fieldText.AppendLine($"- **{item.Item.DbReference.Name}** x{item.DbMeta.Amount}");
            }

            embedBuilder.AddField(
                name: $"༺ Items {(fieldCount > 1 ? $"(Page {i + 1})" : "")} ༻",
                value: fieldText.ToString(),
                true
            );
        }

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
