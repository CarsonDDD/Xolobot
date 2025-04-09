using Dapper;
using Hackathon.DomainObjects;
using Hackathon.Entities;

namespace Hackathon.Services;

public class InventoryService
{
    private readonly DatabaseService _db;

    public InventoryService(DatabaseService db)
    {
        _db = db;
    }

    public Inventory? GetInventoryForPlayer(int playerId)
    {
        using var conn = _db.GetConnection();
        return conn.QuerySingleOrDefault<Inventory>("SELECT * FROM Inventory WHERE player_id = @playerId", new { playerId });
    }

    public List<ItemStack> GetItemsInInventory(int inventoryId)
    {
        using var conn = _db.GetConnection();
        return conn.Query<ItemStack>("SELECT * FROM InventoryItem WHERE inventory_id = @inventoryId", new { inventoryId }).ToList();
    }

    public InventoryWithItems? GetInventoryWithItems(int playerId)
    {
        var inventory = GetInventoryForPlayer(playerId);
        if (inventory == null) return null;

        var itemStacks = GetItemsInInventory(inventory.Id);

        var conn = _db.GetConnection();
        var displayItems = new List<InventoryDisplayItem>();

        foreach (var invItem in itemStacks)
        {
            // Retrieve the item details.
            var item = conn.QuerySingle<Item>("SELECT * FROM Item WHERE id = @id", new { id = invItem.Item_Id });

            // Retrieve the tags associated with the item.
            var tags = conn.Query<Tag>(
                "SELECT t.* FROM ItemTag it JOIN Tag t ON t.id = it.tag_id WHERE it.item_id = @itemId",
                new { itemId = item.Id }).ToList();

            // Map to InventoryDisplayItem.
            displayItems.Add(new InventoryDisplayItem
            {
                DbReference = invItem,
                Item = new ItemWithTags
                {
                    DbReference = item,
                    Tags = tags
                },
            });
        }

        return new InventoryWithItems
        {
            Inventory = inventory,
            Items = displayItems
        };
    }
}