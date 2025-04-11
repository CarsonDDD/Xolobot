using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Hackathon.Managers.Inventory;
using Hackathon.Managers.Shop;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System.Configuration;
using System.Reflection;

namespace Hackathon.Services;

public class InteractionHandler
{
	public class BotResponseArgs : EventArgs
	{
		public SocketMessage SocketMessage { get; set; }
		public String? Response { get; set; }
		public BotResponseArgs(SocketMessage message, String response)
		{
			SocketMessage = message;
			Response = response;
		}
	}

	private readonly DiscordSocketClient _client;
	private readonly InteractionService _interactionService;
	private readonly IServiceProvider _services;
	private readonly ILogger _logger;
	private readonly OpenAIService _openAiService;
	private readonly DatabaseService _database;
	private readonly PlayerService _playerService;
	private readonly PlayerProfileService _profileService;
	private readonly InventoryService _inventoryService;
	private readonly ShopService _shopService;

	public delegate void BotResponseEvent(object sender, BotResponseArgs e);
	public event BotResponseEvent? OnPostBotMention;

	public InteractionHandler(DiscordSocketClient client, InteractionService interactionService, IServiceProvider services, ILogger<InteractionHandler> logger, OpenAIService openAiService, DatabaseService dbService, PlayerService playerService, PlayerProfileService profileService, InventoryService inventoryService, ShopService shopService)
	{
		_interactionService = interactionService;
		_client = client;
		_services = services;
		_logger = logger;
		_openAiService = openAiService;
		_database = dbService;
		_playerService = playerService;
		_profileService = profileService;
		_inventoryService = inventoryService;
		_shopService = shopService;

		// events
		_client.ButtonExecuted += ButtonHandler;
		_client.SelectMenuExecuted += SelectMenuHandler;
	}

	public async Task InitializeAsync()
	{
		await _interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

		//_logger.LogInformation("Testing loging");
		// Logging the loaded modules
		foreach (var module in _interactionService.Modules)
		{
			_logger.LogInformation($"Loaded command module: {module.Name}");
		}

		_client.MessageReceived += HandleMessageReceived;
		_client.InteractionCreated += HandleInteraction;
		_interactionService.InteractionExecuted += HandleInteractionExecuted;
	}

	private async Task HandleMessageReceived(SocketMessage message)
	{
		if (message is SocketUserMessage userMessage && userMessage.MentionedUsers.Any(user => user.Id == _client.CurrentUser.Id))
		{
			// Only mentions towards the bot, anywhere
			String response = await HandleMention(userMessage);

			if (!string.IsNullOrEmpty(response))
			{
				OnPostBotMention?.Invoke(this, new BotResponseArgs(message, response));
			}
			//await Console.Out.WriteLineAsync("Mention TEst");
		}

		//await Console.Out.WriteLineAsync("general message Test");// anything sent, anywhere 
	}

	private async Task SelectMenuHandler(SocketMessageComponent component)
	{
		var value = string.Join(", ", component.Data.Values); // I am confused on why this is an arary for a single string.
		_logger.LogInformation($"{component.User.GlobalName}: {component.Data.CustomId}: {value}");

		if (component.Data.CustomId.StartsWith("buymenu_quantselector") || component.Data.CustomId.StartsWith("buymenu_itemselector"))
		{
			await HandleUpdateBuyMenuAmount(component, component.Data.Values);
		}
		else if (component.Data.CustomId.StartsWith("sellmenu_quantselector") || component.Data.CustomId.StartsWith("sellmenu_itemselector"))
		{
			await HandleUpdateSellMenuAmount(component, component.Data.Values);
		}
	}

