using Microsoft.AspNetCore.SignalR;

namespace Bff.Service.Hubs;

public class FlightHub : Hub
{
    public async Task SendFlightData(string flightId, double latitude, double longitude, double heading, double altitude, double speed)
    {
        await Clients.All.SendAsync("ReceiveFlightData", flightId, latitude, longitude, heading, altitude, speed);
    }

    public async Task ProcessChatMessage(string user, string message)
    {
        // Simply broadcast the message to all clients
        await Clients.All.SendAsync("ReceiveChatMessage", user, message);
        await Clients.All.SendAsync("ReceiveChatMessage", "Mission Control", "Ok");
    }
}
