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
var modelId = builder.Configuration["OllamaModel"] ?? "llama3.2";
var mcpEndpoint = builder.Configuration["McpUrl"] ?? "http://mcpserver.flightcontrol:8080";

var app = builder.Build();

app.MapPost("/execute", async ([FromBody] string message) =>
{
    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.AddOpenAIChatCompletion(modelId, endpoint: new Uri(ollamaOpenAiEndpoint), apiKey: "ignore");

    // TIER 3: DYNAMIC TOOL INJECTION
    var functions = await DynamicToolInjector.GetToolsAsync(mcpEndpoint, "FlightControlAgent");
    if (functions.Count > 0)
    {
        kernelBuilder.Plugins.AddFromFunctions("FlightTools", functions);
    }

    var kernel = kernelBuilder.Build();
    var chatSvc = kernel.GetRequiredService<IChatCompletionService>();
    var history = new ChatHistory();
    
    bool isNav = message.Contains("fly", StringComparison.OrdinalIgnoreCase) || 
                 message.Contains("go to", StringComparison.OrdinalIgnoreCase) || 
                 message.Contains("navigate", StringComparison.OrdinalIgnoreCase) ||
                 message.Contains("home", StringComparison.OrdinalIgnoreCase);

    history.AddSystemMessage("You are the Flight Control Agent. Manage UAV nav/speed/alt. Use tools. " +
                           "Use the FULL name of points (e.g. Home, Target A). " +
                           "Provide a 1-sentence confirmation after actions. " +
                           "DO NOT output internal tool call syntax in your text. " +
                           "RESPONSE RULE: Max 10 words. No markdown.");
    history.AddUserMessage(message);

    var settings = new OpenAIPromptExecutionSettings 
    { 
        FunctionChoiceBehavior = isNav ? FunctionChoiceBehavior.Required(functions.Where(f => f.Name == "navigate_to")) : FunctionChoiceBehavior.Auto() 
    };
    
    var finalResponse = await chatSvc.GetChatMessageContentAsync(history, settings, kernel);

    var content = finalResponse.Content ?? "";

    // Improved CLEANUP
    content = Regex.Replace(content, @"<.*?>", ""); 
    content = Regex.Replace(content, @"FlightTools-[\w_]+", "");
    content = Regex.Replace(content, @"\(location=.*?\)", "");
    content = content.Trim();

    Console.WriteLine($"[FlightControlAgent] Final Response: {content}");

    return Results.Ok(new { Response = content });
});

app.Run();
