using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Hackathon.Managers;
using Hackathon.Managers.Inventory;
using Hackathon.Services;
using Microsoft.Extensions.Logging;

namespace Hackathon.Modules;

[Group("player", "Commands assosiated with YOU!")]
public class PlayerModule : ModuleBase
{
    public PlayerModule(
        ILogger<ModuleBase> logger,
        DatabaseService sqliteDbService,
        PlayerService playerService,
        PlayerProfileService profileService,
        OpenAIService openAIService,
        DiscordSocketClient client,
        InteractionHandler interaction
    )
        : base(
            logger,
            sqliteDbService,
            playerService,
            profileService,
            openAIService,
            client,
            interaction
        ) { }

    [SlashCommand("profile", "Show your profile")]
    public async Task GetProfile()
    {
        await DeferAsync(ephemeral: true);

        var embed = PlayerProfileManager.Instance.BuildProfileEmbed(
            Context.User,
            _profileService,
            _playerService
        );

        if (embed == null)
        {
            await FollowupAsync("You are not registered.", ephemeral: true);
            return;
        }

        await ModifyOriginalResponseAsync(msg => msg.Embed = embed);
    }

    [DefaultMemberPermissions(GuildPermission.Administrator)]
    [SlashCommand("inventory", "Display your inventory")]
    public async Task GetInventoryBasic(bool asList = false, string? filter = null)
    {
        await GetInventoryAdmin(asList, filter, null);
    }

    [DefaultMemberPermissions(GuildPermission.Administrator)]
    [SlashCommand("peak", "Look into any inventory")]
    public async Task GetInventoryAdmin(
        bool asList = false,
        string? filter = null,
        IUser? target = null
    )
    {
        await DeferAsync(ephemeral: true); // can be either

        var player = target == null ? Context.User : target;

        string filterParam = !string.IsNullOrWhiteSpace(filter) ? filter : "";

        var result = asList
            ? InventoryManager.Instance.BuildInventoryList(
                player,
                filterParam,
                _profileService,
                _playerService
            )
            : InventoryManager.Instance.BuildInventoryPage(
                player,
                pageIndex: 0,
                _profileService,
                _playerService,
                true,
                filterParam
            );

        if (result == null)
        {
            await FollowupAsync(
                "You have no inventory or nothing matches the search.",
                ephemeral: true
            );
            return;
        }

        var (embed, components) = result.Value;

        await ModifyOriginalResponseAsync(msg =>
        {
            msg.Embed = embed;
            msg.Components = components;
        });
    }
}
