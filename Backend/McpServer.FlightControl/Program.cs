using McpServer.FlightControl;

var builder = WebApplication.CreateBuilder(args);

// MCP server registration
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

builder.Services.AddSingleton<Tools>();

builder.Services.AddHttpClient();
// Register CommunicationService as a singleton and start it with SignalR hub URL from env
builder.Services.AddSingleton<CommunicationService>(sp =>
{
    var clientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = clientFactory.CreateClient();
    var hubUrl = Environment.GetEnvironmentVariable("SIGNALR_HUB_URL") ?? "http://localhost:3001/hub";
    return new CommunicationService(hubUrl, httpClient);
});

var app = builder.Build();

app.MapMcp();

app.Run("http://*:52001");
