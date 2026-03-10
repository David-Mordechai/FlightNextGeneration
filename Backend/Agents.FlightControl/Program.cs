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

var app = builder.Build();

app.MapPost("/execute", async ([FromBody] string message, FlightTools flightTools) =>
{
    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.AddOpenAIChatCompletion(modelId, endpoint: new Uri(ollamaOpenAiEndpoint), apiKey: "ignore");

    // Register Native Tools
    kernelBuilder.Plugins.AddFromObject(flightTools, "Tools");

    var kernel = kernelBuilder.Build();
    var chatSvc = kernel.GetRequiredService<IChatCompletionService>();
    var history = new ChatHistory();
    
    history.AddSystemMessage("You are the Flight Control Agent. Available Tools: [NavigateTo, ChangeSpeed, ChangeAltitude].\n" +
                           "MANDATE: Use EXACT location names from user (e.g. 'Target A').\n" +
                           "RULE: You MUST call tools for movement or parameter changes.\n" +
                           "RULE: Respond with ONLY one technical string. Example: 'Flying to Home at 500 kts, 5000 ft'. No fluff.");

    history.AddUserMessage(message);

    var settings = new OpenAIPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };
    var finalResponse = await chatSvc.GetChatMessageContentAsync(history, settings, kernel);

    var content = finalResponse.Content ?? "";

    // TRUTH GUARD: Ensure tools were actually fired before allowing the model to claim success.
    var hasToolCalls = history.Any(m => m.Role == AuthorRole.Tool || m.Items.OfType<FunctionCallContent>().Any());
    var toolErrors = history.Where(m => m.Role == AuthorRole.Tool)
                            .Select(m => m.Content)
                            .Where(c => c?.Contains("Error", StringComparison.OrdinalIgnoreCase) == true || 
                                        c?.Contains("Could not find", StringComparison.OrdinalIgnoreCase) == true)
                            .ToList();

    if (toolErrors.Count > 0)
    {
        content = $"Action failed: {toolErrors.First()}";
    }
    else if (hasToolCalls)
    {
        // HARDENED: Return only a technical summary, ignore all LLM chat fluff
        var summary = message.Trim().TrimEnd('.');
        content = $"Executed: {summary}.";
    }
    else
    {
        content = "Error: Intent recognized but no flight tools were triggered.";
    }

    // CLEANUP
    content = Regex.Replace(content, @"Tools\s+tool\s+called", "", RegexOptions.IgnoreCase);
    content = Regex.Replace(content, @"\(.*?\)", ""); 
    content = content.Trim();

    Console.WriteLine($"[FlightControlAgent] Final Response: {content}");

    return Results.Ok(new { Response = content });
});

app.Run();