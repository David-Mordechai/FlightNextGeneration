using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using AiAgents.Shared;

namespace AiAgents.FlightControl;

public class FlightTools
{
    private readonly ILogger<FlightTools> _logger;
    private readonly GeocodingService _geocodingService;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly NotificationService _notifier;

    public FlightTools(ILogger<FlightTools> logger, GeocodingService geocodingService, HttpClient httpClient, IConfiguration configuration, NotificationService notifier)
    {
        _logger = logger;
        _geocodingService = geocodingService;
        _httpClient = httpClient;
        _configuration = configuration;
        _notifier = notifier;
        var bffUrl = _configuration["BffServiceUrl"] ?? "http://bff.service:8080";
        _httpClient.BaseAddress = new Uri(bffUrl);
    }

    private string? GetCorrelationId()
    {
        // For tools, we'd ideally pass the correlation ID through arguments or async local storage.
        // For now, tools will rely on the Agent to report the main steps, 
        // but we can add optional correlationId to tool parameters if needed.
        return null;
    }

    [KernelFunction, Description("Fly the UAV to a named point (e.g., 'Home', 'Target Alpha').")]
    public async Task<string> NavigateTo(
        [Description("The exact name of the destination point."), Required] 
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
                // Straight line
                var json = JsonSerializer.Serialize(new { lat = targetCoords.Value.Lat, lng = targetCoords.Value.Lng });
                var res = await _httpClient.PostAsync("api/mission/target", new StringContent(json, Encoding.UTF8, "application/json"));

                if (!res.IsSuccessStatusCode) return $"Fail to update mission to location {location}.";

                return $"Flying to {location}.";
            }
            else
            {
                // Complex path
                var pathJson = JsonSerializer.Serialize(pathElement);
                var content = new StringContent(pathJson, Encoding.UTF8, "application/json");
                
                await _httpClient.PostAsync("api/mission/path/preview", content);
                await _httpClient.PostAsync("api/mission/path/execute", null);

                return $"Executing optimal route to {location} (avoiding NFZs).";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fail to communicate with the client or services");
            return $"Error: {ex.Message}";
        }
    }

    [KernelFunction, Description("Set the UAV speed in knots.")]
    public async Task<string> ChangeSpeed(
        [Description("Speed in knots."), Required]int speed)
    {
        try
        {
            var json = JsonSerializer.Serialize(new { speed });
            var res = await _httpClient.PostAsync("api/mission/speed", new StringContent(json, Encoding.UTF8, "application/json"));

            if (!res.IsSuccessStatusCode) return $"Fail to update to speed {speed} kts.";
            
            return $"Speed set to {speed} kts.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fail to communicate with the client");
            return "Fail to communicate with the client";
        }
    }

    [KernelFunction, Description("Set the UAV altitude in feet.")]
    public async Task<string> ChangeAltitude(
        [Description("Altitude in feet."), Required] int altitude)
    {
        try
        {
            var json = JsonSerializer.Serialize(new { altitude });
            var res = await _httpClient.PostAsync("api/mission/altitude", new StringContent(json, Encoding.UTF8, "application/json"));

            if (!res.IsSuccessStatusCode) return $"Fail to update to altitude {altitude} feet.";
            
            return $"Altitude set to {altitude} ft.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fail to communicate with the client");
            return "Fail to communicate with the client";
        }
    }
}
