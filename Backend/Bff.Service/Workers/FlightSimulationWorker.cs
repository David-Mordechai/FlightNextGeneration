using Bff.Service.Hubs;
using Bff.Service.Services;
using Microsoft.AspNetCore.SignalR;

namespace Bff.Service.Workers;

public class FlightSimulationWorker(
    IHubContext<FlightHub> hubContext,
    ILogger<FlightSimulationWorker> logger,
    FlightStateService state)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Flight Simulation Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            // 1. Update Physics
            state.UpdatePhysics();

            // 2. Add some visual noise/interpolation for display
            var displayAlt = state.CurrentAltitudeFt + 5 * Math.Sin(DateTime.Now.Ticks / 10000000.0);
            var displaySpeed = state.CurrentSpeedKts + 0.5 * Math.Cos(DateTime.Now.Ticks / 5000000.0);
            var heading = state.GetHeading();

            // 3. Broadcast State
            const string flightId = "UAV-Ashdod-01";
            await hubContext.Clients.All.SendAsync(
                "ReceiveFlightData", 
                flightId, 
                state.CurrentLat, 
                state.CurrentLng, 
                heading, 
                displayAlt, 
                displaySpeed, 
                state.TargetLat, 
                state.TargetLng, 
                cancellationToken: stoppingToken
            );

            await Task.Delay(50, stoppingToken);
        }
    }
}