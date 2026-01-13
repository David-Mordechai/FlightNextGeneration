using System.Text.Json;
using System.Text.Json.Serialization;

namespace McpServer.FlightControl;

public class GeocodingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeocodingService> _logger;

    public GeocodingService(HttpClient httpClient, ILogger<GeocodingService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        // Nominatim requires a User-Agent
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "SkyLab-UAV-Mission-Control");
    }

    public async Task<(double Lat, double Lng)?> GetCoordinatesAsync(string locationName)
    {
        try
        {
            var url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(locationName)}&format=json&limit=1";
            var response = await _httpClient.GetStringAsync(url);
            var results = JsonSerializer.Deserialize<List<NominatimResult>>(response);

            if (results is { Count: > 0 })
            {
                var result = results[0];
                if (double.TryParse(result.Lat, out var lat) && double.TryParse(result.Lon, out var lon))
                {
                    _logger.LogInformation($"Geocoded '{locationName}' to {lat}, {lon}");
                    return (lat, lon);
                }
            }

            _logger.LogWarning($"Could not geocode location: {locationName}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error geocoding location: {locationName}");
            return null;
        }
    }

    private class NominatimResult
    {
        [JsonPropertyName("lat")]
        public string Lat { get; set; } = string.Empty;
        [JsonPropertyName("lon")]
        public string Lon { get; set; } = string.Empty;
    }
}