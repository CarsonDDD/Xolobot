using Hackathon.DataObjects;
using Hackathon.DataObjects.PlayerAdditions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hackathon.Services;

public class MongoDBSettings
{
	public string ConnectionString { get; set; }
	public string DatabaseName { get; set; }
}


public class MongoDBService
{
	public enum SHOPRESULT
	{
		ITEM_NOT_FOUND = -3,
		PLAYER_NOT_FOUND = -1,
		SUCCESS = 1,
		INSUFFICIENT_FUNDS = 0,
		INSUFFICIENT_QUANTITY = -2,
	}
	private readonly IMongoDatabase _database;
	private readonly ILogger _logger;

	public MongoDBService(IOptions<MongoDBSettings> settings, ILogger<MongoDBService> logger)
	{
		var client = new MongoClient(settings.Value.ConnectionString);
		_database = client.GetDatabase(settings.Value.DatabaseName);

		_logger = logger;
		_logger.LogInformation($"Connected to MongoDB {_database.DatabaseNamespace}");
	}

	public async Task<InventoryItem> GetBaseItemAsync(ReferenceItem refItem)
	{
		// Access the Items collection.
		var itemsCollection = _database.GetCollection<BaseItem>("Items");

		// Retrieve the BaseItem using the ReferenceId from the ReferenceItem.
		var filter = Builders<BaseItem>.Filter.Eq(b => b.ReferenceId, refItem.ReferenceId);
		var baseItem = await itemsCollection.Find(filter).FirstOrDefaultAsync();

		if (baseItem == null)
		{
			_logger.LogWarning($"BaseItem not found for ReferenceId: {refItem.ReferenceId}");
			return null;
		}

		// Return the pair as an InventoryItem.
		return new InventoryItem
		{
			BaseItem = baseItem,
			InventoryDetails = refItem
		};
	}




	public async Task<List<ReferenceItem>> GetShopItems()
	{
		try
		{
			var shopItemsCollection = _database.GetCollection<ReferenceItem>("ShopInventory");
			var items = await shopItemsCollection.Find(_ => true).ToListAsync();
			return items;
		}
		catch (Exception ex)
		{
			return new List<ReferenceItem> { new ReferenceItem() };
		}
	}

	public async Task<List<InventoryItem>> GetShopItems(string searchTerm)
	{
		try
		{
			var shopCollection = _database.GetCollection<ReferenceItem>("ShopInventory");

			var pipeline = new[]
			{
			new BsonDocument("$lookup", new BsonDocument
			{
				{ "from", "Items" },
				{ "localField", "ReferenceId" },
				{ "foreignField", "ReferenceId" },
				{ "as", "BaseItem" }
			}),
			new BsonDocument("$unwind", "$BaseItem"),
			new BsonDocument("$match", new BsonDocument("$or", new BsonArray
			{
				new BsonDocument("BaseItem.Name", new BsonDocument("$regex", searchTerm).Add("options", "i")),
				new BsonDocument("BaseItem.Tags", new BsonDocument("$regex", searchTerm).Add("options", "i"))
			}))
		};

			var aggregate = shopCollection.Aggregate<BsonDocument>(pipeline);
			var results = await aggregate.ToListAsync();

			var shopItems = results.Select(doc => new ShopItem
			{
				BaseItem = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<BaseItem>(doc["BaseItem"].AsBsonDocument),
				InventoryDetails = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<ReferenceItem>(doc)
			}).ToList();

			return shopItems;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, $"Error retrieving shop items with search term: {searchTerm}");
			return new List<ShopItem>();
		}
	}


	public async Task<List<PlayerObject>> GetAllPlayers()
	{
		var playerCollection = _database.GetCollection<PlayerObject>("Players");
		List<PlayerObject> players = await playerCollection.Find(_ => true).ToListAsync();
		return players;
	}

	public async Task<List<PlayerObject>> GetPlayer(string discordId)
	{
		var playerCollection = _database.GetCollection<PlayerObject>("Players");
		var filter = Builders<PlayerObject>.Filter.Eq("player.discordId", discordId);

		List<PlayerObject> players = await playerCollection.Find(filter).ToListAsync();

		if (players.Count > 1) Console.WriteLine("huh, it seems discord id:'" + discordId + "' has " + players.Count + "players assosiated with them.");

		return players;
	}


	public async Task<SHOPRESULT> BuyItem(string discordId, string referenceId, int quantity = 1)
	{
		if (quantity <= 0) return SHOPRESULT.INSUFFICIENT_QUANTITY;

		// Suppose "ShopInventory" is a collection of ReferenceItem documents, 
		var shopCollection = _database.GetCollection<ReferenceItem>("ShopInventory");
		var shopItem = await shopCollection
			.Find(s => s.Item.ReferenceId == referenceId)
			.FirstOrDefaultAsync();

		if (shopItem == null) return SHOPRESULT.ITEM_NOT_FOUND;
		if (shopItem.Quantity < quantity) return SHOPRESULT.INSUFFICIENT_QUANTITY;

		// Get the player
		var playerCollection = _database.GetCollection<PlayerObject>("Players");
		var playerFilter = Builders<PlayerObject>.Filter.Eq("player.discordId", discordId);
		var player = await playerCollection.Find(playerFilter).FirstOrDefaultAsync();

		if (player == null) return SHOPRESULT.PLAYER_NOT_FOUND;

		// Check cost
		var totalCost = shopItem.Cost * quantity;
		if (player.treasure.gold < totalCost) return SHOPRESULT.INSUFFICIENT_FUNDS;

		// Deduct player gold
		var newGoldAmount = player.treasure.gold - totalCost;

		// Add or update player's inventory
		var existing = player.inventory.FirstOrDefault(
			x => x.Item.ReferenceId == referenceId
		);
		if (existing != null)
		{
			existing.Quantity += quantity;
		}
		else
		{
			// Add a new entry copying the BaseItem and cost, but overriding quantity
			player.inventory.Add(new ReferenceItem
			{
				Item = shopItem.Item,
				Cost = shopItem.Cost,    // or use BaseItem.BaseCost if you want
				Quantity = quantity
			});
		}

		// Update the player's doc
		var updatePlayer = Builders<PlayerObject>.Update
			.Set(p => p.treasure.gold, newGoldAmount)
			.Set(p => p.inventory, player.inventory);

		await playerCollection.UpdateOneAsync(filter, updatePlayer);

		// Update (or remove) the shop item
		var newShopQuantity = shopItem.Quantity - quantity;
		if (newShopQuantity <= 0)
		{
			// Remove the item from the shop entirely if it hits zero
			await shopCollection.DeleteOneAsync(s => s.Item.ReferenceId == referenceId);
		}
		else
		{
			// Decrement the existing shop quantity
			var updateShop = Builders<ReferenceItem>.Update
				.Set(s => s.Quantity, newShopQuantity);
			await shopCollection.UpdateOneAsync(
				s => s.Item.ReferenceId == referenceId,
				updateShop
			);
		}

		return SHOPRESULT.SUCCESS;
	}
}
