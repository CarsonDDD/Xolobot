using Discord.WebSocket;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Interactions;

// Singleton
namespace Hackathon.Managers.Shop;
public class PlayerManager
{
	private static PlayerManager _instance;
	private PlayerManager() { }
	public static PlayerManager Instance
	{
		get
		{
			if (_instance == null)
			{
				_instance = new PlayerManager();
			}

			return _instance;
		}
	}

	/*public async Task ShowPlayerPage(ISocketMessageChannel location, PlayerObject player, IUserMessage existingMessage = null)
	{
		EmbedBuilder window = new EmbedBuilder()
			.WithTitle(player.player.characterName);
            
    }

	public EmbedBuilder ShowPlayerInventroy(PlayerObject player, bool showAsList)
	{
		EmbedBuilder window = new EmbedBuilder()
			.WithTitle($"{player.player.characterName}'s Inventory");

		foreach(Item item in player.inventory)
		{
			window.AddField($"{item.name}",$"{item.shortdescription}",true);
		}

		return window;
	}*/
}
