using Hackathon.Entities;
namespace Hackathon.DomainObjects;


public class InventoryWithItems
{
    public Inventory Inventory { get; set; }
    public List<ItemStack> Items { get; set; }
}

public class ItemWithTags
{
    public Item DbReference { get; set; }
    public List<Tag> Tags { get; set; }
}

public class ItemStack
{
    public InventoryItem DbReference { get; set; } // Contains inventory meta for the item (quantity, local price for the items in the stack)
    public ItemWithTags Item { get; set; }// compound Item from db
}

