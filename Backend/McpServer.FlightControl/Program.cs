using McpServer.FlightControl;

var builder = WebApplication.CreateBuilder(args);

// MCP server registration
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

builder.Services.AddSingleton<Tools>();
builder.Services.AddSingleton<GeocodingService>();

builder.Services.AddHttpClient();

var app = builder.Build();

app.MapMcp();

app.Run();
