using System.Configuration;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Hackathon.Entities;
using Hackathon.Managers.Inventory;
using Hackathon.Managers.Shop;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

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
    private readonly ItemService _itemService;
    private readonly ShopService _shopService;

    public delegate void BotResponseEvent(object sender, BotResponseArgs e);
    public event BotResponseEvent? OnPostBotMention;

    public InteractionHandler(
        DiscordSocketClient client,
        InteractionService interactionService,
        IServiceProvider services,
        ILogger<InteractionHandler> logger,
        OpenAIService openAiService,
        DatabaseService dbService,
        PlayerService playerService,
        PlayerProfileService profileService,
        InventoryService inventoryService,
        ShopService shopService,
        ItemService itemService
    )
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
        _itemService = itemService;

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
        if (
            message is SocketUserMessage userMessage
            && userMessage.MentionedUsers.Any(user => user.Id == _client.CurrentUser.Id)
        )
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

        if (
            component.Data.CustomId.StartsWith("buymenu_quantselector")
            || component.Data.CustomId.StartsWith("buymenu_itemselector")
        )
        {
            await HandleUpdateBuyMenuAmount(component, component.Data.Values);
        }
        else if (
            component.Data.CustomId.StartsWith("sellmenu_quantselector")
            || component.Data.CustomId.StartsWith("sellmenu_itemselector")
        )
        {
            await HandleUpdateSellMenuAmount(component, component.Data.Values);
        }
        else if (component.Data.CustomId.StartsWith("deletemenu_quantselector"))
        {
            await HandleUpdateDeleteMenuAmount(component, component.Data.Values);
        }
    }

    private async Task HandleUpdateDeleteMenuAmount(
        SocketMessageComponent component,
        IReadOnlyCollection<string> values
    )
    {
        var parts = values.First().Split('_');
        // opendelete_{inventoryItemID}_{amount}
        /*if (parts.Length < 4)
        {
            await component.RespondAsync("error in custom ID.", ephemeral: true);
            return;
        }*/

        int itemId = int.Parse(parts[1]); // db ledger reference
        int amount = int.Parse(parts[2]);

        InventoryItem item = _inventoryService.GetInventoryItem(itemId);

        var result = InventoryManager.Instance.BuildDeleteMenu(item, amount);

        if (result == null)
        {
            await component.RespondAsync(
                "No matching items.---SOMETHING WENT WRONG AAH",
                ephemeral: true
            );
            return;
        }

        var (embed, components) = result.Value;

        await component.UpdateAsync(msg =>
        {
            msg.Embed = embed;
            msg.Components = components;
        });
    }

    private async Task HandleUpdateSellMenuAmount(
        SocketMessageComponent component,
        IReadOnlyCollection<string> values
    )
    {
        var parts = values.First().Split('_');
        // openbuy_{sellerDiscordID}_{itemLedgerID}_{amountSelected}_{filter}. --- Buyer if determined ONLY on press interact

        if (parts.Length < 4)
        {
            await component.RespondAsync("error in custom ID.", ephemeral: true);
            return;
        }

        string buyerDiscordId = ShopManager.SHOP_DISCORD_ID.ToString(); //parts[1];
        string itemId = parts[2];
        string sellerDiscordId = component.User.Id.ToString(); // This is who interacted with the button
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

        var selectedItem = _inventoryService.GetInventoryItemStack(
            Int32.Parse(itemId),
            ulong.Parse(sellerDiscordId)
        );

        var result = ShopManager.Instance.BuildSellInteract(
            _client,
            buyer,
            selectedItem,
            playerSelling,
            filterArray,
            startingAmount
        );

        if (result == null)
        {
            await component.RespondAsync(
                "No matching items.---SOMETHING WENT WRONG AAH",
                ephemeral: true
            );
            return;
        }

        var (embed, components) = result.Value;

        await component.UpdateAsync(msg =>
        {
            msg.Embed = embed;
            msg.Components = components;
        });
    }

    private async Task HandleUpdateBuyMenuAmount(
        SocketMessageComponent component,
        IReadOnlyCollection<string> values
    )
    {
        var parts = values.First().Split('_');
        // openbuy_{sellerDiscordID}_{itemLedgerID}_{amountSelected}_{filter}. --- Buyer if determined ONLY on press interact

        if (parts.Length < 4)
        {
            await component.RespondAsync("error in custom ID.", ephemeral: true);
            return;
        }

        string sellerDiscordId = parts[1];
        string itemId = parts[2]; // define what I actuall mean by this.
        string buyerDiscordId = component.User.Id.ToString(); // This is who interacted with the button
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

        var shopItem = _inventoryService.GetInventoryItemStack(
            Int32.Parse(itemId),
            ulong.Parse(sellerDiscordId)
        );

        var result = ShopManager.Instance.BuildBuyInteract(
            _client,
            player,
            shopItem,
            shop,
            filterArray,
            startingAmount
        );

        if (result == null)
        {
            await component.RespondAsync(
                "No matching items.---SOMETHING WENT WRONG AAH",
                ephemeral: true
            );
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
        if (component.Data.CustomId.StartsWith("inventory"))
        {
            await HandleInventoryPageNavigation(component);
        }
        else if (component.Data.CustomId.StartsWith("shop"))
        {
            await HandleShopPageNavigation(component);
        }
        else if (component.Data.CustomId.StartsWith("openbuy_"))
        {
            //sub menu
            await HandleShopBuyMenuOpenButton(component);
        }
        else if (component.Data.CustomId.StartsWith("opensell_"))
        {
            //sub menu
            await HandleShopSellMenuOpenButton(component);
        }
        else if (component.Data.CustomId.StartsWith("transaction_"))
        {
            await HandleTransactionButton(component);
        }
        else if (component.Data.CustomId.StartsWith("opendelete_"))
        {
            //sub menu
            await HandleOpenDelete(component);
        }
        else if (component.Data.CustomId.StartsWith("delete_"))
        {
            await HandleDelete(component);
        }
    }

    private async Task HandleOpenDelete(SocketMessageComponent component)
    {
        // opendelete_{inventoryItemID}_{amount}
        var parts = component.Data.CustomId.Split('_');
        /*if (parts.Length < 4)
        {
            await component.RespondAsync("error in custom ID.", ephemeral: true);
            return;
        }*/

        int itemId = int.Parse(parts[1]); // db ledger reference
        int amount = int.Parse(parts[2]);

        InventoryItem item = _inventoryService.GetInventoryItem(itemId);
        // Get in-mem profile to create display---only in the actual delete we only care about DB operations.
        // However, in the content we actually want to display, do we actually care/need the player infor to make this display?
        // yes, we need the amount.

        /*
        What we need:
        - ItemStack(DOM)/InventoryItem(DB): Item amount (selector)
        - String: PlayerID (to call the delete function) (essentially hidden) (Database ID)
        - ItemStack(DOM)/Item(DB) Item info for display
        */

        var result = InventoryManager.Instance.BuildDeleteMenu(item, amount);

        if (result == null)
        {
            await component.RespondAsync(
                "No matching items.---SOMETHING WENT WRONG AAH",
                ephemeral: true
            );
            return;
        }

        var (embed, components) = result.Value;
        await component.RespondAsync(embed: embed, components: components, ephemeral: true);
        //await component.RespondAsync("You are tryna delete something", ephemeral: true);

        return;
    }

    private async Task HandleDelete(SocketMessageComponent component)
    {
        // delete_{InventoryItemID}_{startingAmount}_{delta}
        var parts = component.Data.CustomId.Split('_');
        int itemId = int.Parse(parts[1]); // db ledger reference
        int startingAmount = int.Parse(parts[2]);
        int delta = int.Parse(parts[3]);

        var result = _inventoryService.ChangeInventoryItemQuantity(itemId, -delta);
        int? newAmount = result.newAmount;

        if (newAmount == null)
        {
            // ERROR
        }

        //await component.RespondAsync(msg, ephemeral: true);
        await component.UpdateAsync(msg =>
        {
            msg.Content = $"New amount: {newAmount.Value}";
            // Dont remove the delete components if there is still items left
            // We should KEEP the delete features, if the player has more items AND if they chose the max amount. If you have lots of items you will do this

            if (!(startingAmount > 25 && delta == 25))
            {
                msg.Embed = null;
                msg.Components = null;
            }
            else
            {
                // Additions to message content here.
                InventoryItem item = _inventoryService.GetInventoryItem(itemId);
                var comps = InventoryManager.Instance.BuildDeleteMenu(item, 0);
                msg.Components = comps.Value.component;
            }
        });
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

        // Break down filter.
        string filterString = parts.Length >= 6 ? parts[6] : "";
        string[] filterArray = string.IsNullOrWhiteSpace(filterString)
            ? new string[0]
            : filterString.Split('-');

        string message =
            $"Type: {type}\n"
            + $"Giver Discord ID: {_client.GetUser(ulong.Parse(giverDiscordID))}\n"
            + $"Taker Discord ID: {_client.GetUser(ulong.Parse(takerDiscordID))}\n"
            + $"Item ID: {itemID}\n"
            + $"Quantity: {quantity}";

        ShopService.ShopResult result = await ShopManager.ExecuteTransaction(
            _shopService,
            type,
            giverDiscordID,
            takerDiscordID,
            itemID,
            quantity
        );

        if (result == ShopService.ShopResult.Success)
        {
            // Update component
            var buyer = _playerService.GetByDiscordId(takerDiscordID);
            var seller = _profileService.GetProfileByDiscordId(giverDiscordID);
            var selectedItem = _inventoryService.GetInventoryItemStack(
                itemID,
                ulong.Parse(giverDiscordID)
            );
            (Embed, MessageComponent)? updatedComp = null;

            if (type == "buy")
            {
                updatedComp = ShopManager.Instance.BuildBuyInteract(
                    _client,
                    buyer,
                    selectedItem,
                    seller,
                    filterArray,
                    0
                );
            }
            else if (type == "sell")
            {
                updatedComp = ShopManager.Instance.BuildSellInteract(
                    _client,
                    buyer,
                    selectedItem,
                    seller,
                    filterArray,
                    0
                );
            }

            if (updatedComp == null)
            {
                await component.RespondAsync(
                    "No matching items.---SOMETHING WENT WRONG AAH",
                    ephemeral: true
                );
                return;
            }
            var (embed, components) = updatedComp.Value;
            //await component.RespondAsync(embed: embed, components: components, ephemeral: true);

            await component.UpdateAsync(msg =>
            {
                msg.Content = "Items changed!";
                msg.Components = null;
                msg.Embed = null;
                // Update existing component instead of using respond.
            });
        }

        //await component.RespondAsync(message + "\n" + result, ephemeral: true);
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
        string sellerDiscordId = component.User.Id.ToString(); // This is who interacted with the button
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

        var selectedItem = _inventoryService.GetInventoryItemStack(
            Int32.Parse(itemId),
            ulong.Parse(playerSelling.Player.DiscordId.ToString())
        );
        if (selectedItem == null)
            System.Console.WriteLine("Selected item in Interaction Handler is null");

        var result = ShopManager.Instance.BuildSellInteract(
            _client,
            buyer,
            selectedItem,
            playerSelling,
            filterArray,
            startingAmount
        );

        if (result == null)
        {
            await component.RespondAsync(
                "No matching items.---SOMETHING WENT WRONG AAH",
                ephemeral: true
            );
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
        string itemId = parts[2]; // db ledger reference
        int startingAmount = int.Parse(parts[3]);

        // Break down filter.
        string filterString = parts.Length >= 5 ? parts[4] : "";
        string[] filterArray = string.IsNullOrWhiteSpace(filterString)
            ? new string[0]
            : filterString.Split('-');

        string buyerDiscordId = component.User.Id.ToString(); // This is who interacted with the button

        var player = _playerService.GetByDiscordId(buyerDiscordId);
        var shop = _profileService.GetProfileByDiscordId(sellerDiscordId);

        if (shop == null)
        {
            await component.RespondAsync("Shop not found.", ephemeral: true);
            return;
        }

        var shopItem = _inventoryService.GetInventoryItemStack(
            Int32.Parse(itemId),
            ulong.Parse(sellerDiscordId)
        );

        var result = ShopManager.Instance.BuildBuyInteract(
            _client,
            player,
            shopItem,
            shop,
            filterArray,
            startingAmount
        );

        if (result == null)
        {
            await component.RespondAsync(
                "No matching items.---SOMETHING WENT WRONG AAH",
                ephemeral: true
            );
            return;
        }

        var (embed, components) = result.Value;
        await component.RespondAsync(embed: embed, components: components, ephemeral: true);

        return;
    }

    private async Task HandleInventoryPageNavigation(SocketMessageComponent component)
    {
        // inventory_{isDetailed}_{DiscordID}_{destinationPage}_{filter}
        var parts = component.Data.CustomId.Split(
            new[] { '_' },
            StringSplitOptions.RemoveEmptyEntries
        );

        bool isDetailed = bool.Parse(parts[1]);
        ulong discordId = ulong.Parse(parts[2]);
        int destinationPageIndex = int.Parse(parts[3]);
        string filter = parts.Length >= 5 ? parts[4] : "";
        var user = await GetGuildUserAsync(component, discordId);

        var result = InventoryManager.Instance.BuildInventoryPage(
            user,
            destinationPageIndex,
            _profileService,
            _playerService,
            isDetailed,
            filter
        );

        if (result == null)
        {
            await component.RespondAsync(
                "No matching items. This should never be called",
                ephemeral: true
            );
            return;
        }

        var (embed, components) = result.Value;

        await component.UpdateAsync(msg =>
        {
            msg.Embed = embed;
            msg.Components = components;
        });
    }

    private async Task<IUser> GetGuildUserAsync(SocketMessageComponent component, ulong userId)
    {
        var guildChannel = component.Channel as SocketGuildChannel;

        if (guildChannel != null)
        {
            IGuild iGuild = guildChannel.Guild;

            if (iGuild != null)
            {
                var guildUser = await iGuild.GetUserAsync(userId);
                if (guildUser != null)
                    return guildUser;
            }
        }

        IUser cachedUser = _client.GetUser(userId);
        if (cachedUser != null)
            return cachedUser;

        return await _client.Rest.GetUserAsync(userId);
    }

    private async Task HandleShopPageNavigation(SocketMessageComponent component)
    {
        // shop_{isDetailed}_{DiscordID}_{destinationPage}_{filter}
        var parts = component.Data.CustomId.Split(
            new[] { '_' },
            StringSplitOptions.RemoveEmptyEntries
        );

        bool isDetailed = bool.Parse(parts[1]);
        ulong discordId = ulong.Parse(parts[2]);
        int destinationPageIndex = int.Parse(parts[3]);
        string filter = parts.Length >= 5 ? parts[4] : "";
        var user = await GetGuildUserAsync(component, discordId);

        var result = ShopManager.Instance.BuildShopPage(
            user,
            destinationPageIndex,
            _profileService,
            _playerService,
            isDetailed,
            filter
        );

        if (result == null)
        {
            await component.RespondAsync(
                "No matching items. This should never be called",
                ephemeral: true
            );
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
        if (message.Author.IsBot)
            return "";

        var channel = message.Channel;
        var cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;

        // Start typing in a separate task
        var typingTask = Task.Run(
            async () =>
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
            },
            token
        );

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

    private async Task SendResponseInChunks(
        ISocketMessageChannel channel,
        string[] chunks,
        SocketMessage originalMessage = null
    )
    {
        foreach (string chunk in chunks)
        {
            String modchunk =
                "> "
                + chunk.Replace("Xolobot: ", "").Replace("Xolobob: ", "").Replace("\n", "\n> ");

            if (originalMessage != null)
            {
                await (originalMessage as IUserMessage).ReplyAsync(
                    modchunk,
                    allowedMentions: new AllowedMentions(AllowedMentionTypes.None)
                );
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
                    _ = Task.Run(() => HandleInteractionExecutionResult(interaction, result)); // logs output
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

    private Task HandleInteractionExecuted(
        ICommandInfo command,
        IInteractionContext context,
        IResult result
    )
    {
        if (!result.IsSuccess)
            _ = Task.Run(() => HandleInteractionExecutionResult(context.Interaction, result));
        return Task.CompletedTask;
    }

    private async Task HandleInteractionExecutionResult(
        IDiscordInteraction interaction,
        IResult result
    )
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
            await interaction.RespondAsync(
                "An error has occurred. We are already investigating it!",
                ephemeral: true
            );
        }
        else
        {
            await interaction.FollowupAsync(
                "An error has occurred. We are already investigating it!",
                ephemeral: true
            );
        }
    }
}
