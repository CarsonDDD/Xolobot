using System.Globalization;
using System.Text;
using Discord;
using Hackathon.DomainObjects;
using Hackathon.Utility;

namespace Hackathon.Managers;

public class ItemManager
{
    private static ItemManager _instance;

    private ItemManager() { }

    public static ItemManager Instance => _instance ??= new ItemManager();

    public EmbedBuilder CreateDetailedDisplay(
        IUser user,
        ItemStack itemStack,
        string authorName,
        string footer
    )
    {
        string tagsField = itemStack.Item.Tags.Any()
            ? $">>> {Utils.FormatUIList(itemStack.Item.Tags.Select(t => t.Label))}"
            : ">>> ";

        EmbedBuilder display = new EmbedBuilder()
            .WithAuthor(author =>
            {
                author.IconUrl = user.GetAvatarUrl();
                author.Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
                author.Name = authorName;
            })
            .WithTitle(
                $"{(itemStack.DbMeta.Amount > 1 ? $"({itemStack.DbMeta.Amount}) " : "")}**{itemStack.Item.DbReference.Name}** — ***{itemStack.DbMeta.ActualCost}gp***"
            )
            .WithDescription(tagsField)
            .WithFooter(footer)
            .WithImageUrl(
                itemStack.Item.DbReference.ImgUrl
                    ?? "https://www.wargamer.com/wp-content/sites/wargamer/2024/10/dnd-worst-gnome-rejected.jpg"
            )
            //.WithThumbnailUrl(itemStack.Item.DbReference.ImgUrl)
            .WithColor(Color.Purple);

        //display.AddField("Amount:", itemStack.DbMeta.Amount, true);
        //display.AddField("Cost", $"{itemStack.DbMeta.ActualCost} gp", true);
        //display.AddField("Weight", itemStack.Item.DbReference.Weight.ToString(), true);

        var weight = itemStack.Item.DbReference.Weight;
        string weightString = weight switch
        {
            null => "NONE",
            < 0 => "?",
            _ => weight.ToString(),
        };

        display.AddField(
            itemStack.Item.DbReference.ShortDescription ?? "No Short Description",
            "-# Weight: " + weightString,
            false
        );

        return display;
    }

    public EmbedBuilder CreateSmallDisplay(
        IUser embedAuthor,
        string authorName,
        ItemStack itemStack,
        int quantity
    )
    {
        EmbedBuilder display = new EmbedBuilder()
            .WithAuthor(author =>
            {
                author.IconUrl = embedAuthor.GetAvatarUrl();
                author.Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
                author.Name = authorName;
            })
            .WithTitle(itemStack.Item.DbReference.Name)
            .WithDescription(itemStack.Item.DbReference.LongDescription ?? "No description.")
            .WithThumbnailUrl(
                itemStack.Item.DbReference.ImgUrl
                    ?? "https://www.wargamer.com/wp-content/sites/wargamer/2024/10/dnd-worst-gnome-rejected.jpg"
            )
            .WithFooter("footer")
            .WithColor(Color.Purple);

        display.AddField("Price-Per-Unit:", itemStack.DbMeta.ActualCost + "gp", true);
        display.AddField("{total}:", itemStack.DbMeta.ActualCost * quantity + "gp", true);
        /*display.AddField(
            "Total Weight:",
            itemStack.Item.DbReference.Weight * quantity + "lbs",
            true
        );*/

        return display;
    }

    public EmbedBuilder CreateItemNotFound()
    {
        var emptyEmbed = new EmbedBuilder()
            .WithTitle("Nothing Available!")
            .WithDescription(
                "There are no items available with the specified search criteria.\nPlease try a different search or check back later."
            )
            .WithColor(Color.DarkRed);

        return emptyEmbed;
    }

    public (EmbedBuilder embed, ComponentBuilder components) BuildItemDisplayPage(
        IUser user,
        List<ItemStack> items,
        ItemStack displayItem,
        int pageIndex,
        int itemsPerPage,
        bool detailed,
        string authorLabel,
        string filter,
        string baseId,
        ButtonBuilder selectButton
    )
    {
        // Regardless of filtering, detailed view is only when detailed=true.
        int totalPages = (int)Math.Ceiling(items.Count / (double)itemsPerPage);
        pageIndex = Math.Clamp(pageIndex, 0, totalPages - 1);

        string footer = string.IsNullOrWhiteSpace(filter)
            ? $"Page {pageIndex + 1} of {totalPages}"
            : $"Page {pageIndex + 1} of {totalPages} — for '{filter}'";

        var embed = CreateDetailedDisplay(user, displayItem, authorLabel, footer);

        var builder = new ComponentBuilder();

        // Nav
        builder.WithButton(
            "🡄",
            customId: $"{baseId}_{pageIndex - 1}_{filter}" /*, emote: new Emoji("\u2B05")*/
            ,
            style: ButtonStyle.Secondary,
            disabled: pageIndex == 0
        );
        builder.WithButton(
            "🡆",
            customId: $"{baseId}_{pageIndex + 1}_{filter}" /*, emote: new Emoji("\u27A1")*/
            ,
            style: ButtonStyle.Secondary,
            disabled: pageIndex == totalPages - 1
        );

        // Buy
        if (detailed)
        {
            builder.WithButton(selectButton);
        }

        return (embed, builder);
    }

