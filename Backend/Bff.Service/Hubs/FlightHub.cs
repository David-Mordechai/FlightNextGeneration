using Bff.Service.Services;
using Microsoft.AspNetCore.SignalR;

namespace Bff.Service.Hubs;

public class FlightHub(AiChatService aiChatService, ILogger<FlightHub> logger, IServiceProvider serviceProvider) : Hub
{
    private NotificationService NotificationService => serviceProvider.GetRequiredService<NotificationService>();

    public async Task SendFlightData(string flightId, double latitude, double longitude, double heading, double altitude, double speed)
    {
        await Clients.All.SendAsync("ReceiveFlightData", flightId, latitude, longitude, heading, altitude, speed);
    }

    public async Task<bool> CheckAiStatus()
    {
        var isReady = await aiChatService.CheckReadinessAsync();
        return isReady;
    }

    public async Task ProcessChatMessage(string user, string message, string? correlationId = null)
    {
        Console.WriteLine($"[FlightHub] ProcessChatMessage: {user}, {message}, {correlationId}");
        
        // Broadcast the user's message immediately with the correlationId
        await NotificationService.NotifyChatMessage(user, message, null, correlationId);

        // Process message through agentic system
        var aiResponse = await aiChatService.ProcessUserMessage(message, correlationId);
        
        // Broadcast final response with the SAME correlationId
        await NotificationService.NotifyChatMessage("Mission Control", aiResponse.Response, aiResponse.Duration, aiResponse.CorrelationId);
    }

    // New method for agents to send traces to the BFF Hub
    public async Task SendTraceFromAgent(string correlationId, string agent, string step, string content)
    {
        Console.WriteLine($"[FlightHub] SendTraceFromAgent: {correlationId}, {agent}, {step}");
        await NotificationService.NotifyAiTrace(correlationId, agent, step, content);
    }
}
