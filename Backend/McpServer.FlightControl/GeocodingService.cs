using System.Text.Json;
using System.Text.Json.Serialization;

namespace McpServer.FlightControl;

public class GeocodingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeocodingService> _logger;
    private readonly string _c4iServiceUrl;

    public GeocodingService(HttpClient httpClient, ILogger<GeocodingService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _c4iServiceUrl = configuration["C4IServiceUrl"] ?? "http://c4ientities:8080";
    }

    public async Task<(double Lat, double Lng)?> GetCoordinatesAsync(string locationName)
    {
        try
        {
            var url = $"{_c4iServiceUrl}/api/points";
            var response = await _httpClient.GetStringAsync(url);
            
            // Deserialize list of Points
            var points = JsonSerializer.Deserialize<List<PointDto>>(response, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (points != null)
            {
                // Simple case-insensitive match
                var match = points.FirstOrDefault(p => p.Name.Equals(locationName, StringComparison.OrdinalIgnoreCase));
                
                // Also check for "Home" or "Target" prefix logic if needed, but "by name" is requested.
                // The user said "fly to home or fly to some target". 
                // "Home" is just a name "Ashdod Home" in my example. 
                // I'll assume exact (case-insensitive) name match for now. 
                // If user says "Home", they might mean the point named "Home".
                
                if (match != null && match.Location?.Coordinates?.Length >= 2)
                {
                    // GeoJSON is [Lng, Lat]
                    var lng = match.Location.Coordinates[0];
                    var lat = match.Location.Coordinates[1];
                    
                    _logger.LogInformation($"Resolved '{locationName}' to {lat}, {lng}");
                    return (lat, lng);
                }
            }

            _logger.LogWarning($"Could not find location: {locationName}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error resolving location: {locationName}");
            return null;
        }
    }

    private class PointDto
    {
        public string Name { get; set; } = string.Empty;
        public LocationDto Location { get; set; }
    }

    private class LocationDto
    {
        public double[] Coordinates { get; set; }
    }
}