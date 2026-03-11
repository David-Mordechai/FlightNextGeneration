using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Agents.FlightControl;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Register Dependencies for native tools
builder.Services.AddHttpClient();
builder.Services.AddSingleton<GeocodingService>();
builder.Services.AddSingleton<FlightTools>();

var ollamaOpenAiEndpoint = builder.Configuration["OllamaOpenAiEndpoint"] ?? "http://localhost:11434/v1";
var modelId = builder.Configuration["FlightModel"] ?? builder.Configuration["OllamaModel"] ?? "llama3.2";

// Register Semantic Kernel and AI Services in DI
builder.Services.AddOpenAIChatCompletion(modelId, endpoint: new Uri(ollamaOpenAiEndpoint), apiKey: "ignore");
builder.Services.AddKernel().Plugins.AddFromType<FlightTools>("Tools");

var app = builder.Build();

app.MapPost("/execute", async ([FromBody] string message, Kernel kernel) =>
{
    var chatSvc = kernel.GetRequiredService<IChatCompletionService>();
    var history = new ChatHistory();
    
    history.AddSystemMessage("You are the Flight Control Agent. Available Tools: [NavigateTo, ChangeSpeed, ChangeAltitude].\n" +
                           "MANDATE: Use EXACT location names from user (e.g. 'Target A').\n" +
                           "CRITICAL RULE: You MUST call a tool for EVERY specific command in the user message. If they ask for speed AND altitude, you MUST call both tools.\n" +
                           "CRITICAL RULE: If the user requests an altitude change AND a flight to a location, pass the NEW altitude to the 'targetAltitude' parameter of 'NavigateTo'.\n" +
                           "RULE: Respond with ONLY one technical string. Example: 'Flying to Home at 500 kts, 5000 ft'. No fluff.");

    history.AddUserMessage(message);

    var settings = new OpenAIPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };
    
    // SINGLE PASS: Trigger tool execution.
    await chatSvc.GetChatMessageContentAsync(history, settings, kernel);

    var content = "";

    // TRUTH GUARD: Check history for SUCCESSFUL tool results
    var toolMessages = history.Where(m => m.Role == AuthorRole.Tool).ToList();
    var successfulResults = toolMessages.Where(m => !m.Content?.Contains("Error", StringComparison.OrdinalIgnoreCase) ?? false).ToList();
    
    if (toolMessages.Any(m => m.Content?.Contains("Error", StringComparison.OrdinalIgnoreCase) == true))
    {
        var error = toolMessages.First(m => m.Content?.Contains("Error", StringComparison.OrdinalIgnoreCase) == true).Content;
        content = $"I encountered an issue: {error}.";
    }
    else if (successfulResults.Count > 0)
    {
        // PERFORMANCE: Human response in C# (ROBUST EXTRACTION FROM TOOL CALLS)
        var actions = new List<string>();
        
        // Find all function calls that led to successful results
        var toolCalls = history.SelectMany(m => m.Items).OfType<FunctionCallContent>().ToList();

        foreach (var call in toolCalls)
        {
            var args = call.Arguments;
            if (call.FunctionName == "NavigateTo") 
            {
                object? loc = null;
                if (args != null && !args.TryGetValue("location", out loc)) args.TryGetValue("locationName", out loc);
                actions.Add($"setting course for {loc ?? "the target"}");
            }
            if (call.FunctionName == "ChangeSpeed") 
            {
                object? speed = null;
                args?.TryGetValue("speed", out speed);
                actions.Add($"adjusting speed to {speed ?? "requested"} knots");
            }
            if (call.FunctionName == "ChangeAltitude") 
            {
                object? alt = null;
                args?.TryGetValue("altitude", out alt);
                actions.Add($"climbing to {alt ?? "requested"} feet");
            }
        }

        if (actions.Count > 0)
        {
            var distinctActions = actions.Distinct().ToList();
            var summary = string.Join(", ", distinctActions);
            summary = char.ToUpper(summary[0]) + summary.Substring(1);
            content = $"{summary}.";
        }
        else 
        {
            content = "Flight parameters have been updated.";
        }
    }
    else
    {
        content = "I've received your request, but no specific flight actions were executed.";
    }

    Console.WriteLine($"[FlightControlAgent] Final Response: {content}");

    return Results.Ok(new { Response = content });
});

app.Run();