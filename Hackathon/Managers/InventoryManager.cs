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

        if (items.Count == 0)
        {
            var emptyEmbed = new EmbedBuilder()
                .WithTitle("Nothing Available!")
                .WithDescription("There are no items available with the specified search criteria.\nPlease try a different search or check back later.")
                .WithColor(Color.DarkRed)
                .Build();

            return (emptyEmbed, new ComponentBuilder().Build());
        }

        // Use the detailed parameter solely to control view style:
        // If detailed is true, show one item per page regardless of filtering.
        int itemsPerPage = detailed ? 1 : ITEMS_PER_PAGE;
        int totalPages = (int)Math.Ceiling(items.Count / (double)itemsPerPage);
        pageIndex = Math.Clamp(pageIndex, 0, totalPages - 1);

        var pagedItems = items.Skip(pageIndex * itemsPerPage).Take(itemsPerPage);

        var embed = new EmbedBuilder()
            .WithAuthor(user)
            .WithTitle($"{profile.Player.Name}'s Inventory")
            .WithFooter($"Page {pageIndex + 1} of {totalPages}")
            .WithColor(Color.DarkGreen);

        ItemStack? displayedItem = null;

        foreach (var item in pagedItems)
        {
            string tags = item.Item.Tags.Any() ? string.Join(", ", item.Item.Tags.Select(t => t.Label)) : "None";

            if (detailed)
            {
                // Detailed view shows one item with full info
                embed.Title = item.Item.DbReference.Name;
                embed.Description = item.Item.DbReference.LongDescription ?? "No description.";
                embed.Description += "\n\n**Amount:** " + item.DbMeta.Amount;
                embed.WithImageUrl(item.Item.DbReference.ImgUrl ?? "");
                //embed.AddField("‎ ", "**Amount:** " + item.DbReference.Amount, false);
                embed.AddField("‎ ", $"**Cost:** {item.DbMeta.ActualCost} gp", true);
                embed.AddField("‎ ", "**Weight:** " + item.Item.DbReference.Weight.ToString(), true);
                embed.AddField("Tags", tags, false);

                displayedItem = item;
            }
            else
            {
                // Compact view: list items in a field
                embed.AddField(item.Item.DbReference.Name,
                    $"Cost: {item.DbMeta.ActualCost} | Weight: {item.Item.DbReference.Weight}\nTags: {tags}", true);
            }
        }

        string filterParam = !string.IsNullOrWhiteSpace(filter) ? filter : "";

        // Incorporate the detailed flag in the base id.
        string baseId = !string.IsNullOrWhiteSpace(filter)
            ? $"inventory_filtered_{filter}_{(detailed ? "detailed" : "compact")}_{user.Id}"
            : $"inventory_page_{(detailed ? "detailed" : "compact")}_{user.Id}";

        var builder = new ComponentBuilder();

        // Always show nav buttons
        builder.WithButton("🡄", customId: $"{baseId}_{pageIndex - 1}"/*, emote: new Emoji("\u2B05")*/, style: ButtonStyle.Secondary, disabled: pageIndex == 0);
        builder.WithButton("🡆", customId: $"{baseId}_{pageIndex + 1}"/*, emote: new Emoji("\u27A1")*/, style: ButtonStyle.Secondary, disabled: pageIndex == totalPages - 1);

        if (detailed)
        {
            // item id.....
            //itemInQuestion.Item.Id
            string sellId = $"opensell_{player.DiscordId}_{displayedItem.Item.DbReference.Id}_1_{filterParam}";
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
