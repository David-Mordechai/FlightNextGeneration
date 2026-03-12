using Microsoft.AspNetCore.SignalR.Client;

namespace AiAgents.Shared;

public class NotificationService : IAsyncDisposable
{
    private readonly HubConnection _connection;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(IConfiguration config, ILogger<NotificationService> logger)
    {
        _logger = logger;
        var bffUrl = config["BffServiceUrl"] ?? "http://bff.service:8080";
        
        _connection = new HubConnectionBuilder()
            .WithUrl($"{bffUrl}/flightHub")
            .WithAutomaticReconnect()
            .Build();

        _logger.LogInformation("Initializing SignalR Client connecting to {Url}", bffUrl);

        // Fire and forget start
        _ = StartAsync();
    }

    private async Task StartAsync()
    {
        try
        {
            await _connection.StartAsync();
            _logger.LogInformation("Successfully connected to BFF SignalR Hub. Connection State: {State}", _connection.State);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FAILED to connect to BFF SignalR Hub.");
        }
    }

    public async Task NotifyAsync(string correlationId, string agent, string step, string content)
    {
        if (_connection.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("[{CorrelationId}] SignalR NOT connected (State: {State}). Cannot send trace: {Agent} {Step}", correlationId, _connection.State, agent, step);
            return;
        }

        try
        {
            _logger.LogInformation("[{CorrelationId}] Sending trace to Hub: {Agent} ({Step})", correlationId, agent, step);
            await _connection.InvokeAsync("SendTraceFromAgent", correlationId, agent, step, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Error invoking 'SendTraceFromAgent' on BFF Hub", correlationId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
