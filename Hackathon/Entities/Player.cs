namespace Hackathon.Entities;

public class Player
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public string? BackStory { get; set; }
    public int ProficiencyBonus { get; set; }
    public int Gold { get; set; }
    public string? DiscordId { get; set; }
    public string? ImgUrl { get; set; }
}

