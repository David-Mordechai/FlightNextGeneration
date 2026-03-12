using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using AiAgents.Shared;

namespace AiAgents.Payload;

public class PayloadTools
{
    private readonly ILogger<PayloadTools> _logger;
    private readonly GeocodingService _geocodingService;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public PayloadTools(ILogger<PayloadTools> logger, GeocodingService geocodingService, HttpClient httpClient, IConfiguration configuration)
    {
        _logger = logger;
        _geocodingService = geocodingService;
        _httpClient = httpClient;
        _configuration = configuration;
        var bffUrl = _configuration["BffServiceUrl"] ?? "http://bff.service:8080";
        _httpClient.BaseAddress = new Uri(bffUrl);
    }

    [KernelFunction, Description("Direct the UAV's camera gimbal to lock onto a named ground location.")]
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

    [KernelFunction, Description("Reset the UAV's camera gimbal to its default forward-looking scan mode.")]
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
}
