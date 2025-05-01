using Hackathon.Entities;

namespace Hackathon.DomainObjects;

public class InventoryWithItems
{
    public Inventory Inventory { get; set; }
    public List<ItemStack> Items { get; set; }

    public List<ItemStack> FilterList(string[] terms)
    {
        if (terms == null || terms.Length == 0)
            return Items; // No filtering if there are no terms provided.

        var lowerTerms = terms.Select(term => term.ToLowerInvariant()).ToList();

        var filtered = this
            .Items.Where(stack =>
                lowerTerms.Any(term =>
                    stack.Item.DbReference.Name.ToLowerInvariant().Contains(term)
                )
                || stack.Item.Tags.Any(tag =>
                    lowerTerms.Any(term => tag.Label.ToLowerInvariant().Contains(term))
                )
            )
            .ToList();

        return filtered;
    }

    public InventoryWithItems FilteredInventory(string[] terms)
    {
        return new InventoryWithItems { Inventory = this.Inventory, Items = FilterList(terms) };
    }
}

public class ItemWithTags
{
    public Item DbReference { get; set; }
    public List<Tag> Tags { get; set; }
}

public class ItemStack
{
    public InventoryItem DbMeta { get; set; } // Contains inventory meta for the item (quantity, local price for the items in the stack)
    public ItemWithTags Item { get; set; } // compound Item from db
}
