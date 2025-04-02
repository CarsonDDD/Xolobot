using Discord;
using Discord.WebSocket;
using Hackathon.Services;

namespace Hackathon.Managers.Inventory;

public class InventoryManager
{
    private static InventoryManager _instance;
    private InventoryManager() { }
    public static InventoryManager Instance => _instance ??= new InventoryManager();

    private const int ITEMS_PER_PAGE = 1;

    public (Embed embed, MessageComponent components)? BuildInventoryPage(
           IUser user,
           int pageIndex,
           PlayerProfileService profileService,
           PlayerService playerService,
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

        bool isFiltered = !string.IsNullOrWhiteSpace(filter);
        int itemsPerPage = isFiltered ? 1 : ITEMS_PER_PAGE;

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
            string tags = item.Tags.Any() ? string.Join(", ", item.Tags.Select(t => t.Label)) : "None";

            if (isFiltered)
            {
                embed.Title = item.Item.Name;
                embed.Description = item.Item.LongDescription ?? "No description.";
                embed.WithImageUrl(item.Item.ImgUrl ?? "");
                embed.AddField("Cost", $"{item.Item.BaseCost} gp", true);
                embed.AddField("Weight", item.Item.Weight.ToString(), true);
                embed.AddField("Tags", tags, false);
            }
            else
            {
                embed.AddField(item.Item.Name,
                    $"Cost: {item.Item.BaseCost} | Weight: {item.Item.Weight}\nTags: {tags}", false);
            }
        }

        string baseId = isFiltered ? $"inventory_filtered_{filter}_{user.Id}" : $"inventory_page_{user.Id}";

        var builder = new ComponentBuilder();

        if (pageIndex > 0)
            builder.WithButton("Previous", customId: $"{baseId}_{pageIndex - 1}", emote: new Emoji("\u2B05"));

        if (pageIndex < totalPages - 1)
            builder.WithButton("Next", customId: $"{baseId}_{pageIndex + 1}", emote: new Emoji("\u27A1"));

        return (embed.Build(), builder.Build());
    }

    public async Task ShowInventoryPage(
        ISocketMessageChannel location,
        IUser user,
        int pageIndex,
        PlayerProfileService profileService,
        PlayerService playerService,
        IUserMessage? existingMessage = null)
    {
        var result = BuildInventoryPage(user, pageIndex, profileService, playerService);
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
