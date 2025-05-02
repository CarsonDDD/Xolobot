using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Hackathon.Managers;
using Hackathon.Managers.Inventory;
using Hackathon.Services;
using Microsoft.Extensions.Logging;

namespace Hackathon.Modules;

[Group("player", "Commands assosiated with YOU!")]
public class PlayerModule(
    ILogger<ModuleBase> logger,
    DatabaseService sqliteDbService,
    PlayerService playerService,
    PlayerProfileService profileService,
    OpenAIService openAIService,
    DiscordSocketClient client,
    InteractionHandler interaction
)
    : ModuleBase(
        logger,
        sqliteDbService,
        playerService,
        profileService,
        openAIService,
        client,
        interaction
    )
{
    [SlashCommand("profile", "Show your profile")]
    public async Task GetProfile(IUser? other = null)
    {
        await DeferAsync(ephemeral: true);

        var embed = PlayerProfileManager.Instance.BuildProfileEmbed(
            other ?? Context.User, // other == null ? Context.User: other. This syntax is very cool
            _profileService,
            _playerService,
            other == null || Context.Guild.GetUser(Context.User.Id).GuildPermissions.ManageGuild
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

        var player = target ?? Context.User;

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
