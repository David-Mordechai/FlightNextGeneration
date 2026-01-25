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
        1. YOU ARE BLIND AND DEAF. You cannot "see" or "move" anything without tools.
        2. TOOL USAGE IS MANDATORY: You must use a tool for EVERY physical action.
        3. EXECUTION VERIFICATION: 
           - **NEVER** say "Navigating to X" unless you have **ALREADY** emitted the `navigate_to` tool call in this turn.
           - If you only call `list_points`, you are ONLY "Checking for point X", NOT navigating yet.
           - You MUST chain calls: `list_points` -> `navigate_to`.
        4. Use FLIGHT CONTROL tools to navigate. The `navigate_to` tool will automatically check if the point exists in the database.
        5. NEVER assume a point does not exist. ALWAYS try `navigate_to` first.
        6. NEVER output raw JSON tool calls in your response text. 
        7. If you want to use a tool, use the formal tool-calling mechanism.
        8. Use tool response to formulate the answer to the user.
        9. COMMAND PROTOCOL (MANDATORY):
           - For ANY operational request, you MUST call a tool.
           - For COMPLEX requests (e.g. "Fly to X at speed Y"), you MUST call MULTIPLE tools in a row.
           - Order: 1. `navigate_to` -> 2. `change_speed` -> 3. `change_altitude`.
           - You are FORBIDDEN from replying with text unless ALL requested tools have been executed.
        10. RESPONSE STYLE:
           - EXTREMELY CONCISE (MAX 15 WORDS).
           - SUMMARIZE ALL ACTIONS TAKEN.
           - BE DIRECT. NO FILLER (e.g. "The UAV has been instructed to").
           - PLAIN TEXT ONLY. NO MARKDOWN (NO ASTERISKS, NO BOLD).
           - Example: "Navigating to Target at 500kts and 6000ft."
        11. NAVIGATION:
           - MANDATORY SEQUENCE (NEVER RELY ON CACHE):
             1. Call `list_points`.
             2. Call `list_no_fly_zones`.
             3. Call `navigate_to(location)`.
           - **YOU MUST EXECUTE THESE TOOLS REAL-TIME.**
           - **DO NOT** JUST REPLY WITH TEXT. CALL THE TOOLS.
           - IF YOU DO NOT CALL `navigate_to`, THE UAV WILL NOT MOVE.
        12. SPEED/ALTITUDE:
           - If the user specifies speed (e.g. "speed 500") or altitude ("alt 6000"), call the relevant tool.
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
            ChatType.Ollama => new ChatClientBuilder(new OllamaApiClient(new HttpClient { BaseAddress = new Uri(providerUrl), Timeout = TimeSpan.FromMinutes(5) }, model))
                .UseFunctionInvocation()
                .UseOpenTelemetry()
                .Build(),
            _ => throw new ArgumentOutOfRangeException(nameof(chatType), chatType, null)
        };
    }

    public async Task<bool> CheckReadinessAsync()
    {
        if (_chatClient == null) return false;
        try
        {
            // Send a lightweight probe to ensure the model is loaded and responding
            var response = await _chatClient.GetResponseAsync("ping", new ChatOptions { MaxOutputTokens = 5 });
            return response != null;
        }
        catch (Exception ex)
        {
            logger.LogWarning("AI Readiness check failed: {Message}", ex.Message);
            return false;
        }
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