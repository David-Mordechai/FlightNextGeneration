using Bff.Service.Services;
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.SignalR;
using Bff.Service.Hubs;

namespace Bff.Service.Controllers;

public record TargetRequest(double Lat, double Lng);
public record SpeedRequest(double Speed);
public record AltitudeRequest(double Altitude);
public record PayloadPointRequest(double Lat, double Lng, double Alt = 0);
public record CameraFocusRequest(double Lat, double Lng);
public record RouteData(object Path, double Distance);

[ApiController]
[Route("api/mission")]
public class MissionController(FlightStateService flightState, IHubContext<FlightHub> hubContext, AiChatService aiChatService) : ControllerBase
{
    [HttpPost("camera/focus")]
    public async Task<IActionResult> FocusCamera([FromBody] CameraFocusRequest request)
    {
        await hubContext.Clients.All.SendAsync("FocusCamera", request.Lat, request.Lng);
        return Ok(new { Message = $"Camera focused on {request.Lat}, {request.Lng}" });
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] string message)
    {
        var result = await aiChatService.ProcessUserMessage(message);
        return Ok(result);
    }

    [HttpPost("payload/point")]
    public IActionResult PointPayload([FromBody] PayloadPointRequest request)
    {
        flightState.PointPayload(request.Lat, request.Lng, request.Alt);
        return Ok(new { Message = "Sensor locked to target location." });
    }

    [HttpPost("payload/reset")]
    public IActionResult ResetPayload()
    {
        flightState.ResetPayload();
        return Ok(new { Message = "Sensor reset to default scan mode." });
    }

    [HttpPost("target")]
    public IActionResult SetTarget([FromBody] TargetRequest request)
    {
        if (request is { Lat: 0, Lng: 0 })
            return BadRequest("Valid Lat/Lng coordinates are required.");
        
        flightState.SetNewDestination(request.Lat, request.Lng);
        return Ok(new { Message = $"Mission updated to {request.Lat}, {request.Lng}", request.Lat, request.Lng });
    }

    [HttpPost("speed")]
    public IActionResult SetSpeed([FromBody] SpeedRequest request)
    {
        if (request.Speed is <= 0 or > 500)
            return BadRequest("Invalid speed range (1-500 kts).");

        flightState.SetSpeed(request.Speed);
        return Ok(new { Message = $"Target speed set to {request.Speed} kts" });
    }

    [HttpPost("altitude")]
    public IActionResult SetAltitude([FromBody] AltitudeRequest request)
    {
        if (request.Altitude is < 0 or > 60000)
            return BadRequest("Invalid altitude range (0-60000 ft).");

        flightState.SetAltitude(request.Altitude);
        return Ok(new { Message = $"Target altitude set to {request.Altitude} ft" });
    }

    [HttpGet("state")]
    public IActionResult GetState()
    {
        var state = flightState.CurrentState;
        return Ok(state);
    }

    [HttpPost("route")]
    public async Task<IActionResult> BroadcastRoute([FromBody] RouteData route)
    {
        await hubContext.Clients.All.SendAsync("RouteCalculated", route.Path);
        return Ok(new { Message = "Route broadcasted to clients." });
    }

    [HttpPost("path/preview")]
    public async Task<IActionResult> PreviewPath([FromBody] List<GeoPoint> path)
    {
        // Save to Pending (3D)
        flightState.SetPendingPath(path.Select(p => (p.Lat, p.Lng, p.AltitudeFt)).ToList());
        
        // Broadcast for Viz
        await hubContext.Clients.All.SendAsync("RouteCalculated", path);
        
        return Ok(new { Message = "Path set as pending preview." });
    }

    [HttpPost("path/execute")]
    public IActionResult ExecutePath()
    {
        var success = flightState.ExecutePendingPath();
        if (!success) return BadRequest("No pending path to execute.");
        
        return Ok(new { Message = "Executing flight plan." });
    }
}

public class GeoPoint
{
    public double Lat { get; set; }
    public double Lng { get; set; }
    public double AltitudeFt { get; set; }
}