using GenerativeAI;
using GenerativeAI.Microsoft;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OllamaSharp;
using OpenAI;
using System.Collections.Concurrent;

namespace Bff.Service.Services;

public class AiChatService(ILogger<AiChatService> logger, IConfiguration config)
{
    private IChatClient? _chatClient;
    private readonly ConcurrentDictionary<string, IList<McpClientTool>> _connectedTools = new();
    private readonly List<ChatMessage> _chatHistory = [];

    private const string SystemInstructions = """
        You are an AI Mission Control & Flight Assistant.

        ROLES:
        1. MISSION CONTROL (Entity Management): Defines, lists, or deletes persistent points and zones on the map.
        2. FLIGHT CONTROL (UAV Operations): Commands the UAV to navigate, change altitude, or change speed.

        CRITICAL RULES:
        1. Use MISSION CONTROL tools to create or manage map entities. These actions do NOT move the UAV.
        2. Use FLIGHT CONTROL tools to navigate the UAV to EXISTING points.
        3. NEVER assume a navigation request when the user asks to "add", "create", or "define" a point.
        4. NEVER output raw JSON tool calls in your response text. 
        5. If you want to use a tool, use the formal tool-calling mechanism.
        6. Use tool response to formulate the answer to the user.
        7. Be extremely concise. Do NOT use markdown formatting. Output plain text only.
        8. COMPLEX REQUESTS: If a user request requires multiple actions (e.g., "Fly to X and set speed Y"), 
            you MUST call multiple tools sequentially. Do not ask for confirmation. Execute ALL parts of the request immediately.
        9. NAVIGATION WORKFLOW: When asked to fly/navigate, use ONLY 'NavigateTo'. NEVER use 'CreatePoint' as part of a flight command. 
            If the user did not provide coordinates (Lat/Lng), you are STRICTLY FORBIDDEN from using 'CreatePoint'.
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

            var mcpEndpoints = config["McpServerUrl"]?.Split(';', StringSplitOptions.RemoveEmptyEntries) 
                               ?? ["http://mcpserver.flightcontrol:8080"];
            
            // Start background connection retry loop for each endpoint
            foreach (var endpoint in mcpEndpoints)
            {
                _ = ConnectToMcpServerAsync(endpoint.Trim());
            }
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
                logger.LogInformation("Connected to MCP Server at {Endpoint}. Fetching tools...", mcpEndpoint);
                var tools = await mcpClient.ListToolsAsync();
                _connectedTools[mcpEndpoint] = tools;
                
                logger.LogInformation("MCP Tools loaded successfully from {Endpoint}:", mcpEndpoint);
                foreach (var tool in tools)
                {
                    logger.LogInformation("Tool available: {Tool}", tool);
                }
                return; // Connection successful, exit loop
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to connect to MCP Server at {Endpoint}. Retrying in 5 seconds...", mcpEndpoint);
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
            // Aggregate tools from all connected MCP servers
            var allTools = _connectedTools.Values.SelectMany(t => t).Cast<AITool>().ToList();

            // Use non-streaming response for better tool-calling reliability with llama
            var response = await _chatClient.GetResponseAsync(_chatHistory, new ChatOptions
            {
                Tools = allTools
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