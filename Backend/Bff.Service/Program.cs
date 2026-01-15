using Bff.Service.Hubs;
using Bff.Service.Services;
using Bff.Service.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddHttpClient();
builder.Services.AddOpenApi();
builder.Services.AddCors();

builder.Services.AddHostedService<FlightSimulationWorker>();
builder.Services.AddSingleton<FlightStateService>();
builder.Services.AddSingleton<AiChatService>();

var app = builder.Build();

var config = app.Configuration;
var openAiModel = config["OpenAIModelName"] ?? "gpt-4o";
var openAiKey = config["OpenAIKey"];
var geminiModel = config["GoogleGeminiModel"] ?? "gemini-2.0-flash";
var geminiKey = config["GoogleGeminiKey"];
var ollamaModel = config["OllamaModel"] ?? "llama3.2";
var ollamaUrl = config["OllamaUrl"] ?? "http://host.docker.internal:11434";
Console.WriteLine($"[DEBUG] Configured OllamaUrl: {ollamaUrl}");

var provider = config["AIProvider"] ?? "Ollama";
var aiChatService = app.Services.GetRequiredService<AiChatService>();

switch (provider.ToLower())
{
    case "openai":
        aiChatService.BuildChatService(ChatType.OpenAi, openAiModel, openAiKey ?? string.Empty, string.Empty);
        break;
    case "gemini":
        aiChatService.BuildChatService(ChatType.GoogleGemini, geminiModel, geminiKey ?? string.Empty, string.Empty);
        break;
    case "ollama":
    default:
        aiChatService.BuildChatService(ChatType.Ollama, ollamaModel, string.Empty, ollamaUrl);
        break;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

var allowedOrigins = config["AllowedOrigins"]?.Split(',') ?? ["http://localhost:5173", "http://localhost:4173"];

app.UseCors(policyBuilder => policyBuilder
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithOrigins(allowedOrigins)
    .AllowCredentials());

app.MapControllers();
app.MapHub<FlightHub>("/flightHub");

app.Run();