    public ComponentBuilder CreateItemSelector(
        InventoryWithItems inventory,
        ItemStack currentItem,
        string itemSelectorMenuCustomId,
        string itemSelectorCustomId,
        string quantitySelectorMenuCustomId,
        string quantitySelectorCustomId,
        int currentQuantity
    )
    {
        ComponentBuilder menus = new ComponentBuilder();

        // item selector dropdown
        var itemOptions = new List<SelectMenuOptionBuilder>();
        //int maxItem = Math.Min(25, inventory.Items.Count); // Discord max is 25*/

        const int windowSize = 25;
        const int windowCenter = windowSize / 2;

        int currentIndex = inventory.Items.FindIndex(s =>
            s.Item.DbReference.Id == currentItem.Item.DbReference.Id
        );

        int startIndex = currentIndex - windowCenter;
        if (startIndex < 0)
            startIndex = 0; // clamp low
        if (startIndex + windowSize > inventory.Items.Count) // clamp high
            startIndex = Math.Max(0, inventory.Items.Count - windowSize);


        int endIndex = Math.Min(startIndex + windowSize, inventory.Items.Count);

        // Have the current be around the middle then propagate both sides until we have 25 items
        for (int i = startIndex; i < endIndex; i++)
        {
            string itemName = inventory.Items[i].Item.DbReference.Name;
            int itemId = inventory.Items[i].Item.DbReference.Id;

            // Description limit is 100 character
            string rawDescription = string.Join(
                ", ",
                inventory
                    .Items[i]
                    .Item.Tags.Select(tag =>
                        CultureInfo.CurrentCulture.TextInfo.ToTitleCase(tag.Label)
                    )
            );
            string safeDescription =
                rawDescription.Length > 100 ? rawDescription.Substring(0, 100) : rawDescription;


            itemOptions.Add(
                new SelectMenuOptionBuilder(
                    label: $"{i + 1}. {itemName}", // prefix on desc to show that things shift
                    description: safeDescription,
                    value: itemSelectorCustomId.Replace("{i}", itemId.ToString()),
                    isDefault: itemId == currentItem.Item.DbReference.Id
                )
            );
        }

        var itemSelectMenu = new SelectMenuBuilder()
            .WithCustomId(itemSelectorMenuCustomId)
            .WithOptions(itemOptions)
            .WithPlaceholder(currentItem.Item.DbReference.Name);

        menus.WithSelectMenu(itemSelectMenu);

        // quantity selector dropdown (reusable)
        var quantitySelectMenu = CreateQuantitySelector(
            customIdMenuTemplate: quantitySelectorMenuCustomId,
            customIdSelectorTemplate: quantitySelectorCustomId,
            maxQuantity: currentItem.DbMeta.Amount,
            currentQuantity: currentQuantity
        );

        menus.WithSelectMenu(quantitySelectMenu);

        return menus;
    }

    public SelectMenuBuilder CreateQuantitySelector(
        string customIdMenuTemplate,
        string customIdSelectorTemplate,
        int maxQuantity,
        int currentQuantity = 0
    )
    {
        var quantityOptions = new List<SelectMenuOptionBuilder>();
        int cappedMax = Math.Min(25, maxQuantity); // Discord max is 25

        for (int i = 0; i < cappedMax; i++)
        {
            quantityOptions.Add(
                new SelectMenuOptionBuilder(
                    label: (i + 1).ToString(),
                    value: customIdSelectorTemplate.Replace("{i}", (i + 1).ToString())
                )
            );
        }

        return new SelectMenuBuilder()
            .WithCustomId(customIdMenuTemplate)
            .WithOptions(quantityOptions)
            .WithPlaceholder(currentQuantity > 0 ? currentQuantity.ToString() : "Select Quantity");
    }
}
