using Discord;
using Discord.WebSocket;
using Hackathon.DomainObjects;
using Hackathon.Managers.Shop;
using Hackathon.Services;

namespace Hackathon.Managers.Inventory;

public class InventoryManager
{
    private static InventoryManager _instance;
    private InventoryManager() { }
    public static InventoryManager Instance => _instance ??= new InventoryManager();

    public readonly static int ITEMS_PER_PAGE = 5;

    public (Embed embed, MessageComponent components)? BuildInventoryPage(
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
            filter = filter.Replace("_", "");// sanitize

            // You can instead convert your filter string into tokens...
            var terms = filter
                .Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .ToArray();
            items = profile.Inventory.FilterList(terms);
        }

        if (items.Count == 0) return (ItemManager.Instance.CreateItemNotFound().Build(), new ComponentBuilder().Build());

        // Use the detailed parameter solely to control view style:
        // If detailed is true, show one item per page regardless of filtering.
        int itemsPerPage = detailed ? 1 : ITEMS_PER_PAGE;
        int totalPages = (int)Math.Ceiling(items.Count / (double)itemsPerPage);
        pageIndex = Math.Clamp(pageIndex, 0, totalPages - 1);

        var pagedItems = items.Skip(pageIndex * itemsPerPage).Take(itemsPerPage);
        ItemStack? displayedItem = pagedItems.First();

        string footer = string.IsNullOrWhiteSpace(filter) ?
        $"Page {pageIndex + 1} of {totalPages}" :
        $"Page {pageIndex + 1} of {totalPages} — for '{filter}'";

        string authorName = ((user as IGuildUser)?.Nickname ?? user.Username) + "'s Inventory";
        var embed = ItemManager.Instance.CreateDetailedDisplay(user, displayedItem, authorName, footer);
        embed.WithColor(Color.Blue);// example of override

        // inventory_{isDetailed}_{DiscordID}_{destinationPage}_{filter}
        string baseId = $"inventory_{detailed}_{user.Id}";
        var builder = new ComponentBuilder();

        // Always show nav buttons
        builder.WithButton("🡄", customId: $"{baseId}_{pageIndex - 1}_{filter}"/*, emote: new Emoji("\u2B05")*/, style: ButtonStyle.Secondary, disabled: pageIndex == 0);
        builder.WithButton("🡆", customId: $"{baseId}_{pageIndex + 1}_{filter}"/*, emote: new Emoji("\u27A1")*/, style: ButtonStyle.Secondary, disabled: pageIndex == totalPages - 1);

        if (detailed)
        {
            string sellId = $"opensell_{ShopManager.SHOP_DISCORD_ID}_{displayedItem.Item.DbReference.Id}_1_{filterParam}";
            builder.WithButton($"Show {playerService.GetByDiscordId(ShopManager.SHOP_DISCORD_ID.ToString()).Name}", customId: sellId, style: ButtonStyle.Success, emote: new Emoji("🏚️"));
        }

        return (embed.Build(), builder.Build());
    }



    public async Task ShowInventoryPage(
     ISocketMessageChannel location,
     IUser user,
     int pageIndex,
     PlayerProfileService profileService,
     PlayerService playerService,
     bool detailed,
     IUserMessage? existingMessage = null)
    {
        var result = BuildInventoryPage(user, pageIndex, profileService, playerService, detailed, null);
        if (result == null)
        {
            await location.SendMessageAsync($"{user.Mention}, your inventory is empty or you are not registered.");
            return;
        }

        var (embed, components) = result.Value;

        if (existingMessage != null)
        {
            await existingMessage.ModifyAsync(msg =>
            {
                msg.Embed = embed;
                msg.Components = components;
            });
        }
        else
        {
            await location.SendMessageAsync(embed: embed, components: components);
        }
    }


}
