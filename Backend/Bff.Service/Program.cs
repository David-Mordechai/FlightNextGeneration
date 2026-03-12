using Bff.Service.Hubs;
using Bff.Service.Services;
using Bff.Service.Workers;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient();
builder.Services.AddSignalR();

// Register Custom Services
builder.Services.AddSingleton<FlightStateService>();
builder.Services.AddSingleton<AiChatService>();
builder.Services.AddSingleton<NotificationService>();

// Register Speech Services: Offline for STT, Google for TTS
builder.Services.AddSingleton<OfflineSpeechService>();
builder.Services.AddSingleton<ISpeechService, GoogleSpeechService>();

builder.Services.AddHostedService<FlightSimulationWorker>();

// Configure Logging
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Configure CORS
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173", "http://localhost:4173"];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policyBuilder =>
    {
        policyBuilder.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors();
app.MapControllers();
app.MapHub<FlightHub>("/flightHub");

app.Run();
