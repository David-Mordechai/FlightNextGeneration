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

    [McpServerTool, Description("Command the UAV to fly to an EXISTING named point on the map. This tool is for flight control. Automatically calculates optimal path if obstacles (No-Fly Zones) are present.")]
    public async Task<string> NavigateTo(
        [Description("The name of an already defined point to fly to (e.g., 'Home', 'Target Alpha')."), Required] 
        string location)
    {
        try
        {
            // 1. Get Target Coordinates
            var targetCoords = await _geocodingService.GetCoordinatesAsync(location);
            if (!targetCoords.HasValue)
            {
                _logger.LogWarning("Failed to get coordinates for {Location}.", location);
                return $"Could not find coordinates for {location}.";
            }

            // 2. Get Current UAV State
            var stateRes = await _httpClient.GetAsync("api/mission/state");
            if (!stateRes.IsSuccessStatusCode) return "Failed to retrieve UAV state.";
            
            var stateJson = await stateRes.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(stateJson);
            var currentLat = doc.RootElement.GetProperty("lat").GetDouble();
            var currentLng = doc.RootElement.GetProperty("lng").GetDouble();
            var currentAlt = doc.RootElement.GetProperty("altitude").GetDouble();

            // 3. Calculate Path (Call C4I Service)
            var c4IUrl = _configuration["C4IServiceUrl"] ?? "http://c4ientities:8080";
            using var c4IClient = new HttpClient();
            c4IClient.BaseAddress = new Uri(c4IUrl);
            var routeReq = new
            {
                StartLat = currentLat,
                StartLng = currentLng,
                EndLat = targetCoords.Value.Lat,
                EndLng = targetCoords.Value.Lng,
                AltitudeFt = currentAlt
            };
            
            var routeRes = await c4IClient.PostAsync("api/route/calculate", 
                new StringContent(JsonSerializer.Serialize(routeReq), Encoding.UTF8, "application/json"));
            
            if (!routeRes.IsSuccessStatusCode) return "Failed to calculate route via C4I service.";

            var routeJson = await routeRes.Content.ReadAsStringAsync();
            using var routeDoc = JsonDocument.Parse(routeJson);
            var pathElement = routeDoc.RootElement.GetProperty("path");
            var pointCount = pathElement.GetArrayLength();

            if (pointCount <= 2)
            {
                // Straight line - Use direct navigation
                var json = JsonSerializer.Serialize(new
                {
                    lat = targetCoords.Value.Lat,
                    lng = targetCoords.Value.Lng
                });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var res = await _httpClient.PostAsync("api/mission/target", content);

                if (!res.IsSuccessStatusCode)
                    return $"Fail to update mission to location {location}.";

                // AUTO-LOCK SENSOR (Implicit PointPayload)
                // We want the camera to look at the destination while flying
                await PointPayload(location);

                _logger.LogInformation("Path clear. Flying directly to {Location} (Lat: {Latitude}, Lon: {Longitude}). Sensor locked.", location,
                    targetCoords.Value.Lat, targetCoords.Value.Lng);
                return $"Path clear. Flying directly to {location}. Sensor locked on target.";
            }
            else
            {
                // Complex path - Preview and Execute
                var pathJson = JsonSerializer.Serialize(pathElement);
                var content = new StringContent(pathJson, Encoding.UTF8, "application/json");
                
                // Preview
                var previewRes = await _httpClient.PostAsync("api/mission/path/preview", content);
                if (!previewRes.IsSuccessStatusCode) return "Failed to preview optimal path.";

                // Execute
                var execRes = await _httpClient.PostAsync("api/mission/path/execute", null);
                if (!execRes.IsSuccessStatusCode) return "Failed to execute optimal path.";

                // AUTO-LOCK SENSOR
                await PointPayload(location);

                _logger.LogInformation("Obstacles detected. Optimal route calculated and executing to {Location}. Sensor locked.", location);
                return $"Obstacles detected (No-Fly Zones). optimal route calculated and executing to {location}. Sensor locked on target.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fail to communicate with the client or services");
            return $"Error: {ex.Message}";
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

    [McpServerTool, Description("Direct the UAV's camera gimbal to lock onto a named ground location.")]
    public async Task<string> PointPayload(
        [Description("The name of the location to point the camera at."), Required] string location)
    {
        try
        {
            var targetCoords = await _geocodingService.GetCoordinatesAsync(location);
            if (!targetCoords.HasValue) return $"Could not find coordinates for {location}.";

            var json = JsonSerializer.Serialize(new
            {
                lat = targetCoords.Value.Lat,
                lng = targetCoords.Value.Lng,
                alt = targetCoords.Value.Alt
            });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var res = await _httpClient.PostAsync("api/mission/payload/point", content);

            if (!res.IsSuccessStatusCode) return $"Fail to point camera at {location}.";

            _logger.LogInformation("Camera gimbal locked to {Location} (Alt: {Alt}m).", location, targetCoords.Value.Alt);
            return $"Camera gimbal locked to {location}. Sensor footprint updated on map.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fail to communicate with the service");
            return "Fail to communicate with the service";
        }
    }

    [McpServerTool, Description("Reset the UAV's camera gimbal to its default forward-looking scan mode.")]
    public async Task<string> ResetPayload()
    {
        try
        {
            var res = await _httpClient.PostAsync("api/mission/payload/reset", null);
            if (!res.IsSuccessStatusCode) return "Fail to reset camera gimbal.";

            _logger.LogInformation("Camera gimbal reset to scan mode.");
            return "Camera gimbal reset to default scan mode.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fail to communicate with the service");
            return "Fail to communicate with the service";
        }
    }

    // [McpServerTool, Description("Move the main map camera to look at a specific named location.")]
    // public async Task<string> LookAt(
    //     [Description("The name of the location to focus the camera on."), Required] string location)
    // {
    //     // Tool disabled per user request to restrict map camera control.
    //     return "Map camera control is disabled.";
    // }
}