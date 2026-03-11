using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Agents.MissionControl;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Register Dependencies for native tools
builder.Services.AddHttpClient();
builder.Services.AddSingleton<MissionTools>();

var ollamaOpenAiEndpoint = builder.Configuration["OllamaOpenAiEndpoint"] ?? "http://localhost:11434/v1";
var modelId = builder.Configuration["MissionModel"] ?? builder.Configuration["OllamaModel"] ?? "llama3.2";

// Register Semantic Kernel and AI Services in DI
builder.Services.AddOpenAIChatCompletion(modelId, endpoint: new Uri(ollamaOpenAiEndpoint), apiKey: "ignore");
builder.Services.AddKernel().Plugins.AddFromType<MissionTools>("Tools");

var app = builder.Build();

app.MapPost("/execute", async ([FromBody] string message, Kernel kernel) =>
{
    var chatSvc = kernel.GetRequiredService<IChatCompletionService>();
    var history = new ChatHistory();
    
    history.AddSystemMessage("You are the Mission Control Agent. Available Tools: [CreatePoint, ListPoints, DeletePointByName, CreateRectangleZone, CreatePolygonZone, ListNoFlyZones, DeleteNoFlyZoneByName, DeleteAllPoints, DeleteAllNoFlyZones].\n" +
                           "RULE: You MUST use the available tools to perform actions based on the user's intent.\n" +
                           "RULE: If the user intent is NOT about points or no-fly zones, DO NOT call any tools and return an empty string.\n" +
                           "RESPONSE RULE: Return ONLY a technical confirmation string after calling a tool. If no tool is called, return an empty string.");
    history.AddUserMessage(message);

    var settings = new OpenAIPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };
    
    // SINGLE PASS: Trigger tool execution.
    await chatSvc.GetChatMessageContentAsync(history, settings, kernel);

    var content = "";
    
    // TRUTH GUARD: Only report success if a tool call was actually completed successfully.
    var toolResults = history.Where(m => m.Role == AuthorRole.Tool).ToList();
    var successfulToolCalls = toolResults.Where(m => !m.Content?.Contains("Error", StringComparison.OrdinalIgnoreCase) ?? false).ToList();
    
    // Extract original function names corresponding to successful results
    var successfulCallNames = history.SelectMany(m => m.Items)
                                    .OfType<FunctionCallContent>()
                                    .Select(f => f.FunctionName)
                                    .ToList();

    if (successfulToolCalls.Count > 0)
    {
        // Map successful tool calls to human responses in C#
        var actions = new List<string>();
        foreach (var name in successfulCallNames)
        {
            if (name == "CreatePoint") actions.Add("updated map points");
            if (name == "DeletePointByName") actions.Add("removed map points");
            if (name == "CreateRectangleZone") actions.Add("updated restricted zones");
            if (name == "DeleteAllPoints") actions.Add("cleared map points");
            if (name == "DeleteAllNoFlyZones") actions.Add("cleared restricted zones");
        }

        if (actions.Count > 0)
        {
            // DEDUPLICATE: Prevent double reporting
            var distinctActions = actions.Distinct().ToList();
            content = "Mission status: " + string.Join(" and ", distinctActions) + ".";
        }
        else
        {
            content = "Mission database updated.";
        }
    }
    else
    {
        content = ""; // Silent if no tools succeeded or were called
    }

    Console.WriteLine($"[MissionControlAgent] Final Response: {content}");

    return Results.Ok(new { Response = content });
});

app.Run();
