using Discord;
using Discord.Interactions;
using Discord.WebSocket;
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

	public delegate void BotResponseEvent(object sender, BotResponseArgs e);
	public event BotResponseEvent? OnPostBotMention;

	public InteractionHandler(DiscordSocketClient client, InteractionService interactionService, IServiceProvider services, ILogger<InteractionHandler> logger, OpenAIService openAiService, DatabaseService dbService)
	{
		_interactionService = interactionService;
		_client = client;
		_services = services;
		_logger = logger;
		_openAiService = openAiService;
		_database = dbService;

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
		// Shop nav
		if (component.Data.CustomId.Contains("shop_page_"))
		{
			await HandleShopNavigation(component);
		}
		else if (component.Data.CustomId.Contains("item_page_"))
		{
			await HandleItemNavigation(component);
		}
		else if (component.Data.CustomId.Contains("shop_buy_"))
		{
			await HandleBuyItem(component);
		}
	}

	private async Task HandleBuyItem(SocketMessageComponent component)
	{
		string[] parts = component.Data.CustomId.Split('_');
		if (parts.Length < 4) return;
		//if(!int.TryParse(parts[3], out int page)) return;
		// 2 is item name
		string itemName = parts[2];
		// 3 is discord id

		//await component.RespondAsync($@"<@{component.User.Id}> attempted to buy item: {itemName}");

		//ShopManager.Instance.BuyItem(component, itemName, _database);

		//await component.DeferAsync();// stops crashing?
	}

	// the custom id is going to be formatted as : `shop_page_<searchterm>_#`
	private async Task HandleShopNavigation(SocketMessageComponent component)
	{
		string[] parts = component.Data.CustomId.Split('_');
		if (parts.Length < 3) return;
		if (!int.TryParse(parts[2], out int page)) return;

		//var items = await _database.GetShopItems();

		// modify shop menu with new page
		//await ShopManager.Instance.ShowShopPage(component.Channel, page, items, (IUserMessage)component.Message);

		await component.DeferAsync();// stops crashing?
	}

	private async Task HandleItemNavigation(SocketMessageComponent component)
	{
		// 2 is search term
		// 3 is page
		string[] parts = component.Data.CustomId.Split('_');
		if (parts.Length < 4) return;
		if (!int.TryParse(parts[3], out int page)) return;

		String searchTerm = parts[2];

		//var items = await _database.GetShopItems();

		// modify shop menu with new page
		//await ShopManager.Instance.ShowItemPage(component.Channel, searchTerm, page, items, component.User, (IUserMessage)component.Message);

		await component.DeferAsync();// stops crashing?
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