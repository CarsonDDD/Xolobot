using Discord;
using Hackathon.Entities;
using Hackathon.Services;

namespace Hackathon.DomainObjects;

public class PlayerProfile
{
    public Player Player { get; set; }
    public List<Stat> Stats { get; set; }
    public List<Class> Classes { get; set; }
    public List<Race> Races { get; set; }
    public List<Language> Languages { get; set; }
    public List<Proficiency> Proficiencies { get; set; }
    public InventoryWithItems Inventory { get; set; }
}

