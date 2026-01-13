using ModelContextProtocol.Server;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;

namespace McpServer.FlightControl;

[McpServerToolType]
public class Tools
{
    private readonly ILogger<Tools> _logger;
    private readonly GeocodingService _geocodingService;
    private readonly HttpClient _httpClient;

    public Tools(ILogger<Tools> logger, GeocodingService geocodingService, HttpClient httpClient)
    {
        _logger = logger;
        _geocodingService = geocodingService;
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("http://localhost:5066");
    }

    [McpServerTool, Description("Navigate the UAV to a specific city or location.")]
    public async Task<string> NavigateTo(
        [Description("The name of the city or location to fly to (e.g., 'Tel Aviv', 'Haifa')."), Required] 
        string location)
    {
        try
        {
            var coordinates = await _geocodingService.GetCoordinatesAsync(location);
            if (!coordinates.HasValue)
            {
                _logger.LogWarning("Failed to get coordinates for {Location}.", location);
                return $"Could not find coordinates for {location}.";
            }

            var json = JsonSerializer.Serialize(new
            {
                lat = coordinates.Value.Lat,
                lng = coordinates.Value.Lng
            });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var res = await _httpClient.PostAsync("api/mission/target", content);

            if (!res.IsSuccessStatusCode)
                return $"Fail to update mission to location {location}.";

            _logger.LogInformation("Coordinates for {Location} are Lat: {Latitude}, Lon: {Longitude}.", location,
                coordinates.Value.Lat, coordinates.Value.Lng);
            return $"Mission updated! Flying to {location}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fail to communicate with the client");
            return "Fail to communicate with the client";
        }
    }

    [McpServerTool, Description("Change the UAV's target speed in knots.")]
    public async Task<string> ChangeSpeed(
        [Description("Target speed in knots (e.g., 150)."), Required]int speed)
    {
        try
        {
            var json = JsonSerializer.Serialize(new
            {
                speed
            });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var res = await _httpClient.PostAsync("api/mission/speed", content);

            if (!res.IsSuccessStatusCode)
                return $"Fail to update to speed {speed} kts.";
            
            _logger.LogInformation("Acknowledged. Adjusting speed to {Speed} kts.", speed);
            return $"Acknowledged. Adjusting speed to {speed} kts.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fail to communicate with the client");
            return "Fail to communicate with the client";
        }
    }

    [McpServerTool, Description("Change the UAV's target altitude in feet.")]
    public async Task<string> ChangeAltitude(
        [Description("Target altitude in feet (e.g., 5000)."), Required] int altitude)
    {
        try
        {
            var json = JsonSerializer.Serialize(new
            {
                altitude
            });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var res = await _httpClient.PostAsync("api/mission/altitude", content);

            if (!res.IsSuccessStatusCode)
                return $"Fail to update to altitude {altitude} feet.";
            
            _logger.LogInformation("Acknowledged. Changing altitude to {Altitude} ft.", altitude);
            return $"Acknowledged. Changing altitude to {altitude} feet.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fail to communicate with the client");
            return "Fail to communicate with the client";
        }
    }
}