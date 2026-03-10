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

var app = builder.Build();

app.MapPost("/execute", async ([FromBody] string message, MissionTools missionTools) =>
{
    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.AddOpenAIChatCompletion(modelId, endpoint: new Uri(ollamaOpenAiEndpoint), apiKey: "ignore");

    // Register Native Tools
    kernelBuilder.Plugins.AddFromObject(missionTools, "Tools");

    var kernel = kernelBuilder.Build();
    var chatSvc = kernel.GetRequiredService<IChatCompletionService>();
    var history = new ChatHistory();
    
    history.AddSystemMessage("You are the Mission Control Agent. Available Tools: [CreatePoint, ListPoints, DeletePointByName, CreateRectangleZone, CreatePolygonZone, ListNoFlyZones, DeleteNoFlyZoneByName, DeleteAllPoints, DeleteAllNoFlyZones].\n" +
                           "RULE: You MUST use the available tools to perform actions based on the user's intent.\n" +
                           "RULE: NEVER claim to perform an action if you haven't successfully invoked the corresponding tool.\n" +
                           "RESPONSE RULE: Max 10 words. Plain text only. No markdown.");
    history.AddUserMessage(message);

    var settings = new OpenAIPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };
    var finalResponse = await chatSvc.GetChatMessageContentAsync(history, settings, kernel);

    var content = finalResponse.Content ?? "";
    
    // TRUTH GUARD: Check history for ANY tool calls.
    var hasToolCalls = history.Any(m => m.Role == AuthorRole.Tool || m.Items.OfType<FunctionCallContent>().Any());
    var toolErrors = history.Where(m => m.Role == AuthorRole.Tool)
                            .Select(m => m.Content)
                            .Where(c => c?.Contains("Error", StringComparison.OrdinalIgnoreCase) == true)
                            .ToList();

    if (toolErrors.Count > 0)
    {
        content = $"Action failed: {toolErrors.First()}";
    }
    else if (hasToolCalls)
    {
        // HARDENED: Return only a minimal technical summary
        content = "Entities updated.";
    }
    else
    {
        content = "Error: Intent recognized, but no matching tool could be executed. No action taken.";
    }

    // CLEANUP: Remove any leaked tool call syntax from the output
    content = Regex.Replace(content, @"<toolcall>.*?</toolcall>", "", RegexOptions.Singleline);
    content = Regex.Replace(content, @"<argkey>.*?</argkey>", "", RegexOptions.Singleline);
    content = Regex.Replace(content, @"<argvalue>.*?</argvalue>", "", RegexOptions.Singleline);
    content = Regex.Replace(content, @"Tools-[\w_]+", "", RegexOptions.Singleline);
    content = Regex.Replace(content, @"Tools\s+tool\s+called", "", RegexOptions.IgnoreCase);
    content = Regex.Replace(content, @"\(.*?\)", "", RegexOptions.Singleline);
    content = content.Trim();

    Console.WriteLine($"[MissionControlAgent] Final Response: {content}");

    return Results.Ok(new { Response = content });
});

app.Run();