using ModelContextProtocol.Server;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;

namespace McpServer.MissionControl;

[McpServerToolType]
public class Tools
{
    private readonly ILogger<Tools> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public Tools(ILogger<Tools> logger, HttpClient httpClient, IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClient;
        _configuration = configuration;
        var c4iUrl = _configuration["C4IServiceUrl"] ?? "http://c4ientities:8080";
        _httpClient.BaseAddress = new Uri(c4iUrl);
    }

    [McpServerTool, Description("Create a new operational point (Home or Target).")]
    public async Task<string> CreatePoint(
        [Description("Name of the point (e.g., 'Alpha', 'Base')."), Required] string name,
        [Description("Type of point: 'Home' or 'Target'."), Required] string type,
        [Description("Latitude coordinate."), Required] double lat,
        [Description("Longitude coordinate."), Required] double lng)
    {
        try 
        {
            int typeVal = type.Equals("Home", StringComparison.OrdinalIgnoreCase) ? 0 : 1;

            var point = new 
            {
                name = name,
                type = typeVal,
                location = new 
                {
                    type = "Point",
                    coordinates = new[] { lng, lat }
                }
            };

            var json = JsonSerializer.Serialize(point);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var res = await _httpClient.PostAsync("api/points", content);

            if (!res.IsSuccessStatusCode)
                return $"Failed to create point {name}. Status: {res.StatusCode}";

            _logger.LogInformation("Created point {Name} at {Lat}, {Lng}", name, lat, lng);
            return $"Successfully created point '{name}' ({type}) at {lat}, {lng}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating point");
            return $"Error: {ex.Message}";
        }
    }

    [McpServerTool, Description("List all active points.")]
    public async Task<string> ListPoints()
    {
        try
        {
            var res = await _httpClient.GetAsync("api/points");
            if (!res.IsSuccessStatusCode) return "Failed to list points.";

            var json = await res.Content.ReadAsStringAsync();
            return $"Active Points: {json}"; 
        }
        catch (Exception ex)
        {
             return $"Error: {ex.Message}";
        }
    }

    [McpServerTool, Description("Delete a point by its name.")]
    public async Task<string> DeletePointByName(
        [Description("The name of the point to delete."), Required] string name)
    {
        try
        {
            var res = await _httpClient.GetAsync("api/points");
            if (!res.IsSuccessStatusCode) return "Failed to retrieve points for lookup.";
            
            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            
            string? idToDelete = null;
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.GetProperty("name").GetString()?.Equals(name, StringComparison.OrdinalIgnoreCase) == true)
                {
                    idToDelete = element.GetProperty("id").GetString();
                    break;
                }
            }

            if (idToDelete == null) return $"Point with name '{name}' not found.";

            var delRes = await _httpClient.DeleteAsync($"api/points/{idToDelete}");
            if (!delRes.IsSuccessStatusCode) return $"Failed to delete point '{name}'.";

            return $"Successfully deleted point '{name}'.";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
