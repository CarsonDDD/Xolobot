using Hackathon.Entities;
namespace Hackathon.DomainObjects;


public class InventoryWithItems
{
    public Inventory Inventory { get; set; }
    public List<ItemWithTags> Items { get; set; }
}

public class ItemWithTags
{
    public Item Item { get; set; }
    public List<Tag> Tags { get; set; }
}

