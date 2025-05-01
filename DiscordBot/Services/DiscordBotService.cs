using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hackathon.Services;

public class DiscordBotService(
    DiscordSocketClient client,
    InteractionService interactions,
    IConfiguration config,
    ILogger<DiscordBotService> logger,
    InteractionHandler interactionHandler
    ) : IHostedService
{
    private readonly DiscordSocketClient _client = client;
    private readonly InteractionService _interactions = interactions;
    private readonly IConfiguration _config = config;
    private readonly ILogger _logger = logger;
    private readonly InteractionHandler _interactionHandler = interactionHandler;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _client.Ready += ClientReady;

        _client.Log += LogAsync;
        _interactions.Log += LogAsync;

        await _interactionHandler.InitializeAsync();

        await _client.LoginAsync(TokenType.Bot, _config["Secrets:Discord"]);

        await _client.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _client.StopAsync();
    }

    private async Task ClientReady()
    {
        _logger.LogInformation($"Logged as {_client.CurrentUser}");

        //await _interactions.RegisterCommandsGloballyAsync();

        // Register commands for each guild the bot is in
        foreach (var guild in _client.Guilds)
        {
            await _interactions.RegisterCommandsToGuildAsync(guild.Id);
            _logger.LogInformation($"Registered commands to guild: {guild.Name} (ID: {guild.Id})");
        }
    }

    public async Task LogAsync(LogMessage msg)
    {
        var severity = msg.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Trace,
            LogSeverity.Debug => LogLevel.Debug,
            _ => LogLevel.Information,
        };

        _logger.Log(severity, msg.Exception, msg.Message);

        await Task.CompletedTask;
    }
}
