using Bff.Service.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bff.Service.Controllers;

public record TargetRequest(double Lat, double Lng);
public record SpeedRequest(double Speed);
public record AltitudeRequest(double Altitude);

[ApiController]
[Route("api/mission")]
public class MissionController(FlightStateService flightState) : ControllerBase
{
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
}