using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Agents.Core;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var ollamaOpenAiEndpoint = builder.Configuration["OllamaOpenAiEndpoint"] ?? "http://localhost:11434/v1";
var modelId = builder.Configuration["PayloadModel"] ?? builder.Configuration["OllamaModel"] ?? "llama3.2";
var mcpEndpoint = builder.Configuration["McpUrl"] ?? "http://mcpserver.payload:8080";

var app = builder.Build();

app.MapPost("/execute", async ([FromBody] string message) =>
{
    Console.WriteLine($"[PayloadAgent] Received Message: {message}");
    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.AddOpenAIChatCompletion(modelId, endpoint: new Uri(ollamaOpenAiEndpoint), apiKey: "ignore");

    // TIER 3: DYNAMIC TOOL INJECTION
    var functions = await DynamicToolInjector.GetToolsAsync(mcpEndpoint, "PayloadAgent", message);
    if (functions.Count > 0)
    {
        kernelBuilder.Plugins.AddFromFunctions("Tools", functions);
    }

    var kernel = kernelBuilder.Build();
    var chatSvc = kernel.GetRequiredService<IChatCompletionService>();
    var history = new ChatHistory();
    
    var toolNames = string.Join(", ", functions.Select(f => f.Name));
    history.AddSystemMessage("You are the Payload Agent. Available Tools: [PointPayload, ResetPayload].\n" +
                           "MISSION: Lock the camera gimbal on any location mentioned in the user's flight command.\n" +
                           "THINKING PROCESS:\n" +
                           "1. Identify if a location (e.g. 'Target A', 'Home') is in the message.\n" +
                           "2. If yes, you MUST call PointPayload for that location.\n" +
                           "3. If no location, check if 'reset' is mentioned. If yes, call ResetPayload.\n" +
                           "4. Otherwise, return an empty string.\n" +
                           "CONFIRMATION: After calling a tool, respond ONLY with 'Sensor locked: [Location]'.");
    history.AddUserMessage(message);

    var settings = new OpenAIPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };
    var finalResponse = await chatSvc.GetChatMessageContentAsync(history, settings, kernel);

    var content = finalResponse.Content ?? "";

    // Improved TRUTH GUARD: Check history for Tool results.
    var hasToolCalls = history.Any(m => m.Role == AuthorRole.Tool || m.Items.OfType<FunctionCallContent>().Any());
    var toolErrors = history.Where(m => m.Role == AuthorRole.Tool)
                            .Select(m => m.Content)
                            .Where(c => c?.Contains("Error", StringComparison.OrdinalIgnoreCase) == true || 
                                        c?.Contains("Could not find", StringComparison.OrdinalIgnoreCase) == true)
                            .ToList();

    if (toolErrors.Count > 0)
    {
        content = "Action failed: Sensor could not be locked.";
    }
    else if (!hasToolCalls)
    {
        content = "";
    }

    // Improved CLEANUP
    content = Regex.Replace(content, @"Tools\s+tool\s+called", "", RegexOptions.IgnoreCase);
    content = Regex.Replace(content, @"\(.*?\)", ""); 
    content = content.Trim();

    Console.WriteLine($"[PayloadAgent] Final Response: {content}");

    return Results.Ok(new { Response = content });
});

app.Run();
