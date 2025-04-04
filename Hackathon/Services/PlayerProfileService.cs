using Dapper;
using Hackathon.DomainObjects;
using Hackathon.Entities;

namespace Hackathon.Services;

public class PlayerProfileService
{
    private readonly DatabaseService _db;

    public PlayerProfileService(DatabaseService db)
    {
        _db = db;
    }

    public PlayerProfile? GetProfile(int playerId)
    {
        using var conn = _db.GetConnection();

        var player = conn.QuerySingleOrDefault<Player>("SELECT * FROM Player WHERE id = @id", new { id = playerId });
        if (player == null) return null;

        var stats = conn.Query<Stat>("SELECT * FROM Stat WHERE player_id = @id", new { id = playerId }).ToList();

        var classes = conn.Query<Class>(
            @"SELECT c.* FROM PlayerClass pc
              JOIN Class c ON c.id = pc.class_id
              WHERE pc.player_id = @id", new { id = playerId }).ToList();

        var races = conn.Query<Race>(
            @"SELECT r.* FROM PlayerRace pr
              JOIN Race r ON r.id = pr.race_id
              WHERE pr.player_id = @id", new { id = playerId }).ToList();

        var languages = conn.Query<Language>(
            @"SELECT l.* FROM PlayerLanguage pl
              JOIN Language l ON l.id = pl.language_id
              WHERE pl.player_id = @id", new { id = playerId }).ToList();

        var proficiencies = conn.Query<Proficiency>(
            @"SELECT p.* FROM PlayerProficiency pp
              JOIN Proficiency p ON p.id = pp.proficiency_id
              WHERE pp.player_id = @id", new { id = playerId }).ToList();

        var inventory = conn.QuerySingleOrDefault<Inventory>(
            "SELECT * FROM Inventory WHERE player_id = @id", new { id = playerId });

        List<InventoryDisplayItem> displayList = new();//List<ItemWithTags> itemList = new();

        if (inventory != null)
        {
            var inventoryItems = conn.Query<InventoryItem>(
                "SELECT * FROM InventoryItem WHERE inventory_id = @invId", new { invId = inventory.Id }).ToList();

            foreach (var invItem in inventoryItems)
            {
                // Resolve the item data.
                var item = conn.QuerySingle<Item>("SELECT * FROM Item WHERE id = @id", new { id = invItem.Item_Id });

                // Retrieve associated tags.
                var tags = conn.Query<Tag>(
                    @"SELECT t.* FROM ItemTag it
                  JOIN Tag t ON t.id = it.tag_id
                  WHERE it.item_id = @itemId",
                    new { itemId = item.Id }).ToList();

                // Build the ItemWithTags instance.
                var itemWithTags = new ItemWithTags
                {
                    DbReference = item,
                    Tags = tags
                };

                // Build the InventoryDisplayItem including the quantity and actual cost.
                displayList.Add(new InventoryDisplayItem
                {
                    DbReference = invItem,
                    Item = itemWithTags,
                });
            }
        }

        return new PlayerProfile
        {
            Player = player,
            Stats = stats,
            Classes = classes,
            Races = races,
            Languages = languages,
            Proficiencies = proficiencies,
            Inventory = inventory == null ? null : new InventoryWithItems
            {
                Inventory = inventory,
                Items = displayList
            }
        };
    }
}

