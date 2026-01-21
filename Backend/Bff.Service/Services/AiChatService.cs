using GenerativeAI;
using GenerativeAI.Microsoft;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OllamaSharp;
using OpenAI;
using System.Collections.Concurrent;
using System.Text.Json;

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
        1. YOU ARE BLIND AND DEAF TO THE WORLD. You cannot "see" the map or "move" the UAV yourself. 
        2. You MUST use the provided tools for EVERY physical action (Flying, Changing Speed, Changing Altitude).
        3. EXECUTION MANDATE: If you reply "Navigating to X", you MUST have successfully called the 'navigate_to' tool in this turn. 
           - Checking for the point's existence via 'list_points' is GOOD, but it is NOT ENOUGH.
           - You must followed 'list_points' with 'navigate_to' immediately.
        4. Use FLIGHT CONTROL tools to navigate the UAV to EXISTING points.
        5. NEVER assume a navigation request when the user asks to "add", "create", or "define" a point.
        6. NEVER output raw JSON tool calls in your response text. 
        7. If you want to use a tool, use the formal tool-calling mechanism.
        8. Use tool response to formulate the answer to the user.
        9. Be extremely concise. Do NOT use markdown formatting. Output plain text only.
        10. COMPLEX REQUESTS: If a user request requires multiple actions (e.g., "Fly to X and set speed Y"), 
            you MUST call multiple tools sequentially. Do not ask for confirmation. Execute ALL parts of the request immediately.
        11. NAVIGATION WORKFLOW: When asked to fly/navigate:
            - Step 1: Call 'list_points' to verify the target exists (if you don't know it).
            - Step 2: Call 'navigate_to' with the confirmed point name.
            - Step 3: Call 'look_at' to focus the camera on the destination.
            - NEVER stop at Step 1.
        12. DATA FRESHNESS & NO CACHING: The tool results in your conversation history are SNAPSHOTS. 
            - If the user refers to ANY entity (Point, Zone), you MUST call the relevant listing tool AGAIN to get the latest data.
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
                .UseFunctionInvocation()
                .UseOpenTelemetry()
                .Build(),
            ChatType.OpenAi => new ChatClientBuilder(new OpenAIClient(apiKey)
                    .GetChatClient(model).AsIChatClient())
                .UseFunctionInvocation()
                .UseOpenTelemetry()
                .Build(),
            ChatType.Ollama => new ChatClientBuilder(new OllamaApiClient(providerUrl, model))
                .UseFunctionInvocation()
                .UseOpenTelemetry()
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

            // The middleware (UseFunctionInvocation) will process tool calls.
            // It returns the entire sequence of new messages in response.Messages.
            var response = await _chatClient.GetResponseAsync(_chatHistory, new ChatOptions
            {
                Tools = allTools
            });

            // We MUST manually add the new messages (tool calls, results, final response) 
            // to our history so the AI has context for the next turn.
            foreach (var msg in response.Messages)
            {
                _chatHistory.Add(msg);

                if (msg.Role == ChatRole.Assistant)
                {
                    foreach (var toolCall in msg.Contents.OfType<FunctionCallContent>())
                    {
                        var log = $"[AI TOOL CALL] Executing: {toolCall.Name} with arguments: {(toolCall.Arguments != null ? JsonSerializer.Serialize(toolCall.Arguments) : "none")}";
                        logger.LogInformation(log);
                    }
                }
                else if (msg.Role == ChatRole.Tool)
                {
                    foreach (var toolResult in msg.Contents.OfType<FunctionResultContent>())
                    {
                        var log = $"[AI TOOL RESULT] {toolResult.CallId}: {toolResult.Result?.ToString() ?? "Success"}";
                        logger.LogInformation(log);
                    }
                }
            }

            var fullResponse = response.ToString();
            logger.LogInformation("AI Response: {Response}", fullResponse);

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