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

var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
var openAiModel = config["OpenAIModelName"] ?? "gpt-4o";
var openAiKey = config["OpenAIKey"];
var geminiModel = config["GoogleGeminiModel"] ?? "gemini-2.0-flash";
var geminiKey = config["GoogleGeminiKey"];
const string ollamaModel = "llama3.2";
const string ollamaUrl = "http://localhost:11434";
var aiChatService = app.Services.GetRequiredService<AiChatService>();
aiChatService.BuildChatService(ChatType.Ollama, ollamaModel, string.Empty, ollamaUrl);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.UseCors(policyBuilder => policyBuilder
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithOrigins("http://localhost:5173", "http://localhost:4173")
    .AllowCredentials());

app.MapControllers();
app.MapHub<FlightHub>("/flightHub");

app.Run("http://*:5066");
