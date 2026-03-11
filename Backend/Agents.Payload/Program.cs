using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Agents.Payload;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Register Dependencies for native tools
builder.Services.AddHttpClient();
builder.Services.AddSingleton<GeocodingService>();
builder.Services.AddSingleton<PayloadTools>();

var ollamaOpenAiEndpoint = builder.Configuration["OllamaOpenAiEndpoint"] ?? "http://localhost:11434/v1";
var modelId = builder.Configuration["PayloadModel"] ?? builder.Configuration["OllamaModel"] ?? "llama3.2";

// Register Semantic Kernel and AI Services in DI
builder.Services.AddOpenAIChatCompletion(modelId, endpoint: new Uri(ollamaOpenAiEndpoint), apiKey: "ignore");
builder.Services.AddKernel().Plugins.AddFromType<PayloadTools>("Tools");

var app = builder.Build();

app.MapPost("/execute", async ([FromBody] string message, Kernel kernel) =>
{
    Console.WriteLine($"[PayloadAgent] Received Message: {message}");
    var chatSvc = kernel.GetRequiredService<IChatCompletionService>();
    var history = new ChatHistory();
    
    history.AddSystemMessage("You are the Payload Agent. Available Tools: [PointPayload, ResetPayload].\n" +
                           "MISSION: Lock the camera gimbal on any location mentioned in the user's command.\n" +
                           "MANDATE: If a location (Home, Target A, etc.) is mentioned, you MUST call PointPayload for that location.\n" +
                           "MANDATE: If 'reset' or 're-calibrate' is mentioned, you MUST call ResetPayload.\n" +
                           "CONFIRMATION: Your primary job is tool execution. C# will handle the natural language confirmation.");
    history.AddUserMessage(message);

    var settings = new OpenAIPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };
    
    // SINGLE PASS: Trigger tool execution.
    await chatSvc.GetChatMessageContentAsync(history, settings, kernel);

    var content = "";

    // TRUTH GUARD: Check history for SUCCESSFUL tool results.
    var toolMessages = history.Where(m => m.Role == AuthorRole.Tool).ToList();
    var successfulResults = toolMessages.Where(m => !m.Content?.Contains("Error", StringComparison.OrdinalIgnoreCase) ?? false).ToList();
    
    if (successfulResults.Count > 0)
    {
        // PERFORMANCE: Human response in C# (ROBUST EXTRACTION)
        var actions = new List<string>();
        var toolCalls = history.SelectMany(m => m.Items).OfType<FunctionCallContent>().ToList();

        foreach (var call in toolCalls)
        {
            var args = call.Arguments;
            if (call.FunctionName == "PointPayload")
            {
                object? loc = null;
                if (args != null && !args.TryGetValue("location", out loc)) args.TryGetValue("locationName", out loc);
                actions.Add($"I've locked the camera gimbal on {loc ?? "the target"} for you.");
            }
            else if (call.FunctionName == "ResetPayload")
            {
                actions.Add("Sensors have been reset and calibrated.");
            }
        }

        if (actions.Count > 0)
        {
            content = string.Join(" ", actions.Distinct());
        }
    }
    else if (toolMessages.Any(m => m.Content?.Contains("Error", StringComparison.OrdinalIgnoreCase) == true))
    {
        content = "I'm sorry, I encountered a technical issue while attempting to lock the sensor.";
    }
    else
    {
        content = ""; // Silent if no relevant payload actions
    }

    Console.WriteLine($"[PayloadAgent] Final Response: {content}");

    return Results.Ok(new { Response = content });
});

app.Run();