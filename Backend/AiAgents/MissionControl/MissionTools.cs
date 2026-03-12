using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using AiAgents.Shared;

namespace AiAgents.MissionControl;

public class MissionTools
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MissionTools> _logger;
    private readonly string _c4iUrl;
    private readonly NotificationService _notifier;

    public MissionTools(HttpClient httpClient, ILogger<MissionTools> logger, IConfiguration configuration, NotificationService notifier)
    {
        _httpClient = httpClient;
        _logger = logger;
        _notifier = notifier;
        _c4iUrl = configuration["C4IServiceUrl"] ?? "http://c4ientities:8080";
    }

    private async Task NotifyBff(string entityType, string changeType, object data)
    {
        try
        {
            var update = new { EntityType = entityType, ChangeType = changeType, Data = data };
            await _httpClient.PostAsJsonAsync("http://bff.service:8080/api/notifications/entity-update", update);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify BFF of entity update.");
        }
    }

    [KernelFunction, Description("Create a new named operational point (Target or Home).")]
    public async Task<string> CreatePoint(
        [Description("Name of the point")] string name, 
        [Description("Type of point: 0 for Home, 1 for Target")] int typeVal, 
        [Description("Latitude")] double lat, 
        [Description("Longitude")] double lng)
    {
        try
        {
            var pointDto = new { name, type = typeVal, lat, lng };
            var res = await _httpClient.PostAsJsonAsync($"{_c4iUrl}/api/points", pointDto);
            if (!res.IsSuccessStatusCode) return "Failed to create point in database.";

            var createdPoint = await res.Content.ReadFromJsonAsync<JsonElement>();
            await NotifyBff("Point", "Created", createdPoint);

            return $"Successfully created point '{name}' at {lat}, {lng}.";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [KernelFunction, Description("Delete a point by its exact name.")]
    public async Task<string> DeletePointByName([Description("Name of the point to delete")] string name)
    {
        try
        {
            var pointsRes = await _httpClient.GetAsync($"{_c4iUrl}/api/points");
            if (!pointsRes.IsSuccessStatusCode) return "Failed to fetch points.";

            var points = await pointsRes.Content.ReadFromJsonAsync<List<JsonElement>>();
            var pointToDelete = points?.FirstOrDefault(p => p.GetProperty("name").GetString()?.Equals(name, StringComparison.OrdinalIgnoreCase) ?? false);

            if (pointToDelete == null) return $"Point '{name}' not found.";

            var idToDelete = pointToDelete.Value.GetProperty("id").GetGuid();
            var delRes = await _httpClient.DeleteAsync($"{_c4iUrl}/api/points/{idToDelete}");
            if (!delRes.IsSuccessStatusCode) return $"Failed to delete point '{name}'.";

            await NotifyBff("Point", "Deleted", new { Id = idToDelete });

            return $"Successfully deleted point '{name}'.";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [KernelFunction, Description("List all operational points on the map.")]
    public async Task<string> ListPoints()
    {
        try
        {
            var res = await _httpClient.GetAsync($"{_c4iUrl}/api/points");
            if (!res.IsSuccessStatusCode) return "Failed to fetch points.";
            return await res.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [KernelFunction, Description("Create a rectangular No-Fly Zone.")]
    public async Task<string> CreateRectangleZone(string name, double minLat, double minLng, double maxLat, double maxLng, double minAlt = 0, double maxAlt = 10000)
    {
        try
        {
            var zoneDto = new
            {
                Name = name,
                MinLat = minLat,
                MinLng = minLng,
                MaxLat = maxLat,
                MaxLng = maxLng,
                MinAltitude = minAlt,
                MaxAltitude = maxAlt,
                IsActive = true
            };

            var res = await _httpClient.PostAsJsonAsync($"{_c4iUrl}/api/noflyzones/rectangle", zoneDto);
            if (!res.IsSuccessStatusCode) return "Failed to create no-fly zone.";

            var createdZone = await res.Content.ReadFromJsonAsync<JsonElement>();
            await NotifyBff("NoFlyZone", "Created", createdZone);

            return $"Successfully created rectangular No-Fly Zone '{name}'.";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [KernelFunction, Description("Delete a No-Fly Zone by name.")]
    public async Task<string> DeleteNoFlyZoneByName(string name)
    {
        try
        {
            var zonesRes = await _httpClient.GetAsync($"{_c4iUrl}/api/noflyzones");
            if (!zonesRes.IsSuccessStatusCode) return "Failed to fetch zones.";

            var zones = await zonesRes.Content.ReadFromJsonAsync<List<JsonElement>>();
            var zoneToDelete = zones?.FirstOrDefault(z => z.GetProperty("name").GetString()?.Equals(name, StringComparison.OrdinalIgnoreCase) ?? false);

            if (zoneToDelete == null) return $"Zone '{name}' not found.";

            var idToDelete = zoneToDelete.Value.GetProperty("id").GetGuid();
            var delRes = await _httpClient.DeleteAsync($"{_c4iUrl}/api/noflyzones/{idToDelete}");
            if (!delRes.IsSuccessStatusCode) return $"Failed to delete zone '{name}'.";

            await NotifyBff("NoFlyZone", "Deleted", new { Id = idToDelete });

            return $"Successfully deleted zone '{name}'.";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
