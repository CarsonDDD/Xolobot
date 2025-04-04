using Hackathon.Entities;
namespace Hackathon.DomainObjects;


public class InventoryWithItems
{
    public Inventory Inventory { get; set; }
    public List<InventoryDisplayItem> Items { get; set; }
}

public class ItemWithTags
{
    public Item DbReference { get; set; }
    public List<Tag> Tags { get; set; }
}

public class InventoryDisplayItem
{
    public InventoryItem DbReference { get; set; } //db, MAYBE WE SHOULD NEVER USE THIS!?!?!?!
    public ItemWithTags Item { get; set; }// compound Item from db
}