	private async Task HandleUpdateSellMenuAmount(SocketMessageComponent component, IReadOnlyCollection<string> values)
	{
		var parts = values.First().Split('_');
		// openbuy_{sellerDiscordID}_{itemLedgerID}_{amountSelected}_{filter}. --- Buyer if determined ONLY on press interact

		if (parts.Length < 4)
		{
			await component.RespondAsync("error in custom ID.", ephemeral: true);
			return;
		}

		string buyerDiscordId = parts[1];
		string itemId = parts[2];
		string sellerDiscordId = component.User.Id.ToString();// This is who interacted with the button
		int startingAmount = int.Parse(parts[3]);

		// Break down filter. 
		string filterString = parts.Length >= 5 ? parts[4] : "";
		string[] filterArray = string.IsNullOrWhiteSpace(filterString)
			? new string[0]
			: filterString.Split('-');

		var buyer = _playerService.GetByDiscordId(buyerDiscordId);
		var playerSelling = _profileService.GetProfileByDiscordId(sellerDiscordId);

		if (playerSelling == null)
		{
			await component.RespondAsync("Shop not found.", ephemeral: true);
			return;
		}

		var selectedItem = _inventoryService.GetInventoryItemStack(Int32.Parse(itemId), ulong.Parse(buyerDiscordId));

		var result = ShopManager.Instance.BuildSellInteract(_client, buyer, selectedItem, playerSelling, filterArray, startingAmount);

		if (result == null)
		{
			await component.RespondAsync("No matching items.---SOMETHING WENT WRONG AAH", ephemeral: true);
			return;
		}

		var (embed, components) = result.Value;

		await component.UpdateAsync(msg =>
		{
			msg.Embed = embed;
			msg.Components = components;
		});
	}

	private async Task HandleUpdateBuyMenuAmount(SocketMessageComponent component, IReadOnlyCollection<string> values)
	{
		var parts = values.First().Split('_');
		// openbuy_{sellerDiscordID}_{itemLedgerID}_{amountSelected}_{filter}. --- Buyer if determined ONLY on press interact

		if (parts.Length < 4)
		{
			await component.RespondAsync("error in custom ID.", ephemeral: true);
			return;
		}

		string sellerDiscordId = parts[1];
		string itemId = parts[2];// define what I actuall mean by this.
		string buyerDiscordId = component.User.Id.ToString();// This is who interacted with the button
		int startingAmount = int.Parse(parts[3]);

		// Break down filter. 
		string filterString = parts.Length >= 5 ? parts[4] : "";
		string[] filterArray = string.IsNullOrWhiteSpace(filterString)
			? new string[0]
			: filterString.Split('-');

		var player = _playerService.GetByDiscordId(buyerDiscordId);
		var shop = _profileService.GetProfileByDiscordId(sellerDiscordId);

		if (shop == null)
		{
			await component.RespondAsync("Shop not found.", ephemeral: true);
			return;
		}

		var shopItem = _inventoryService.GetInventoryItemStack(Int32.Parse(itemId), ulong.Parse(sellerDiscordId));

		var result = ShopManager.Instance.BuildBuyInteract(_client, player, shopItem, shop, filterArray, startingAmount);

		if (result == null)
		{
			await component.RespondAsync("No matching items.---SOMETHING WENT WRONG AAH", ephemeral: true);
			return;
		}

		var (embed, components) = result.Value;

		await component.UpdateAsync(msg =>
		{
			msg.Embed = embed;
			msg.Components = components;
		});
	}

	private async Task ButtonHandler(SocketMessageComponent component)
	{
		//Console.Out.WriteLine(component.User.GlobalName + ": " + component.Data.CustomId);
		_logger.LogInformation($"{component.User.GlobalName}: {component.Data.CustomId}");
		// Inv nav
		if (component.Data.CustomId.StartsWith("inventory_page_") ||
		component.Data.CustomId.StartsWith("inventory_filtered_"))
		{
			await HandleInventoryPageNavigation(component);
		}
		else if (component.Data.CustomId.StartsWith("shop_page_") ||
		 component.Data.CustomId.StartsWith("shop_filtered_"))
		{
			await HandleShopPageNavigation(component);
		}
		else if (component.Data.CustomId.StartsWith("openbuy_"))
		{
			await HandleShopBuyMenuOpenButton(component);
		}
		else if (component.Data.CustomId.StartsWith("opensell_"))
		{
			await HandleShopSellMenuOpenButton(component);
		}
		else if (component.Data.CustomId.StartsWith("transaction_"))
		{
			await HandleTransactionButton(component);
		}
	}

