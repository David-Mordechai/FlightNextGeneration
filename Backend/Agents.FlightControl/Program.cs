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
    
    history.AddSystemMessage("You are a flight command parser. Your mission is to extract and execute tools for ALL flight parameters in the user message.\n" +
                           "RULES:\n" +
                           "- If 'fly', 'go', 'home' or a location is mentioned -> call NavigateTo.\n" +
                           "- If 'speed' is mentioned -> call ChangeSpeed with the number provided.\n" +
                           "- If 'altitude' is mentioned -> call ChangeAltitude with the number provided.\n" +
                           "Example: 'fly target2 speed 500 altitude 5000' -> NavigateTo(location='target2'), ChangeSpeed(speed=500), ChangeAltitude(altitude=5000)");

    history.AddUserMessage(message);

    // STABILITY: Disable auto-invocation to manually filter tool calls
    var settings = new OpenAIPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: false) };
    
    // 1. Get tool choices from LLM
    var response = await chatSvc.GetChatMessageContentAsync(history, settings, kernel);
    
    // 2. Extract and Filter tool calls
    var toolCalls = response.Items.OfType<FunctionCallContent>().ToList();
    var executedActions = new List<string>();

    foreach (var call in toolCalls)
    {
        bool allowed = true;
        if (call.FunctionName == "ChangeSpeed" && !message.Contains("speed", StringComparison.OrdinalIgnoreCase)) allowed = false;
        if (call.FunctionName == "ChangeAltitude" && !message.Contains("altitude", StringComparison.OrdinalIgnoreCase)) allowed = false;
        if (call.FunctionName == "NavigateTo" && !Regex.IsMatch(message, @"fly|go|home|return|target", RegexOptions.IgnoreCase)) allowed = false;

        if (allowed)
        {
            try 
            {
                var result = await call.InvokeAsync(kernel);
                
                // Collect for summary
                var args = call.Arguments;
                if (call.FunctionName == "NavigateTo") 
                {
                    object? loc = null;
                    if (args != null && !args.TryGetValue("location", out loc)) args.TryGetValue("locationName", out loc);
                    executedActions.Add($"setting course for {loc ?? "the target"}");
                }
                if (call.FunctionName == "ChangeSpeed") 
                {
                    object? speed = null;
                    args?.TryGetValue("speed", out speed);
                    executedActions.Add($"adjusting speed to {speed ?? "requested"} knots");
                }
                if (call.FunctionName == "ChangeAltitude") 
                {
                    object? alt = null;
                    args?.TryGetValue("altitude", out alt);
                    executedActions.Add($"climbing to {alt ?? "requested"} feet");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FlightControlAgent] Error invoking {call.FunctionName}: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine($"[FlightControlAgent] BLOCKED hallucinated tool call: {call.FunctionName}");
        }
    }

    var content = "";
    if (executedActions.Count > 0)
    {
        var summary = string.Join(", ", executedActions.Distinct());
        summary = char.ToUpper(summary[0]) + summary.Substring(1);
        content = $"{summary}.";
    }
    else
    {
        content = "I've received your request, but no specific flight actions were executed.";
    }

    Console.WriteLine($"[FlightControlAgent] Final Response: {content}");

    return Results.Ok(new { Response = content });
});

app.Run();
