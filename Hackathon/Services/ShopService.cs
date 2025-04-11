using System.Data;
using Dapper;
using Hackathon.Entities;

namespace Hackathon.Services;

public class ShopService
{
    public enum ShopResult
    {
        Success,
        InsufficientQuantity,
        InsufficientFunds,
        ItemNotFound,
        UnknownError
    }

    private readonly DatabaseService _db;
    public ShopService(DatabaseService db)
    {
        _db = db;
    }

    /// <summary>
    /// Executes a transaction to transfer an item from the seller to the buyer,
    /// updating both parties’ gold and inventory.
    /// 
    /// For a "buy" operation, giverDiscordId should be the shopkeeper's Discord ID
    /// (the seller) and takerDiscordId the buyer's Discord ID.
    /// </summary>
    public async Task<ShopResult> ExecuteTransaction(string type, string giverDiscordId, string takerDiscordId, int itemId, int quantity)
    {
        using var conn = _db.GetConnection();
        // Open a transaction so that we guarantee atomic updates.
        using var transaction = conn.BeginTransaction();
        try
        {
            // Fetch seller (the giver) by Discord ID.
            var seller = await conn.QuerySingleOrDefaultAsync<Player>(
                "SELECT * FROM Player WHERE discordId = @discordId",
                new { discordId = giverDiscordId },
                transaction
            );
            if (seller == null)
                return ShopResult.ItemNotFound;

            // Fetch buyer (the taker) by Discord ID.
            var buyer = await conn.QuerySingleOrDefaultAsync<Player>(
                "SELECT * FROM Player WHERE discordId = @discordId",
                new { discordId = takerDiscordId },
                transaction
            );
            if (buyer == null)
                return ShopResult.UnknownError;

            // Get seller's inventory.
            var sellerInventory = await conn.QuerySingleOrDefaultAsync<Inventory>(
                "SELECT * FROM Inventory WHERE player_id = @sellerId",
                new { sellerId = seller.Id },
                transaction
            );
            if (sellerInventory == null)
                return ShopResult.ItemNotFound;

            // Get seller's item stack for the given item.
            var sellerInvItem = await conn.QuerySingleOrDefaultAsync<InventoryItem>(
                "SELECT * FROM InventoryItem WHERE inventory_id = @inventoryId AND item_id = @itemId",
                new { inventoryId = sellerInventory.Id, itemId },
                transaction
            );
            if (sellerInvItem == null)
                return ShopResult.ItemNotFound;

            // Ensure seller has enough quantity.
            if (sellerInvItem.Amount < quantity)
                return ShopResult.InsufficientQuantity;

            // Compute the total cost. (Using the seller's actual cost.)
            int totalCost = sellerInvItem.ActualCost * quantity;

            // Check if buyer has enough gold.
            if (buyer.Gold < totalCost)
                return ShopResult.InsufficientFunds;

            // Update buyer's gold: subtract totalCost.
            await conn.ExecuteAsync(
                "UPDATE Player SET gold = gold - @cost WHERE id = @buyerId",
                new { cost = totalCost, buyerId = buyer.Id },
                transaction
            );

            // Update seller's gold: add totalCost.
            await conn.ExecuteAsync(
                "UPDATE Player SET gold = gold + @cost WHERE id = @sellerId",
                new { cost = totalCost, sellerId = seller.Id },
                transaction
            );

            // Update seller's inventory: reduce the quantity.
            int newSellerQty = sellerInvItem.Amount - quantity;
            if (newSellerQty > 0)
            {
                await conn.ExecuteAsync(
                    "UPDATE InventoryItem SET amount = @newQty WHERE id = @id",
                    new { newQty = newSellerQty, id = sellerInvItem.Id },
                    transaction
                );
            }
            else
            {
                // Remove the item from seller's inventory if the quantity becomes zero.
                await conn.ExecuteAsync(
                    "DELETE FROM InventoryItem WHERE id = @id",
                    new { id = sellerInvItem.Id },
                    transaction
                );
            }

            // Update buyer's inventory.
            // First, retrieve buyer's inventory. Create one if it doesn't exist.
            var buyerInventory = await conn.QuerySingleOrDefaultAsync<Inventory>(
                "SELECT * FROM Inventory WHERE player_id = @buyerId",
                new { buyerId = buyer.Id },
                transaction
            );
            if (buyerInventory == null)
            {
                // Insert a new inventory record for the buyer.
                int newInventoryId = await conn.ExecuteScalarAsync<int>(
                    "INSERT INTO Inventory (player_id) VALUES (@buyerId); SELECT last_insert_rowid();",
                    new { buyerId = buyer.Id },
                    transaction
                );
                buyerInventory = new Inventory { Id = newInventoryId, Player_Id = buyer.Id };
            }

            // Check if the buyer already has an InventoryItem for this item.
            var buyerInvItem = await conn.QuerySingleOrDefaultAsync<InventoryItem>(
                "SELECT * FROM InventoryItem WHERE inventory_id = @inventoryId AND item_id = @itemId",
                new { inventoryId = buyerInventory.Id, itemId },
                transaction
            );
            if (buyerInvItem != null)
            {
                // Increase the quantity.
                await conn.ExecuteAsync(
                    "UPDATE InventoryItem SET amount = amount + @quantity WHERE id = @id",
                    new { quantity, id = buyerInvItem.Id },
                    transaction
                );
            }
            else
            {
                // Insert a new row for the buyer.
                await conn.ExecuteAsync(
                    "INSERT INTO InventoryItem (inventory_id, item_id, actualCost, amount) VALUES (@inventoryId, @itemId, @actualCost, @quantity)",
                    new { inventoryId = buyerInventory.Id, itemId, actualCost = sellerInvItem.ActualCost, quantity },
                    transaction
                );
            }

            // ADJUST PRICE!!!!!!
            int newActualCost = (int)(sellerInvItem.ActualCost * 1);// TODO: This should be above one depending on if the TYPE of transaction and if the shopkeeper is involced
            await conn.ExecuteAsync(
                "UPDATE InventoryItem SET actualCost = @newActualCost WHERE inventory_id = @inventoryId AND item_id = @itemId",
                new { newActualCost, inventoryId = buyerInventory.Id, itemId },
                transaction
            );

            // Commit the transaction.
            transaction.Commit();
            return ShopResult.Success;
        }
        catch (Exception ex)
        {
            // Rollback if any error occurs.
            transaction.Rollback();
            // Optionally log the exception.
            return ShopResult.UnknownError;
        }
    }
}
