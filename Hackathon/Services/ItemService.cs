using Dapper;
using Hackathon.DomainObjects;
using Hackathon.Entities;

namespace Hackathon.Services;

public class ItemService(DatabaseService db)
{
    private readonly DatabaseService _db = db;

    public Item? GetById(int itemId)
    {
        using var conn = _db.GetConnection();
        return conn.QuerySingleOrDefault<Item>(
            "SELECT * FROM Item WHERE id = @id",
            new { id = itemId }
        );
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
                new { itemId }
            )
            .ToList();
    }

    public ItemWithTags? GetItemWithTags(int itemId)
    {
        var item = GetById(itemId);
        if (item == null)
            return null;

        var tags = GetTagsForItem(itemId);
        return new ItemWithTags { DbReference = item, Tags = tags };
    }
}
