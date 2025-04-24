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
        return conn.QuerySingleOrDefault<Inventory>(
            "SELECT * FROM Inventory WHERE player_id = @playerId",
            new { playerId }
        );
    }

    public List<InventoryItem> GetItemsInInventory(int inventoryId)
    {
        using var conn = _db.GetConnection();
        return conn.Query<InventoryItem>(
                "SELECT * FROM InventoryItem WHERE inventory_id = @inventoryId",
                new { inventoryId }
            )
            .ToList();
    }

    public InventoryWithItems? GetInventoryWithItems(int playerId)
    {
        var inventory = GetInventoryForPlayer(playerId);
        if (inventory == null)
            return null;

        var itemStacks = GetItemsInInventory(inventory.Id);

        var conn = _db.GetConnection();
        var displayItems = new List<ItemStack>();

        foreach (var invItem in itemStacks)
        {
            // Retrieve the item details.
            var item = conn.QuerySingle<Item>(
                "SELECT * FROM Item WHERE id = @id",
                new { id = invItem.Item_Id }
            );

            // Retrieve the tags associated with the item.
            var tags = conn.Query<Tag>(
                    "SELECT t.* FROM ItemTag it JOIN Tag t ON t.id = it.tag_id WHERE it.item_id = @itemId",
                    new { itemId = item.Id }
                )
                .ToList();

            // Map to InventoryDisplayItem.
            displayItems.Add(
                new ItemStack
                {
                    DbMeta = invItem,
                    Item = new ItemWithTags { DbReference = item, Tags = tags },
                }
            );
        }

        return new InventoryWithItems { Inventory = inventory, Items = displayItems };
    }

    // Both these functions may be useless, as we can easily get this info from the PlayerProfile DomainObj....However, for single lookups this may be faster
    /// <summary>
    /// Gets a single inventory item stack from a player given the actual Item id from the Items table.
    /// Returns null if the item is not found in the player's inventory.
    /// </summary>
    public ItemStack? GetInventoryItemStack(int itemId, int playerId)
    {
        // First, get the player's inventory.
        var inventory = GetInventoryForPlayer(playerId);
        if (inventory == null)
            return null;

        using var conn = _db.GetConnection();

        // Look for an inventory entry (stack) matching the given item id.
        var invItem = conn.QuerySingleOrDefault<InventoryItem>(
            "SELECT * FROM InventoryItem WHERE inventory_id = @inventoryId AND item_id = @itemId",
            new { inventoryId = inventory.Id, itemId }
        );

        if (invItem == null)
            return null;

        // Get the details for the corresponding Item.
        var item = conn.QuerySingleOrDefault<Item>(
            "SELECT * FROM Item WHERE id = @id",
            new { id = invItem.Item_Id }
        );
        if (item == null)
            return null;

        // Retrieve the tags associated with the item.
        var tags = conn.Query<Tag>(
                @"SELECT t.* FROM ItemTag it 
              JOIN Tag t ON t.id = it.tag_id 
              WHERE it.item_id = @itemId",
                new { itemId = item.Id }
            )
            .ToList();

        // Build the compound ItemWithTags.
        var itemWithTags = new ItemWithTags { DbReference = item, Tags = tags };

        // Create the inventory display object (ItemStack) that will be returned.
        var stack = new ItemStack { DbMeta = invItem, Item = itemWithTags };

        return stack;
    }

    public ItemStack? GetInventoryItemStack(int itemId, ulong discordId)
    {
        using var conn = _db.GetConnection();
        // First, look up the player by their Discord id.
        var player = conn.QuerySingleOrDefault<Player>(
            "SELECT * FROM Player WHERE discordId = @discordId",
            new { discordId = discordId.ToString() }
        );
        if (player == null)
            return null;

        return GetInventoryItemStack(itemId, player.Id);
    }
}
