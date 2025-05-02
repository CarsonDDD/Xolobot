using Discord;
using Hackathon.Services;
using Hackathon.Utility;

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
        string goldDisplay = showGold ? profile.Player.Gold.ToString() : "Unknown";

        /* ---------- stat block ---------- */
        int maxLabel = profile.Stats.Max(s => s.Label.Length);
        int maxValue = profile.Stats.Max(s => s.Value.ToString().Length);
        string statBlock = string.Join(
            '\n',
            profile.Stats.Select(s =>
                $"{s.Label.PadRight(maxLabel)} : {s.Value.ToString().PadLeft(maxValue)}"
            )
        );

        /* ---------- long lists ---------- */
        string languagesField = $">>> {Utils.FormatUIList(profile.Languages.Select(l => l.Label))}";
        string proficienciesField = $">>> {Utils.FormatUIList(profile.Proficiencies.Select(p => p.Label))}";
        string classesField = $">>> {Utils.FormatUIList(profile.Classes.Select(c => c.Label))}";
        string racesField = $">>> {Utils.FormatUIList(profile.Races.Select(r => r.Label))}";

        /* ---------- embed ---------- */
        var embed = new EmbedBuilder()
            .WithAuthor(a =>
            {
                a.IconUrl = user.GetAvatarUrl();
                a.Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
                a.Name = discordName;
            })
            .WithTitle($" __**{profile.Player.Name}'s Profile**__")
            .WithThumbnailUrl(profile.Player.ImgUrl)
            .AddField("Classes:", classesField, inline: true)
            .AddField("Races:", racesField, inline: true)
            .AddField($"Proficiencies (***+{player.ProficiencyBonus}***):", proficienciesField)
            .AddField("Languages:", languagesField)
            .WithDescription($"```cs\n{statBlock}\n```\n **Gold:** **`{goldDisplay}`**")
            .WithColor(Color.Blue);

        return embed.Build();
    }
}
