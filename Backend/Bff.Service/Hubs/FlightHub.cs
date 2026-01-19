using Bff.Service.Services;
using Microsoft.AspNetCore.SignalR;

namespace Bff.Service.Hubs;

public class FlightHub(AiChatService aiChatService, ILogger<FlightHub> logger) : Hub
{
    public async Task SendFlightData(string flightId, double latitude, double longitude, double heading, double altitude, double speed)
    {
        await Clients.All.SendAsync("ReceiveFlightData", flightId, latitude, longitude, heading, altitude, speed);
    }

    public async Task ProcessChatMessage(string user, string message)
    {
        // Simply broadcast the message to all clients
        await Clients.All.SendAsync("ReceiveChatMessage", user, message, null); // Pass null for duration for user messages

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await aiChatService.ProcessUserMessage(message);
        stopwatch.Stop();
        
        var durationSeconds = stopwatch.Elapsed.TotalSeconds;
        logger.LogInformation("AI Request processed in {Duration} seconds", durationSeconds);
        
        await Clients.All.SendAsync("ReceiveChatMessage", "Mission Control", result, durationSeconds);
    }
}
