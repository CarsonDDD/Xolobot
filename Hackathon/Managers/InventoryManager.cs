using Discord;
using Discord.WebSocket;
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
            if (filter.Contains("_")) return null; // Prevent invalid identifiers

            var terms = filter.ToLowerInvariant()
                              .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            items = items.Where(i => terms.All(term =>
                i.Item.DbReference.Name.ToLower().Contains(term) ||
                i.Item.Tags.Any(t => t.Label.ToLower().Contains(term))
            )).ToList();
        }

        if (items.Count == 0) return null;

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
            }
            else
            {
                // Compact view: list items in a field
                embed.AddField(item.Item.DbReference.Name,
                    $"Cost: {item.DbMeta.ActualCost} | Weight: {item.Item.DbReference.Weight}\nTags: {tags}", false);
            }
        }

        // Incorporate the detailed flag in the base id.
        string baseId = !string.IsNullOrWhiteSpace(filter)
            ? $"inventory_filtered_{filter}_{(detailed ? "detailed" : "compact")}_{user.Id}"
            : $"inventory_page_{(detailed ? "detailed" : "compact")}_{user.Id}";

        var builder = new ComponentBuilder();

        // Always show nav buttons
        builder.WithButton(" ", customId: $"{baseId}_{pageIndex - 1}", emote: new Emoji("\u2B05"), disabled: pageIndex == 0);
        builder.WithButton(" ", customId: $"{baseId}_{pageIndex + 1}", emote: new Emoji("\u27A1"), disabled: pageIndex == totalPages - 1);

        if (detailed)
        {
            // item id.....
            //itemInQuestion.Item.Id
            string sellId = $"inventory_sell_{user.Id}_{items[pageIndex].Item.DbReference.Id}";// Note this is refencing item and NOT inventory item... Is this bad?
            builder.WithButton(" ", customId: sellId, emote: new Emoji("\uD83D\uDC4C"));
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
