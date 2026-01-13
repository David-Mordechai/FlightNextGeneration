using GenerativeAI;
using GenerativeAI.Microsoft;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OllamaSharp;
using OpenAI;

namespace Bff.Service.Services;

public class AiChatService(ILogger<AiChatService> logger)
{
    private IChatClient? _chatClient;
    private IList<McpClientTool> _tools = [];

    public async void BuildChatService(ChatType chatType, string model, string apiKey, string providerUrl)
    {
        try
        {
            _chatClient = BuildChatClient(chatType, model, apiKey, providerUrl);

            var mcpClient = await McpClient.CreateAsync(
                new HttpClientTransport(new HttpClientTransportOptions
                {
                    Endpoint = new Uri("http://localhost:52001")
                }));

            // List all available tools from the MCP server.
            logger.LogInformation("Available tools:");
            _tools = await mcpClient.ListToolsAsync();
            foreach (var tool in _tools)
            {
                logger.LogInformation("Tool available: {Tool}", tool);
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Fail to start chat service");
        }
    }

    private static IChatClient BuildChatClient(ChatType chatType, string model, string apiKey, string providerUrl)
    {
        return chatType switch
        {
            ChatType.GoogleGemini => new ChatClientBuilder(new GenerativeAIChatClient(
                    new GoogleAIPlatformAdapter(apiKey), modelName: model))
                .Build(),
            ChatType.OpenAi => new ChatClientBuilder(new OpenAIClient(apiKey)
                    .GetChatClient(model).AsIChatClient())
                .UseFunctionInvocation()
                .Build(),
            ChatType.Ollama => new ChatClientBuilder(new OllamaApiClient(providerUrl, model))
                .UseFunctionInvocation()
                .Build(),
            _ => throw new ArgumentOutOfRangeException(nameof(chatType), chatType, null)
        };
    }

    public async Task<string> ProcessUserMessage(string userMessage)
    {
        if(_chatClient is null) return "Chat service not initialized.";
        var response = string.Empty;
        var message = new ChatMessage(ChatRole.User, userMessage);

        await foreach (var update in _chatClient.GetStreamingResponseAsync(message, new ChatOptions
                       {
                           Tools = [.. _tools]
                       }))
        {
            if (update.Role != ChatRole.Tool) continue;

            if (update.Contents.FirstOrDefault() is FunctionResultContent text)
            {
                response = text.Result?.ToString();
            }
        }

        return response ?? "Fail to process user request";
    }
}

public enum ChatType
{
    OpenAi,
    GoogleGemini,
    Ollama
}