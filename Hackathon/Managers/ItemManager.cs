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
}
