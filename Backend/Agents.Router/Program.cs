using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var ollamaOpenAiEndpoint = builder.Configuration["OllamaOpenAiEndpoint"] ?? "http://localhost:11434/v1";
var modelId = builder.Configuration["RouterModel"] ?? builder.Configuration["OllamaModel"] ?? "llama3.2";

// Register Semantic Kernel and AI Services in DI
builder.Services.AddOpenAIChatCompletion(modelId, endpoint: new Uri(ollamaOpenAiEndpoint), apiKey: "ignore");
builder.Services.AddKernel();

var app = builder.Build();

app.MapPost("/route", async ([FromBody] string message, Kernel kernel) =>
{
    var chatSvc = kernel.GetRequiredService<IChatCompletionService>();
    var history = new ChatHistory();
    history.AddSystemMessage("You are the Tier 1 Router Agent. Decisions: [\"FlightControl\", \"MissionControl\", \"Payload\"].\n" +
                           "ROUTING RULES:\n" +
                           "1. For navigation (fly, go, return, home, speed, altitude), ALWAYS use [\"FlightControl\", \"Payload\"].\n" +
                           "2. For map management (create, add, delete, remove, clear, points, zones), use [\"MissionControl\"].\n" +
                           "3. CRITICAL: DO NOT use MissionControl for flight commands unless it's explicitly to CREATE or DELETE a point/zone.\n" +
                           "4. CRITICAL: Return ONLY a JSON string array of required agents (e.g. [\"FlightControl\"]). DO NOT include any other text, explanation, or extra JSON fields. If multiple agents are needed, return them in one array.");
    history.AddUserMessage(message);

    var response = await chatSvc.GetChatMessageContentAsync(history);
    var content = response.Content?.Trim() ?? "";
    
    Console.WriteLine($"[RouterAgent] Raw Classification: {content}");

    var targets = new List<string>();
    
    // Pure Model-Driven Intent Routing
    if (content.Contains("[") && content.Contains("]"))
    {
        try 
        {
            var start = content.IndexOf("[");
            var end = content.LastIndexOf("]") + 1;
            var json = content.Substring(start, end - start);
            targets = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch { }
    }

    if (targets.Count == 0)
    {
        if (content.Contains("FlightControl", StringComparison.OrdinalIgnoreCase)) targets.Add("FlightControl");
        if (content.Contains("MissionControl", StringComparison.OrdinalIgnoreCase)) targets.Add("MissionControl");
        if (content.Contains("Payload", StringComparison.OrdinalIgnoreCase)) targets.Add("Payload");
    }

    Console.WriteLine($"[RouterAgent] Final Targets: {string.Join(", ", targets)}");

    return Results.Ok(new { Targets = targets });
});

app.Run();
