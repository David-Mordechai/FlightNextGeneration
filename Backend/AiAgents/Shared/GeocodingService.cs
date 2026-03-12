using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiAgents.Shared;

public class GeocodingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeocodingService> _logger;
    private readonly string _c4iUrl;

    public GeocodingService(HttpClient httpClient, ILogger<GeocodingService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _c4iUrl = configuration["C4IServiceUrl"] ?? "http://c4ientities:8080";
    }

    public async Task<(double Lat, double Lng, double Alt)?> GetCoordinatesAsync(string locationName)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_c4iUrl}/api/points");
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            var points = JsonSerializer.Deserialize<List<PointDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var match = points?.FirstOrDefault(p => p.Name.Equals(locationName, StringComparison.OrdinalIgnoreCase));
            if (match != null && match.Location != null && match.Location.Coordinates != null && match.Location.Coordinates.Length >= 2)
            {
                return (match.Location.Coordinates[1], match.Location.Coordinates[0], 0);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving location: {LocationName}", locationName);
            return null;
        }
    }

    private class PointDto
    {
        public string Name { get; set; } = string.Empty;
        public LocationDto? Location { get; set; }
    }

    private class LocationDto
    {
        public double[]? Coordinates { get; set; }
    }
}
