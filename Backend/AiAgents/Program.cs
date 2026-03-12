using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using AiAgents.Router;
using AiAgents.FlightControl;
using AiAgents.Payload;
using AiAgents.MissionControl;
using AiAgents.Shared;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// 1. Register Infrastructure
builder.Services.AddHttpClient();
builder.Services.AddSingleton<GeocodingService>();

// 2. Register Agent Tools (Plugins)
builder.Services.AddSingleton<FlightTools>();
builder.Services.AddSingleton<PayloadTools>();
builder.Services.AddSingleton<MissionTools>();

// 3. Register Shared AI Connection (One model instance for all agents)
var ollamaOpenAiEndpoint = builder.Configuration["OllamaOpenAiEndpoint"] ?? "http://localhost:11434/v1";
var modelId = builder.Configuration["OllamaModel"] ?? "llama3.2";

// Register the Chat Completion Service as a singleton so all agents share the same "warm" connection
builder.Services.AddOpenAIChatCompletion(modelId, endpoint: new Uri(ollamaOpenAiEndpoint), apiKey: "ignore");

// 4. Register In-Process Agent Services
builder.Services.AddScoped<RouterAgent>();
builder.Services.AddScoped<FlightAgent>();
builder.Services.AddScoped<PayloadAgent>();
builder.Services.AddScoped<MissionAgent>();

var app = builder.Build();

app.MapPost("/execute", async ([FromBody] string message, 
    RouterAgent router, 
    FlightAgent flight, 
    PayloadAgent payload, 
    MissionAgent mission) =>
{
    var startTime = DateTime.UtcNow;
    Console.WriteLine($"[AiAgents] START Request: {message}");

    // Phase 1: Routing (In-Process)
    var targets = await router.ClassifyAsync(message);
    Console.WriteLine($"[AiAgents] Targets: {string.Join(", ", targets)}");

    if (targets.Count == 0) return Results.Ok(new { Response = "No action required." });

    // Phase 2: Parallel Execution (Each agent uses its own isolated Kernel)
    var responses = new List<string>();
    var tasks = targets.Select(async target =>
    {
        string? response = target switch
        {
            "FlightControl" => await flight.ProcessAsync(message),
            "Payload" => await payload.ProcessAsync(message),
            "MissionControl" => await mission.ProcessAsync(message),
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(response))
        {
            lock (responses) { responses.Add(response); }
        }
    });

    await Task.WhenAll(tasks);

    var finalResult = string.Join(". ", responses.Distinct().Select(r => r.Trim().TrimEnd('.'))) + ".";
    var duration = (DateTime.UtcNow - startTime).TotalSeconds;
    Console.WriteLine($"[AiAgents] END Request. Duration: {duration:F2}s. Result: {finalResult}");

    return Results.Ok(new { Response = finalResult });
});

app.Run();