	private async Task HandleTransactionButton(SocketMessageComponent component)
	{
		// transaction_{string:type}_{giverDiscordID}_{takerDiscordID}_{itemID}_{quanity}
		var parts = component.Data.CustomId.Split('_');

		string type = parts[1];
		string giverDiscordID = parts[2];
		string takerDiscordID = parts[3];
		int itemID = Int32.Parse(parts[4]);
		int quantity = Int32.Parse(parts[5]);

		string message = $"Type: {type}\n" +
				 $"Giver Discord ID: {_client.GetUser(ulong.Parse(giverDiscordID))}\n" +
				 $"Taker Discord ID: {_client.GetUser(ulong.Parse(takerDiscordID))}\n" +
				 $"Item ID: {itemID}\n" +
				 $"Quantity: {quantity}";


		ShopService.ShopResult result = await ShopManager.ExecuteTransaction(_shopService, type, giverDiscordID, takerDiscordID, itemID, quantity);

		await component.RespondAsync(message + "\n" + result, ephemeral: true);

	}


	private async Task HandleShopSellMenuOpenButton(SocketMessageComponent component)
	{
		var parts = component.Data.CustomId.Split('_');
		// opensell_{sellerDiscordID}_{itemLedgerID}_{amountSelected}_{filter}. --- Buyer if determined ONLY on press interact

		if (parts.Length < 4)
		{
			await component.RespondAsync("error in custom ID.", ephemeral: true);
			return;
		}

		string buyerDiscordId = parts[1];
		string itemId = parts[2];
		string sellerDiscordId = component.User.Id.ToString();// This is who interacted with the button
		int startingAmount = int.Parse(parts[3]);

		// Break down filter. 
		string filterString = parts.Length >= 5 ? parts[4] : "";
		string[] filterArray = string.IsNullOrWhiteSpace(filterString)
			? new string[0]
			: filterString.Split('-');

		var buyer = _playerService.GetByDiscordId(buyerDiscordId);
		var playerSelling = _profileService.GetProfileByDiscordId(sellerDiscordId);

		if (playerSelling == null)
		{
			await component.RespondAsync("Shop not found.", ephemeral: true);
			return;
		}

		var selectedItem = _inventoryService.GetInventoryItemStack(Int32.Parse(itemId), ulong.Parse(buyerDiscordId));

		var result = ShopManager.Instance.BuildSellInteract(_client, buyer, selectedItem, playerSelling, filterArray, startingAmount);

		if (result == null)
		{
			await component.RespondAsync("No matching items.---SOMETHING WENT WRONG AAH", ephemeral: true);
			return;
		}

		var (embed, components) = result.Value;
		await component.RespondAsync(embed: embed, components: components, ephemeral: true);

		/*var (embed, components) = result.Value;

		await component.UpdateAsync(msg =>
		{
			msg.Embed = embed;
			msg.Components = components;
		});*/
	}

	private async Task HandleShopBuyMenuOpenButton(SocketMessageComponent component)
	{
		var parts = component.Data.CustomId.Split('_');
		// openbuy_{sellerDiscordID}_{itemLedgerID}_{amountSelected}_{filter}. --- Buyer if determined ONLY on press interact
		if (parts.Length < 4)
		{
			await component.RespondAsync("error in custom ID.", ephemeral: true);
			return;
		}

		string sellerDiscordId = parts[1];
		string itemId = parts[2];// db ledger reference
		int startingAmount = int.Parse(parts[3]);

		// Break down filter. 
		string filterString = parts.Length >= 5 ? parts[4] : "";
		string[] filterArray = string.IsNullOrWhiteSpace(filterString)
			? new string[0]
			: filterString.Split('-');

		string buyerDiscordId = component.User.Id.ToString();// This is who interacted with the button

		var player = _playerService.GetByDiscordId(buyerDiscordId);
		var shop = _profileService.GetProfileByDiscordId(sellerDiscordId);

		if (shop == null)
		{
			await component.RespondAsync("Shop not found.", ephemeral: true);
			return;
		}

		var shopItem = _inventoryService.GetInventoryItemStack(Int32.Parse(itemId), ulong.Parse(sellerDiscordId));

		var result = ShopManager.Instance.BuildBuyInteract(_client, player, shopItem, shop, filterArray, startingAmount);

		if (result == null)
		{
			await component.RespondAsync("No matching items.---SOMETHING WENT WRONG AAH", ephemeral: true);
			return;
		}

		var (embed, components) = result.Value;
		await component.RespondAsync(embed: embed, components: components, ephemeral: true);

		return;
	}

