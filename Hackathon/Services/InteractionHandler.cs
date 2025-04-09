using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Hackathon.Managers.Inventory;
using Hackathon.Managers.Shop;
using Microsoft.Extensions.Logging;
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

	public delegate void BotResponseEvent(object sender, BotResponseArgs e);
	public event BotResponseEvent? OnPostBotMention;

	public InteractionHandler(DiscordSocketClient client, InteractionService interactionService, IServiceProvider services, ILogger<InteractionHandler> logger, OpenAIService openAiService, DatabaseService dbService, PlayerService playerService, PlayerProfileService profileService)
	{
		_interactionService = interactionService;
		_client = client;
		_services = services;
		_logger = logger;
		_openAiService = openAiService;
		_database = dbService;
		_playerService = playerService;
		_profileService = profileService;

		// events
		_client.ButtonExecuted += ButtonHandler;
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

	private async Task ButtonHandler(SocketMessageComponent component)
	{
		Console.Out.WriteLine(component.User.GlobalName + ": " + component.Data.CustomId);
		// Inv nav
		if (component.Data.CustomId.StartsWith("inventory_page_") ||
		component.Data.CustomId.StartsWith("inventory_filtered_"))
		{
			await HandleInventoryPageNavigation(component);
		}
		else if (component.Data.CustomId.StartsWith("shop_page_") ||
		 component.Data.CustomId.StartsWith("shop_filtered_"))
		{

			if (component.Data.CustomId.EndsWith("buy"))
			{
				await HandleShopBuyStartButton(component);
			}
			else
			{
				// nav
				await HandleShopPageNavigation(component);
			}

		}
	}

	private async Task HandleShopBuyStartButton(SocketMessageComponent component)
	{
		await component.RespondAsync("Buy button pressed", ephemeral: true);
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