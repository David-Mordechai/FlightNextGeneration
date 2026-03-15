using Bff.Service.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Bff.Service.Services;

public class NotificationService(IHubContext<FlightHub> hubContext, ILogger<NotificationService> logger)
{
    public async Task NotifyAiTrace(string correlationId, string agent, string step, string content, double? duration = null)
    {
        try
        {
            logger.LogInformation("[{CorrelationId}] [SignalR] Broadcasting TRACE for {Agent}: {Step} (Duration: {Duration})", correlationId, agent, step, duration);
            await hubContext.Clients.All.SendAsync("ReceiveAiTrace", correlationId, agent, step, content, duration);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to broadcast AI trace via SignalR");
        }
    }

    public async Task NotifyChatMessage(string user, string message, double? duration = null, string? correlationId = null)
    {
        try
        {
            logger.LogInformation("[{CorrelationId}] [SignalR] Broadcasting CHAT from {User}", correlationId ?? "N/A", user);
            await hubContext.Clients.All.SendAsync("ReceiveChatMessage", user, message, duration, correlationId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to broadcast chat message via SignalR");
        }
    }
}
