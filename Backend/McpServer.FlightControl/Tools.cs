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
    private readonly IConfiguration _configuration;

    public Tools(ILogger<Tools> logger, GeocodingService geocodingService, HttpClient httpClient, IConfiguration configuration)
    {
        _logger = logger;
        _geocodingService = geocodingService;
        _httpClient = httpClient;
        _configuration = configuration;
        var bffUrl = _configuration["BffServiceUrl"] ?? "http://bff.service:8080";
        _httpClient.BaseAddress = new Uri(bffUrl);
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

    [McpServerTool, Description("Calculate an optimal route to a target avoiding no-fly zones.")]
    public async Task<string> CalculateOptimalPath(
        [Description("The name of the target city or location (e.g., 'Haifa')."), Required] string targetLocation)
    {
        try
        {
            // 1. Get Target Coordinates
            var targetCoords = await _geocodingService.GetCoordinatesAsync(targetLocation);
            if (!targetCoords.HasValue) return $"Could not find location: {targetLocation}";

            // 2. Get Current UAV State
            var stateRes = await _httpClient.GetAsync("api/mission/state");
            if (!stateRes.IsSuccessStatusCode) return "Failed to retrieve UAV state.";
            
            var stateJson = await stateRes.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(stateJson);
            var currentLat = doc.RootElement.GetProperty("lat").GetDouble();
            var currentLng = doc.RootElement.GetProperty("lng").GetDouble();

            // 3. Calculate Path (Call C4I Service)
            // Note: In real world, use Service Discovery. Here hardcoded port.
            var c4iUrl = _configuration["C4IServiceUrl"] ?? "http://c4ientities:8080";
            using var c4iClient = new HttpClient { BaseAddress = new Uri(c4iUrl) };
            var routeReq = new
            {
                StartLat = currentLat,
                StartLng = currentLng,
                EndLat = targetCoords.Value.Lat,
                EndLng = targetCoords.Value.Lng
            };
            
            var routeRes = await c4iClient.PostAsync("api/route/calculate", 
                new StringContent(JsonSerializer.Serialize(routeReq), Encoding.UTF8, "application/json"));
            
            if (!routeRes.IsSuccessStatusCode) return "Failed to calculate route via C4I service.";

            var routeJson = await routeRes.Content.ReadAsStringAsync();
            using var routeDoc = JsonDocument.Parse(routeJson);
            var distance = routeDoc.RootElement.GetProperty("totalDistanceMeters").GetDouble();
            var path = routeDoc.RootElement.GetProperty("path");

            // 4. Broadcast to Frontend (Call Bff Preview)
            // path is already a JsonElement array from C4I response
            var pathJson = JsonSerializer.Serialize(path);
            var content = new StringContent(pathJson, Encoding.UTF8, "application/json");
            
            await _httpClient.PostAsync("api/mission/path/preview", content);

            return $"Optimal route calculated to {targetLocation}. Distance: {distance/1000:F2} km. Path displayed on map. Approve execution?";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating optimal path");
            return $"Error: {ex.Message}";
        }
    }

    [McpServerTool, Description("Execute the previously calculated and previewed flight plan.")]
    public async Task<string> ExecuteFlightPlan()
    {
        try
        {
            var res = await _httpClient.PostAsync("api/mission/path/execute", null);
            if (!res.IsSuccessStatusCode) return "Failed to execute plan. No pending path found or system error.";

            return "Flight plan executed. UAV is proceeding to waypoints.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing flight plan");
            return "Error executing flight plan.";
        }
    }
}