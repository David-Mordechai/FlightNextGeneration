using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Agents.Core;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var ollamaOpenAiEndpoint = builder.Configuration["OllamaOpenAiEndpoint"] ?? "http://localhost:11434/v1";
var modelId = builder.Configuration["OllamaModel"] ?? "llama3.2";

var app = builder.Build();

app.MapPost("/route", async ([FromBody] string message) =>
{
    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.AddOpenAIChatCompletion(modelId, endpoint: new Uri(ollamaOpenAiEndpoint), apiKey: "ignore");
    var kernel = kernelBuilder.Build();

    var chatSvc = kernel.GetRequiredService<IChatCompletionService>();
    var history = new ChatHistory();
    history.AddSystemMessage("You are the Tier 1 Router Agent. Decisions: [FlightControl, MissionControl, Payload]. " +
                           "For navigation (fly, go, home), ALWAYS use BOTH [\"FlightControl\", \"Payload\"]. " +
                           "Return ONLY a JSON string array of required agents.");
    history.AddUserMessage(message);

    var response = await chatSvc.GetChatMessageContentAsync(history);
    var content = response.Content?.Trim() ?? "";
    
    List<string> targets;
    try 
    {
        targets = JsonSerializer.Deserialize<List<string>>(content) ?? new List<string>();
    }
    catch 
    {
        // Fallback to simple logic if LLM is chatty
        targets = new List<string>();
        if (content.Contains("FlightControl")) targets.Add("FlightControl");
        if (content.Contains("Payload")) targets.Add("Payload");
        if (content.Contains("MissionControl")) targets.Add("MissionControl");
        
        if (targets.Count == 0)
        {
            var heuristic = AgentRegistry.ResolveAgent(message);
            targets.Add(heuristic);
            if (heuristic == "FlightControl" && (message.Contains("fly") || message.Contains("home")))
                targets.Add("Payload");
        }
    }

    return Results.Ok(new { Targets = targets });
});

app.Run();
