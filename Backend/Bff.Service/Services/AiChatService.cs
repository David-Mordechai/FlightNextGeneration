using GenerativeAI;
using GenerativeAI.Microsoft;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OllamaSharp;
using OpenAI;

namespace Bff.Service.Services;

public class AiChatService(ILogger<AiChatService> logger, IConfiguration config)
{
    private IChatClient? _chatClient;
    private IList<McpClientTool> _tools = [];

    public void BuildChatService(ChatType chatType, string model, string apiKey, string providerUrl)
    {
        try
        {
            _chatClient = BuildChatClient(chatType, model, apiKey, providerUrl);
            var mcpEndpoint = config["McpServerUrl"] ?? "http://mcpserver.flightcontrol:8080";
            
            // Start background connection retry loop
            _ = ConnectToMcpServerAsync(mcpEndpoint);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Fail to initialize chat client");
        }
    }

    private async Task ConnectToMcpServerAsync(string mcpEndpoint)
    {
        while (true)
        {
            try
            {
                logger.LogInformation("Connecting to MCP Server at {Endpoint}...", mcpEndpoint);
                var mcpClient = await McpClient.CreateAsync(
                    new HttpClientTransport(new HttpClientTransportOptions
                    {
                        Endpoint = new Uri(mcpEndpoint)
                    }));

                // List all available tools from the MCP server.
                logger.LogInformation("Connected to MCP Server. Fetching tools...");
                _tools = await mcpClient.ListToolsAsync();
                
                logger.LogInformation("MCP Tools loaded successfully:");
                foreach (var tool in _tools)
                {
                    logger.LogInformation("Tool available: {Tool}", tool);
                }
                return; // Connection successful, exit loop
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to connect to MCP Server. Retrying in 5 seconds...");
                await Task.Delay(5000);
            }
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

        try
        {
            await foreach (var update in _chatClient.GetStreamingResponseAsync(message, new ChatOptions
                           {
                               Tools = [.. _tools]
                           }))
            {
                if (update.Role != ChatRole.Tool) continue;

                if (update.Contents.FirstOrDefault() is FunctionResultContent text)
                {
                    response += text.Result?.ToString();
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Fail to process user message");
        }


        return string.IsNullOrEmpty(response) ? "Fail to process user request" : response;
    }
}

public enum ChatType
{
    OpenAi,
    GoogleGemini,
    Ollama
}