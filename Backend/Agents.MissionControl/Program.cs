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
var mcpEndpoint = builder.Configuration["McpUrl"] ?? "http://mcpserver.missioncontrol:8080";

var app = builder.Build();

app.MapPost("/execute", async ([FromBody] string message) =>
{
    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.AddOpenAIChatCompletion(modelId, endpoint: new Uri(ollamaOpenAiEndpoint), apiKey: "ignore");

    // TIER 3: DYNAMIC TOOL INJECTION
    var functions = await DynamicToolInjector.GetToolsAsync(mcpEndpoint, "MissionControlAgent");
    if (functions.Count > 0)
    {
        kernelBuilder.Plugins.AddFromFunctions("MissionTools", functions);
    }

    var kernel = kernelBuilder.Build();
    var chatSvc = kernel.GetRequiredService<IChatCompletionService>();
    var history = new ChatHistory();
    history.AddSystemMessage("You are the Mission Control Agent. Manage map entities/zones/points. Use tools. " +
                           "NEVER output raw tool tags like <argkey> in your text response. " +
                           "RESPONSE RULE: Max 10 words. Plain text only. No markdown.");
    history.AddUserMessage(message);

    var settings = new OpenAIPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };
    var finalResponse = await chatSvc.GetChatMessageContentAsync(history, settings, kernel);

    var content = finalResponse.Content ?? "";

    // CLEANUP: Remove any leaked tool call syntax from the output
    content = Regex.Replace(content, @"<toolcall>.*?</toolcall>", "", RegexOptions.Singleline);
    content = Regex.Replace(content, @"<argkey>.*?</argkey>", "", RegexOptions.Singleline);
    content = Regex.Replace(content, @"<argvalue>.*?</argvalue>", "", RegexOptions.Singleline);
    content = Regex.Replace(content, @"MissionTools-[\w_]+", "", RegexOptions.Singleline);
    content = content.Trim();

    Console.WriteLine($"[MissionControlAgent] Final Response: {content}");

    return Results.Ok(new { Response = content });
});

app.Run();
