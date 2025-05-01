using Discord;
using DnsClient.Protocol;
using Hackathon.DomainObjects;

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
        string? tags = itemStack.Item.Tags.Any() ? string.Join(", ", itemStack.Item.Tags) : null;

        EmbedBuilder display = new EmbedBuilder()
            .WithAuthor(author =>
            {
                author.IconUrl = user.GetAvatarUrl();
                author.Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
                author.Name = authorName;
            })
            .WithTitle(
                $"{(itemStack.DbMeta.Amount > 1 ? $"({itemStack.DbMeta.Amount}) " : "")}{itemStack.Item.DbReference.Name} — *{itemStack.DbMeta.ActualCost}gp*"
            )
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

    /*public ComponentBuilder CreateItemSelector(
        InventoryWithItems inventory,
        ItemStack currentItem,
        string itemSelectorMenuCustomId,
        string itemSelectorCustomId,
        string quanitySelectorMenuCustomId,
        string quanitySelectorCustomId,
        int currentQuantity
    )
    {
        ComponentBuilder menus = new ComponentBuilder();

        // item selector
        var itemSelector = new List<SelectMenuOptionBuilder>();
        int maxItem = Math.Min(25, inventory.Items.Count); // 25 is max
        for (int i = 0; i < maxItem; i++)
        {
            string itemName = inventory.Items[i].Item.DbReference.Name;
            int itemId = inventory.Items[i].Item.DbReference.Id;
            //opensell_{non-componentInteractorDisocrdID}_{itemId}_0_{filterParam} // 0 as starting amount
            itemSelector.Add(
                new SelectMenuOptionBuilder(
                    label: itemName,
                    description: string.Join(", ", inventory.Items[i].Item.Tags),
                    value: itemSelectorCustomId.Replace("{i}", itemId.ToString())
                )
            );
        }
        menus.WithSelectMenu(
            customId: itemSelectorMenuCustomId,
            options: itemSelector,
            placeholder: currentItem.Item.DbReference.Name
        );

        // generate amount list.
        var quantityOptions = new List<SelectMenuOptionBuilder>();
        int maxQuant = Math.Min(25, currentItem.DbMeta.Amount); // 25 is max
        for (int i = 0; i < maxQuant; i++)
        {
            //opensell_{non-componentInteractorDisocrdID}_{itemId}_{quantity}_{filterParam}
            quantityOptions.Add(
                new SelectMenuOptionBuilder(
                    label: (i + 1).ToString(),
                    value: quanitySelectorCustomId.Replace("{i}", (i + 1).ToString())
                )
            );
        }
        menus.WithSelectMenu(
            customId: quanitySelectorMenuCustomId,
            options: quantityOptions,
            placeholder: currentQuantity > 0 ? currentQuantity.ToString() : "Select Quantity"
        );

        return menus;
    }*/

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
        int maxItem = Math.Min(25, inventory.Items.Count); // Discord max is 25

        for (int i = 0; i < maxItem; i++)
        {
            string itemName = inventory.Items[i].Item.DbReference.Name;
            int itemId = inventory.Items[i].Item.DbReference.Id;

            itemOptions.Add(
                new SelectMenuOptionBuilder(
                    label: itemName,
                    description: string.Join(", ", inventory.Items[i].Item.Tags),
                    value: itemSelectorCustomId.Replace("{i}", itemId.ToString())
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
