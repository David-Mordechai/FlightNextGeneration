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
builder.Services.AddSingleton<GeocodingService>();

var app = builder.Build();

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
