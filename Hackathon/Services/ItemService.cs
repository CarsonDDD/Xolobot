using Dapper;
using Hackathon.DomainObjects;
using Hackathon.Entities;

namespace Hackathon.Services;

public class ItemService
{
    private readonly DatabaseService _db;

    public ItemService(DatabaseService db)
    {
        _db = db;
    }

    public Item? GetById(int itemId)
    {
        using var conn = _db.GetConnection();
        return conn.QuerySingleOrDefault<Item>("SELECT * FROM Item WHERE id = @id", new { id = itemId });
    }

    public List<Item> GetAll()
    {
        using var conn = _db.GetConnection();
        return conn.Query<Item>("SELECT * FROM Item").ToList();
    }

    public List<Tag> GetTagsForItem(int itemId)
    {
        using var conn = _db.GetConnection();
        return conn.Query<Tag>(
            "SELECT t.* FROM ItemTag it JOIN Tag t ON t.id = it.tag_id WHERE it.item_id = @itemId",
            new { itemId }).ToList();
    }

    public ItemWithTags? GetItemWithTags(int itemId)
    {
        var item = GetById(itemId);
        if (item == null) return null;

        var tags = GetTagsForItem(itemId);
        return new ItemWithTags
        {
            DbReference = item,
            Tags = tags
        };
    }

    // Both these functions may be useless, as we can easily get this info from the PlayerProfile DomainObj....However, for single lookups this may be faster 
    public ItemStack? GetInventoryDisplayItem(int itemId, int playerId)
    {
        return null;
    }

    public ItemStack? GetInventoryDisplayItem(int itemId, ulong discordId)
    {
        return null;
    }
}