	private async Task HandleInventoryPageNavigation(SocketMessageComponent component)
	{
		var parts = component.Data.CustomId.Split('_');
		// For filtered inventory, expected format:
		//   inventory_filtered_{filter}_{detailed/compact}_{userId}_{pageIndex}
		// For non-filtered:
		//   inventory_page_{detailed/compact}_{userId}_{pageIndex}
		if (parts.Length < 5) return;

		bool isFiltered = parts[1].Equals("filtered", StringComparison.OrdinalIgnoreCase);
		string? filter = null;
		bool detailed = false;
		ulong userId;
		int pageIndex;

		if (isFiltered)
		{
			if (parts.Length < 6) return;
			filter = parts[2];
			detailed = parts[3].Equals("detailed", StringComparison.OrdinalIgnoreCase);
			if (!ulong.TryParse(parts[4], out userId)) return;
			if (!int.TryParse(parts[5], out pageIndex)) return;
		}
		else
		{
			detailed = parts[2].Equals("detailed", StringComparison.OrdinalIgnoreCase);
			if (!ulong.TryParse(parts[3], out userId)) return;
			if (!int.TryParse(parts[4], out pageIndex)) return;
		}

		var user = _client.GetUser(userId);
		if (user == null) return;

		var result = InventoryManager.Instance.BuildInventoryPage(
			user,
			pageIndex,
			_profileService,
			_playerService,
			detailed,
			filter
		);

		if (result == null)
		{
			await component.RespondAsync("No matching items.", ephemeral: true);
			return;
		}

		var (embed, components) = result.Value;

		await component.UpdateAsync(msg =>
		{
			msg.Embed = embed;
			msg.Components = components;
		});
	}

	private async Task HandleShopPageNavigation(SocketMessageComponent component)
	{
		var parts = component.Data.CustomId.Split('_');
		// For filtered shop view, expected format:
		//   shop_filtered_{filter}_{detailed/compact}_{userId}_{pageIndex}
		// For non-filtered shop view:
		//   shop_page_{detailed/compact}_{userId}_{pageIndex}
		if (parts.Length < 5) return;

		bool isFiltered = parts[1].Equals("filtered", StringComparison.OrdinalIgnoreCase);
		string? filter = null;
		bool detailed = false;
		ulong userId;
		int pageIndex;

		if (isFiltered)
		{
			if (parts.Length < 6) return;
			filter = parts[2];
			detailed = parts[3].Equals("detailed", StringComparison.OrdinalIgnoreCase);
			if (!ulong.TryParse(parts[4], out userId)) return;
			if (!int.TryParse(parts[5], out pageIndex)) return;
		}
		else
		{
			detailed = parts[2].Equals("detailed", StringComparison.OrdinalIgnoreCase);
			if (!ulong.TryParse(parts[3], out userId)) return;
			if (!int.TryParse(parts[4], out pageIndex)) return;
		}

		var user = _client.GetUser(userId);
		if (user == null) return;

		var result = ShopManager.Instance.BuildShopPage(
			user,
			pageIndex,
			_profileService,
			_playerService,
			detailed,
			filter
		);

		if (result == null)
		{
			await component.RespondAsync("No matching items.", ephemeral: true);
			return;
		}

		var (embed, components) = result.Value;

		await component.UpdateAsync(msg =>
		{
			msg.Embed = embed;
			msg.Components = components;
		});
	}


