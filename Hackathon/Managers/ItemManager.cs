using Discord;
using DnsClient.Protocol;
using Hackathon.DomainObjects;

namespace Hackathon.Managers;

public class ItemManager
{
    private static ItemManager _instance;
    private ItemManager() { }
    public static ItemManager Instance => _instance ??= new ItemManager();



    public EmbedBuilder CreateDetailedDisplay(IUser user, ItemStack itemStack, string authorName, string footer)
    {
        string? tags = itemStack.Item.Tags.Any() ? string.Join(", ", itemStack.Item.Tags) : null;

        EmbedBuilder display = new EmbedBuilder()
        .WithAuthor(author =>
        {
            author.IconUrl = user.GetAvatarUrl();
            author.Url = "https://www.youtube.com/watch?v=uKxyLmbOc0Q";
            author.Name = authorName;
        })
        .WithTitle($"{(itemStack.DbMeta.Amount > 1 ? $"({itemStack.DbMeta.Amount}) " : "")}{itemStack.Item.DbReference.Name} — *{itemStack.DbMeta.ActualCost}gp*")
        .WithDescription(tags == null ? "" : $"> *{tags}*")
        .WithFooter(footer)
        .WithImageUrl(itemStack.Item.DbReference.ImgUrl)
        //.WithThumbnailUrl(itemStack.Item.DbReference.ImgUrl)
        .WithColor(Color.Purple);

        //display.AddField("Amount:", itemStack.DbMeta.Amount, true);
        //display.AddField("Cost", $"{itemStack.DbMeta.ActualCost} gp", true);
        //display.AddField("Weight", itemStack.Item.DbReference.Weight.ToString(), true);
        display.AddField("Description:", itemStack.Item.DbReference.LongDescription + "\n", false);

        return display;
    }

    public EmbedBuilder CreateSmallDisplay(IUser embedAuthor, string authorName, ItemStack itemStack, int quantity)
    {
        EmbedBuilder display = new EmbedBuilder()
            .WithAuthor(author =>
            {
                author.IconUrl = embedAuthor.GetAvatarUrl();
                author.Url = "https://www.youtube.com/watch?v=uKxyLmbOc0Q";
                author.Name = authorName;
            })
            .WithTitle(itemStack.Item.DbReference.Name)
            .WithDescription(itemStack.Item.DbReference.LongDescription ?? "No description.")
            .WithThumbnailUrl(itemStack.Item.DbReference.ImgUrl ?? "")
            .WithFooter("footer")
            .WithColor(Color.Purple);


        display.AddField("Price-Per-Unit:", itemStack.DbMeta.ActualCost + "gp", true);
        display.AddField("{total}:", itemStack.DbMeta.ActualCost * quantity + "gp", true);

        return display;
    }

    public EmbedBuilder CreateItemNotFound()
    {
        var emptyEmbed = new EmbedBuilder()
        .WithTitle("Nothing Available!")
        .WithDescription("There are no items available with the specified search criteria.\nPlease try a different search or check back later.")
        .WithColor(Color.DarkRed);

        return emptyEmbed;
    }

    /*public (Embed embed, MessageComponent components) BuildItemDisplayPage(
        IUser user,
        List<ItemStack> items,
        int pageIndex,
        int itemsPerPage,
        bool detailed,
        string customIdPrefix,    // e.g. "inventory_page_" or "shop_page_"
        string headerText,
        string filter,
        string authorSuffix,
        Color embedColor,
        Func<ItemStack, string> itemButtonLabelFunc = null // optional: for the action button label
    )
    {
        int totalPages = (int)Math.Ceiling(items.Count / (double)itemsPerPage);
        pageIndex = Math.Clamp(pageIndex, 0, totalPages - 1);
        var pagedItems = items.Skip(pageIndex * itemsPerPage).Take(itemsPerPage).ToList();

        ItemStack? displayedItem = pagedItems.First();

        string footer = filter == null ?
        $"Page {pageIndex + 1} of {totalPages}" :
        $"Page {pageIndex + 1} of {totalPages} — for '{filter}'";

        string authorName = ((user as IGuildUser)?.Nickname ?? user.Username) + authorSuffix;
        var embed = Instance.CreateDetailedDisplay(user, displayedItem, authorName, footer);
        embed.WithColor(embedColor);

        string filterParam = !string.IsNullOrWhiteSpace(filter) ? filter : "";

        //shop_{isdetailed}_{whosInvDiscordID}_{destinationPage}_{filter}
        //inv_{}
        string baseId = $"{customIdPrefix}_{(string.IsNullOrWhiteSpace(filterParam) ? "" : "filtered_" + filterParam + "_")}{(detailed ? "detailed" : "compact")}_{user.Id}";

        var builder = new ComponentBuilder();
        // Navigation buttons: always show them and disable as needed.
        builder.WithButton("🡄", customId: $"{baseId}_{pageIndex - 1}", style: ButtonStyle.Secondary, disabled: pageIndex == 0);
        builder.WithButton("🡆", customId: $"{baseId}_{pageIndex + 1}", style: ButtonStyle.Secondary, disabled: pageIndex == totalPages - 1);

        // If detailed view, add an action button (for example, for buy/sell)
        if (detailed && pagedItems.Count > 0)
        {
            // Use the provided function to generate a button label if given,
            // otherwise, default to a generic "Select" text.
            string actionLabel = itemButtonLabelFunc != null
                ? itemButtonLabelFunc(pagedItems.First())
                : "Select";

            // Build the action button custom id. For example, for a buy action:
            // Format: openbuy_{sellerDiscordId}_{itemId}_0_{filterParam}
            // customIdPrefix might be "openbuy"
            string actionButtonCustomId = $"{customIdPrefix}_{pagedItems.First().Item.DbReference.Id}_0_{filterParam}";
            builder.WithButton(actionLabel, customId: actionButtonCustomId, style: ButtonStyle.Success);
        }

        return (embedBuilder.Build(), builder.Build());
    }*/

}
