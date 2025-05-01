namespace Hackathon.Entities;

public class Item
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string? ShortDescription { get; set; }
    public string? LongDescription { get; set; }
    public string? Lore { get; set; }
    public int BaseCost { get; set; }
    public double Weight { get; set; }
    public string? ImgUrl { get; set; }
}
