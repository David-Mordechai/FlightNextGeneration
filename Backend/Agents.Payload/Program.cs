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
    
    history.AddSystemMessage("DANGER: DO NOT CALL ResetPayload unless the user message contains the word 'reset'.\n" +
                           "INSTRUCTION: Call PointPayload ONLY for location names (e.g. 'Target A').");

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
        if (call.FunctionName == "ResetPayload" && !message.Contains("reset", StringComparison.OrdinalIgnoreCase)) allowed = false;
        if (call.FunctionName == "PointPayload" && !Regex.IsMatch(message, @"home|target|alpha|beta|A|B", RegexOptions.IgnoreCase)) allowed = false;

        if (allowed)
        {
            try 
            {
                var result = await call.InvokeAsync(kernel);
                
                // Collect for summary
                var args = call.Arguments;
                if (call.FunctionName == "PointPayload")
                {
                    object? loc = null;
                    if (args != null && !args.TryGetValue("location", out loc)) args.TryGetValue("locationName", out loc);
                    executedActions.Add($"I've locked the camera gimbal on {loc ?? "the target"} for you.");
                }
                else if (call.FunctionName == "ResetPayload")
                {
                    executedActions.Add("Sensors have been reset and calibrated.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PayloadAgent] Error invoking {call.FunctionName}: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine($"[PayloadAgent] BLOCKED hallucinated tool call: {call.FunctionName}");
        }
    }

    var content = "";
    if (executedActions.Count > 0)
    {
        content = string.Join(" ", executedActions.Distinct());
    }
    else
    {
        content = ""; // Silent if no relevant payload actions
    }

    Console.WriteLine($"[PayloadAgent] Final Response: {content}");

    return Results.Ok(new { Response = content });
});

app.Run();
