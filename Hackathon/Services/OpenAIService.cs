using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;

namespace Hackathon.Services;

public class OpenAIService
{
    public class ResponseGenerationArgs : EventArgs
    {
        public string Prompt { get; set; }
        public string OriginalPrompt { get; }
        public String? Response { get; set; }

        public ResponseGenerationArgs(String prompt, String response) //maybe user who make the request?!?!?
        {
            OriginalPrompt = (String)prompt.Clone();
            Prompt = prompt;
            Response = response;
        }
    }

    public delegate void ResponseGenerationEvent(object sender, ResponseGenerationArgs e);
    public event ResponseGenerationEvent? OnPostResponseGeneration;
    public event ResponseGenerationEvent? OnPreResponseGeneration;

    private readonly OpenAIClient _client;
    private readonly string _systemPrompt;
    private const string MODEL = "gpt-3.5-turbo"; //"gpt-3.5-turbo";

    private readonly ILogger _logger;

    public OpenAIService(string apiKey, string systemPrompt, ILogger<OpenAIService> logger)
    {
        _client = new OpenAIClient(apiKey);
        _systemPrompt = systemPrompt;

        _logger = logger;
        _logger.LogInformation($"Connected to OpenAI {MODEL}");
    }

    public async Task<string> GenerateResponse(
        DiscordSocketClient bot,
        SocketMessage mentionMessage,
        int contextLength = 10
    )
    {
        var messages = await mentionMessage.Channel.GetMessagesAsync(contextLength).FlattenAsync();

        var conversation = new List<ChatRequestMessage>(contextLength + 1);

        for (int i = 0; i < messages.Count(); i++)
        {
            var message = messages.ElementAt(i);
            // Skip messages from bots and the mention message itself
            //if(message.Author.IsBot || message.Id == mentionMessage.Id) continue;

            string username = CleanUsername(message.Author.Username);

            string messageString = message.Content;
            // Potentially modify prompt
            if (i == messages.Count() - 1)
            {
                // This is the prompt
                ResponseGenerationArgs promptModifyArgs = new ResponseGenerationArgs(
                    messageString,
                    null
                );
                OnPreResponseGeneration?.Invoke(this, promptModifyArgs);
                messageString = promptModifyArgs.Prompt;
            }

            // How can I make it have the multiple Users? In javascript I can add a username, Do i need to do this manually by injecting into prompt?
            string formattedMessage = $"{username}: {messageString}";

            // properly detemrine message type
            if (message.Author.Id == bot.CurrentUser.Id)
            {
                conversation.Add(new ChatRequestAssistantMessage($"{formattedMessage}"));
            }
            else
            {
                conversation.Add(new ChatRequestUserMessage($"{formattedMessage}"));
            }
            //await Console.Out.WriteLineAsync(formattedMessage);
        }

        // Order matter
        conversation.Add(new ChatRequestSystemMessage(_systemPrompt)); //this needs to be first

        conversation.Reverse();

        // Do GPT
        var chatCompletionsOptions = new ChatCompletionsOptions()
        {
            DeploymentName = MODEL,
            //MaxTokens = 100,
        };
        foreach (var chatMessage in conversation)
        {
            chatCompletionsOptions.Messages.Add(chatMessage);
        }

        //var response = await _client.GetChatCompletionsAsync(chatCompletionsOptions);
        //return response.Value.Choices[0].Message.Content;
        return await MakeOpenAIRequest(chatCompletionsOptions);
    }

    public async Task<string> GenerateFromPromptWithHistory(
        DiscordSocketClient bot,
        IEnumerable<IMessage> messageHistory,
        string prompt = ""
    )
    {
        var conversation = new List<ChatRequestMessage>();

        foreach (var message in messageHistory)
        {
            //if(message.Author.IsBot) continue;

            string username = CleanUsername(message.Author.Username);
            string messageString = message.Content;

            string formattedMessage = $"{username}: {messageString}";

            // detemrine message type
            if (message.Author.Id == bot.CurrentUser.Id)
            {
                conversation.Add(new ChatRequestAssistantMessage($"{formattedMessage}"));
            }
            else
            {
                conversation.Add(new ChatRequestUserMessage($"{formattedMessage}"));
            }
        }

        // handle prompt itself
        if (!string.IsNullOrEmpty(prompt))
        {
            ResponseGenerationArgs args = new ResponseGenerationArgs(prompt, null);
            OnPreResponseGeneration?.Invoke(this, args);
            prompt = args.Prompt;

            conversation.Add(new ChatRequestUserMessage(prompt));
        }

        conversation.Add(new ChatRequestSystemMessage(_systemPrompt)); //this needs to be first
        conversation.Reverse();

        // Do GPT
        var chatCompletionsOptions = new ChatCompletionsOptions()
        {
            DeploymentName = MODEL,
            //MaxTokens = 100,
        };
        foreach (var chatMessage in conversation)
        {
            chatCompletionsOptions.Messages.Add(chatMessage);
        }

        // Make the OpenAI Request
        return await MakeOpenAIRequest(chatCompletionsOptions);
    }

    public async Task<string> GenerateFromPrompt(
        String prompt,
        String systemPrompt = null,
        bool callOnPreResponseGeneration = false
    )
    {
        var chatCompletionsOptions = new ChatCompletionsOptions()
        {
            DeploymentName = MODEL,
            //MaxTokens = 100,
        };

        if (systemPrompt == null)
        {
            systemPrompt = _systemPrompt;
        }

        chatCompletionsOptions.Messages.Add(new ChatRequestSystemMessage(systemPrompt));

        if (callOnPreResponseGeneration)
        {
            ResponseGenerationArgs args = new ResponseGenerationArgs(prompt, null);
            OnPreResponseGeneration?.Invoke(this, args);
            prompt = args.Prompt;
        }

        chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(prompt));

        //var response = await _client.GetChatCompletionsAsync(chatCompletionsOptions);
        //return response.Value.Choices[0].Message.Content;
        return await MakeOpenAIRequest(chatCompletionsOptions);
    }

    private async Task<string> MakeOpenAIRequest(ChatCompletionsOptions context)
    {
        // last is system, second last is prompt
        string prompt = context.Messages.ElementAt(context.Messages.Count - 2).ToString()!;

        var response = await _client.GetChatCompletionsAsync(context);
        string output = response.Value.Choices[0].Message.Content;

        ResponseGenerationArgs args = new ResponseGenerationArgs(prompt, output);
        OnPostResponseGeneration?.Invoke(this, args);

        return args.Response;
    }

    private String CleanUsername(String name) =>
        Regex.Replace(Regex.Replace(name, @"\s+", "_"), @"[^\w\s]", "");

    // originalMessage determines if reply or not
    public String[] SplitResponse(string response, int chunkSize = 2000)
    {
        List<String> chunks = new List<string>();
        for (int i = 0; i < response.Length; i += chunkSize)
        {
            var chunk = response.Substring(i, Math.Min(chunkSize, response.Length - i));
            chunks.Add(chunk);
        }

        return chunks.ToArray();
    }
}
