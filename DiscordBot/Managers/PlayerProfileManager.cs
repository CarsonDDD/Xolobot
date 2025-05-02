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
        PlayerService playerService,
        bool showGold
    )
    {
        var player = playerService.GetByDiscordId(user.Id.ToString());
        if (player == null)
            return null;

        var profile = profileService.GetProfile(player.Id);
        if (profile == null)
            return null;

        string discordName = ((user as IGuildUser)?.Nickname ?? user.Username) + "";
        var embed = new EmbedBuilder()
            .WithAuthor(author =>
            {
                author.IconUrl = user.GetAvatarUrl();
                author.Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
                author.Name = discordName;
            })
            .WithTitle($"{profile.Player.Name}'s Profile")
            .WithThumbnailUrl(profile.Player.ImgUrl)
            .AddField("Gold", showGold ? profile.Player.Gold.ToString(): "Unknown", true)
            .AddField(
                "Classes",
                profile.Classes.Count != 0
                    ? string.Join(", ", profile.Classes.Select(c => c.Label))
                    : "Nothing",
                true
            )
            .AddField("Races", string.Join(", ", profile.Races.Select(r => r.Label)), true)
            .AddField(
                "Languages",
                profile.Languages.Count != 0
                    ? "> *" + string.Join(", ", profile.Languages.Select(l => l.Label)) + "*"
                    : "> *Nothing*"
            )
            .AddField(
                "Proficiencies",
                profile.Proficiencies.Count != 0
                    ? "> *" + string.Join(", ", profile.Proficiencies.Select(p => p.Label)) + "*"
                    : "> *Nothing*"
            )
            .WithFooter(
                "Stats\n:" + string.Join("\n", profile.Stats.Select(s => $"{s.Label}: {s.Value}"))
            )
            .WithDescription("desc goes here")
            .WithColor(Color.Blue);

        return embed.Build();
    }
}
