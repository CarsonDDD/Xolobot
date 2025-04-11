using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Serilog;
using Serilog.Sinks.SystemConsole;
using MongoDB.Driver;
using Hackathon.Services;
using Microsoft.Extensions.Logging;
using Hackathon.Managers.Shop;

public class Startup
{
	public async Task Initialize()
	{
		Console.WriteLine("Program Starting");
		var builder = new HostBuilder();

		builder.ConfigureAppConfiguration(options =>
			options.AddJsonFile("appsettings.json")
				.AddEnvironmentVariables());

		var loggerConfig = new LoggerConfiguration()
			.WriteTo.Console()
			.WriteTo.File($"logs/log-{DateTime.Now:yy.MM.dd_HH.mm}.log")
			.CreateLogger();

		builder.ConfigureServices((host, services) =>
		{
			services.AddLogging(options => options.AddSerilog(loggerConfig, dispose: true));

			services.AddSingleton(new DiscordSocketClient(
				new DiscordSocketConfig
				{
					GatewayIntents = GatewayIntents.All,
					FormatUsersInBidirectionalUnicode = false,
					AlwaysDownloadUsers = true,
					LogGatewayIntentWarnings = false,
					LogLevel = LogSeverity.Info
				}));

			// DISCORD
			services.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>(), new InteractionServiceConfig()
			{
				LogLevel = LogSeverity.Info
			}));

			services.AddSingleton<InteractionHandler>();
			services.AddHostedService<DiscordBotService>();

			// OPENAI
			services.AddSingleton(provider =>
			{
				var apiKey = host.Configuration["Secrets:OpenAI"];
				var systemPrompt = host.Configuration["System-prompt"];
				var logger = provider.GetRequiredService<ILogger<OpenAIService>>();
				return new OpenAIService(apiKey, systemPrompt, logger);
			});


			// MONGO
			//services.Configure<MongoDBSettings>(host.Configuration.GetSection(nameof(MongoDBSettings)));
			//services.AddSingleton<MongoDBService>();
			//services.AddSingleton(new DatabaseService("xolobot.db", logger));
			services.AddSingleton(provider =>
			{
				var logger = provider.GetRequiredService<ILogger<DatabaseService>>();
				return new DatabaseService("xolobot.db", logger);
			});

			services.AddTransient<PlayerService>();
			services.AddTransient<PlayerProfileService>();
			services.AddTransient<ItemService>();
			services.AddTransient<InventoryService>();
			services.AddTransient<ShopService>();

		});

		var app = builder.Build();

		await app.RunAsync();
	}
}