	// ai response
	private async Task<String> HandleMention(SocketMessage message)
	{
		if (message.Author.IsBot) return "";

		var channel = message.Channel;
		var cancellationTokenSource = new CancellationTokenSource();
		var token = cancellationTokenSource.Token;

		// Start typing in a separate task
		var typingTask = Task.Run(async () =>
		{
			try
			{
				while (!token.IsCancellationRequested)
				{
					await channel.TriggerTypingAsync();
					await Task.Delay(1000, token);
				}
			}
			catch (TaskCanceledException)
			{
				// Do nothing
			}
		}, token);


		var response = await _openAiService.GenerateResponse(_client, message);

		await SendResponseInChunks(channel, _openAiService.SplitResponse(response), message);

		// Signal the cancellation and wait for the task to complete
		cancellationTokenSource.Cancel();
		await typingTask;
		cancellationTokenSource.Dispose();

		return response;
	}

	// originalMessage determines if reply or not
	/*private async Task SendResponseInChunks(ISocketMessageChannel channel, string response, SocketMessage originalMessage = null)
	{
		const int chunkSize = 2000; // Discord's max message length
		for(int i = 0; i < response.Length; i += chunkSize) {
			var chunk = response.Substring(i, Math.Min(chunkSize, response.Length - i));

            if(originalMessage != null) {
				await (originalMessage as IUserMessage).ReplyAsync(chunk, allowedMentions: new AllowedMentions(AllowedMentionTypes.None));
			}
            else {
			    await channel.SendMessageAsync(chunk);
            }
		}
	}*/

	private async Task SendResponseInChunks(ISocketMessageChannel channel, string[] chunks, SocketMessage originalMessage = null)
	{
		foreach (string chunk in chunks)
		{

			String modchunk = "> " + chunk.Replace("Xolobot: ", "").Replace("Xolobob: ", "").Replace("\n", "\n> ");

			if (originalMessage != null)
			{
				await (originalMessage as IUserMessage).ReplyAsync(modchunk, allowedMentions: new AllowedMentions(AllowedMentionTypes.None));
			}
			else
			{
				await channel.SendMessageAsync(modchunk);
			}
		}
	}

	private async Task HandleInteraction(SocketInteraction interaction)
	{
		try
		{
			if (interaction is SocketSlashCommand)
			{
				var context = new SocketInteractionContext(_client, interaction);

				var result = await _interactionService.ExecuteCommandAsync(context, _services);

				if (!result.IsSuccess)
					_ = Task.Run(() => HandleInteractionExecutionResult(interaction, result));// logs output


			}
			else if (interaction is SocketMessageComponent messageComponent)
			{
				//_logger.LogInformation("Mention test");
				await Console.Out.WriteLineAsync("Mention TEst");
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, ex.Message);
		}
	}

	private Task HandleInteractionExecuted(ICommandInfo command, IInteractionContext context, IResult result)
	{
		if (!result.IsSuccess)
			_ = Task.Run(() => HandleInteractionExecutionResult(context.Interaction, result));
		return Task.CompletedTask;
	}

	private async Task HandleInteractionExecutionResult(IDiscordInteraction interaction, IResult result)
	{
		switch (result.Error)
		{
			case InteractionCommandError.UnmetPrecondition:
				_logger.LogInformation($"Unmet precondition - {result.Error}");
				break;

			case InteractionCommandError.BadArgs:
				_logger.LogInformation($"Unmet precondition - {result.Error}");
				break;

			case InteractionCommandError.ConvertFailed:
				_logger.LogInformation($"Convert Failed - {result.Error}");
				break;

			case InteractionCommandError.Exception:
				_logger.LogInformation($"Exception - {result.Error}");
				break;

			case InteractionCommandError.ParseFailed:
				_logger.LogInformation($"Parse Failed - {result.Error}");
				break;

			case InteractionCommandError.UnknownCommand:
				_logger.LogInformation($"Unknown Command - {result.Error}");
				break;

			case InteractionCommandError.Unsuccessful:
				_logger.LogInformation($"Unsuccessful - {result.Error}");
				break;
		}

		if (!interaction.HasResponded)
		{
			await interaction.RespondAsync("An error has occurred. We are already investigating it!", ephemeral: true);
		}
		else
		{
			await interaction.FollowupAsync("An error has occurred. We are already investigating it!", ephemeral: true);
		}
	}
}