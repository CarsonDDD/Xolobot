using Discord;
using Hackathon.Services;

namespace Hackathon.Managers;

public class PlayerProfileManager
{
    private static PlayerProfileManager _instance;
    private PlayerProfileManager() { }
    public static PlayerProfileManager Instance => _instance ??= new PlayerProfileManager();

    public Embed? BuildProfileEmbed(
        IUser user,
        PlayerProfileService profileService,
        PlayerService playerService)
    {
        var player = playerService.GetByDiscordId(user.Id.ToString());
        if (player == null) return null;

        var profile = profileService.GetProfile(player.Id);
        if (profile == null) return null;

        var embed = new EmbedBuilder()
            .WithAuthor(user)
            .WithTitle($"{profile.Player.Name}'s Profile")
            .WithThumbnailUrl(profile.Player.ImgUrl)
            .AddField("Gold", profile.Player.Gold.ToString(), true)
            .AddField("Classes", string.Join(", ", profile.Classes.Select(c => c.Label)), true)
            .AddField("Races", string.Join(", ", profile.Races.Select(r => r.Label)), true)
            .AddField("Languages", string.Join(", ", profile.Languages.Select(l => l.Label)))
            .AddField("Proficiencies", string.Join(", ", profile.Proficiencies.Select(p => p.Label)))
            .AddField("Stats", string.Join("\n", profile.Stats.Select(s => $"{s.Label}: {s.Value}")))
            .WithColor(Color.Blue);

        return embed.Build();
    }
}
