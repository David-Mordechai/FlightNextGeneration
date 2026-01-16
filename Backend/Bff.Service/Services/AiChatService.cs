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
    private readonly List<ChatMessage> _chatHistory = [];

    private const string SystemInstructions = """
                                               You are an AI Flight Control Assistant.

                                               CRITICAL RULES:
                                               1. You have ACCESS to real-time flight tools. Use them!
                                               2. NEVER output raw JSON tool calls in your response text. 
                                               3. If you want to use a tool, use the formal tool-calling mechanism.
                                               4. Use tool response to formulate the answer to the user.
                                               5. Be extremely concise. Direct answers only.
                                               6. Do NOT use markdown formatting like bold (**text**) or italics in your response. Output plain text only.
                                               """;

    public void BuildChatService(ChatType chatType, string model, string apiKey, string providerUrl)
    {
        try
        {
            _chatClient = BuildChatClient(chatType, model, apiKey, providerUrl);
            
            // Initialize history with system prompt
            if (_chatHistory.Count == 0)
            {
                _chatHistory.Add(new ChatMessage(ChatRole.System, SystemInstructions));
            }

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
        
        // Add user message to history
        _chatHistory.Add(new ChatMessage(ChatRole.User, userMessage));

        try
        {
            // Use non-streaming response for better tool-calling reliability with llama
            var response = await _chatClient.GetResponseAsync(_chatHistory, new ChatOptions
            {
                Tools = [.. _tools]
            });

            // Let's try to get the text from the response safely.
            var fullResponse = response.ToString();
            
            // If the model returned tool results, they might be in the history already if managed by middleware,
            // but for the final user response, we just take the last text message.
            
            logger.LogInformation("AI Response: {Response}", fullResponse);

            // Add assistant response to history
            if (!string.IsNullOrEmpty(fullResponse))
            {
                _chatHistory.Add(new ChatMessage(ChatRole.Assistant, fullResponse));
            }

            return fullResponse;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Fail to process user message");
            return "Error processing request.";
        }
    }
}

public enum ChatType
{
    OpenAi,
    GoogleGemini,
    Ollama
}