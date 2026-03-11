using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;

namespace Agents.MissionControl;

public class MissionTools
{
    private readonly ILogger<MissionTools> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly string _c4iUrl;
    private readonly string _bffUrl;

    public MissionTools(ILogger<MissionTools> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _c4iUrl = _configuration["C4IServiceUrl"] ?? "http://c4ientities:8080";
        _bffUrl = _configuration["BffServiceUrl"] ?? "http://bff.service:8080";
    }

    private async Task NotifyBff(string entityType, string changeType, object data)
    {
        try 
        {
            using var client = _httpClientFactory.CreateClient();
            var payload = new { EntityType = entityType, ChangeType = changeType, Data = data };
            var json = JsonSerializer.Serialize(payload);
            await client.PostAsync($"{_bffUrl}/api/notifications/entity-update", 
                new StringContent(json, Encoding.UTF8, "application/json"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify BFF");
        }
    }

    [KernelFunction, Description("Define a new persistent operational point (Home or Target) on the map. REQUIRES explicit Latitude and Longitude from the user. Do NOT use this tool if the user did not provide coordinates.")]
    public async Task<string> CreatePoint(
        [Description("Name of the point (e.g., 'Alpha', 'Base')."), Required] string name,
        [Description("Type of point: 'Home' or 'Target'."), Required] string type,
        [Description("Latitude coordinate."), Required] double lat,
        [Description("Longitude coordinate."), Required] double lng)
    {
        try 
        {
            using var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_c4iUrl);

            // 1. Check if a point with this name already exists
            var listRes = await client.GetAsync("api/points");
            if (!listRes.IsSuccessStatusCode) return "Failed to retrieve existing points for validation.";

            var listJson = await listRes.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(listJson);
            
            string? existingId = null;
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.GetProperty("name").GetString()?.Equals(name, StringComparison.OrdinalIgnoreCase) == true)
                {
                    existingId = element.GetProperty("id").GetString();
                    break;
                }
            }

            int typeVal = type.Equals("Home", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            var pointDto = new { name, type = typeVal, lat, lng };
            var json = JsonSerializer.Serialize(pointDto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage res;
            string actionType;

            if (existingId != null)
            {
                // UPDATE existing
                res = await client.PutAsync($"api/points/{existingId}", content);
                actionType = "Updated";
            }
            else
            {
                // CREATE new
                res = await client.PostAsync("api/points", content);
                actionType = "Created";
            }

            if (!res.IsSuccessStatusCode)
                return $"Failed to {actionType.ToLower()} point {name}. Status: {res.StatusCode}";

            // If it's a PUT, we need to fetch the updated point to notify the BFF, as PUT returns NoContent
            JsonElement finalPoint;
            if (existingId != null)
            {
                var fetchRes = await client.GetAsync($"api/points");
                var fetchJson = await fetchRes.Content.ReadAsStringAsync();
                using var fetchDoc = JsonDocument.Parse(fetchJson);
                finalPoint = fetchDoc.RootElement.EnumerateArray().First(e => e.GetProperty("id").GetString() == existingId).Clone();
            }
            else
            {
                var createdJson = await res.Content.ReadAsStringAsync();
                finalPoint = JsonSerializer.Deserialize<JsonElement>(createdJson);
            }

            await NotifyBff("Point", actionType, finalPoint);
            _logger.LogInformation("{ActionType} point {Name} at {Lat}, {Lng}", actionType, name, lat, lng);
            return $"Successfully {actionType.ToLower()} point '{name}' ({type}) at {lat}, {lng}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/updating point");
            return $"Error: {ex.Message}";
        }
    }

    [KernelFunction, Description("Define a new rectangular No-Fly Zone on the map. This tool is for map management only.")]
    public async Task<string> CreateRectangleZone(
        [Description("Name of the zone."), Required] string name,
        [Description("Minimum latitude."), Required] double minLat,
        [Description("Minimum longitude."), Required] double minLng,
        [Description("Maximum latitude."), Required] double maxLat,
        [Description("Maximum longitude."), Required] double maxLng,
        [Description("Minimum altitude in feet.")] double minAlt = 0,
        [Description("Maximum altitude in feet.")] double maxAlt = 10000)
    {
        try 
        {
            using var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_c4iUrl);

            // Check if zone with this name already exists
            var listRes = await client.GetAsync("api/noflyzones");
            if (!listRes.IsSuccessStatusCode) return "Failed to retrieve existing zones for validation.";
            
            var listJson = await listRes.Content.ReadAsStringAsync();
            using var listDoc = JsonDocument.Parse(listJson);
            string? existingId = null;
            foreach (var element in listDoc.RootElement.EnumerateArray())
            {
                if (element.GetProperty("name").GetString()?.Equals(name, StringComparison.OrdinalIgnoreCase) == true)
                {
                    existingId = element.GetProperty("id").GetString();
                    break;
                }
            }

            var coordinates = new[] {
                new[] { minLng, minLat }, // SW
                new[] { maxLng, minLat }, // SE
                new[] { maxLng, maxLat }, // NE
                new[] { minLng, maxLat }, // NW
                new[] { minLng, minLat }  // Close loop
            };

            var zoneId = existingId != null ? Guid.Parse(existingId) : Guid.NewGuid();
            var zone = new 
            {
                id = zoneId,
                name,
                minAltitude = minAlt,
                maxAltitude = maxAlt,
                isActive = true,
                geometry = new 
                {
                    type = "Polygon",
                    coordinates = new[] { coordinates }
                }
            };

            var json = JsonSerializer.Serialize(zone);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            HttpResponseMessage res;
            string actionType;

            if (existingId != null)
            {
                res = await client.PutAsync($"api/noflyzones/{existingId}", content);
                actionType = "Updated";
            }
            else
            {
                res = await client.PostAsync("api/noflyzones", content);
                actionType = "Created";
            }

            if (!res.IsSuccessStatusCode)
                return $"Failed to {actionType.ToLower()} rectangle zone {name}. Status: {res.StatusCode}";

            // Notify BFF
            JsonElement finalZone;
            if (existingId != null)
            {
                var fetchRes = await client.GetAsync("api/noflyzones");
                var fetchJson = await fetchRes.Content.ReadAsStringAsync();
                using var fetchDoc = JsonDocument.Parse(fetchJson);
                finalZone = fetchDoc.RootElement.EnumerateArray().First(e => e.GetProperty("id").GetString() == existingId).Clone();
            }
            else
            {
                var createdJson = await res.Content.ReadAsStringAsync();
                finalZone = JsonSerializer.Deserialize<JsonElement>(createdJson);
            }

            await NotifyBff("NoFlyZone", actionType, finalZone);
            return $"Successfully {actionType.ToLower()} Rectangle No-Fly Zone '{name}' from ({minLat}, {minLng}) to ({maxLat}, {maxLng}).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/updating rectangle zone");
            return $"Error: {ex.Message}";
        }
    }

    [KernelFunction, Description("Define a new polygon No-Fly Zone on the map from a list of coordinates. This tool is for map management only.")]
    public async Task<string> CreatePolygonZone(
        [Description("Name of the zone."), Required] string name,
        [Description("Coordinates as an array of [lng, lat] pairs. Example: [[34.1, 31.1], [34.2, 31.1], [34.2, 31.2], [34.1, 31.1]]"), Required] double[][] coordinates,
        [Description("Minimum altitude in feet.")] double minAlt = 0,
        [Description("Maximum altitude in feet.")] double maxAlt = 10000)
    {
        try 
        {
            using var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_c4iUrl);

            // Check if zone with this name already exists
            var listRes = await client.GetAsync("api/noflyzones");
            if (!listRes.IsSuccessStatusCode) return "Failed to retrieve existing zones for validation.";
            
            var listJson = await listRes.Content.ReadAsStringAsync();
            using var listDoc = JsonDocument.Parse(listJson);
            string? existingId = null;
            foreach (var element in listDoc.RootElement.EnumerateArray())
            {
                if (element.GetProperty("name").GetString()?.Equals(name, StringComparison.OrdinalIgnoreCase) == true)
                {
                    existingId = element.GetProperty("id").GetString();
                    break;
                }
            }

            if (coordinates.Length < 3) return "Polygon must have at least 3 points.";
            var coordList = coordinates.ToList();
            if (coordList[0][0] != coordList[^1][0] || coordList[0][1] != coordList[^1][1])
            {
                coordList.Add(coordList[0]);
            }

            var zoneId = existingId != null ? Guid.Parse(existingId) : Guid.NewGuid();
            var zone = new 
            {
                id = zoneId,
                name,
                minAltitude = minAlt,
                maxAltitude = maxAlt,
                isActive = true,
                geometry = new 
                {
                    type = "Polygon",
                    coordinates = new[] { coordList.ToArray() }
                }
            };

            var json = JsonSerializer.Serialize(zone);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            HttpResponseMessage res;
            string actionType;

            if (existingId != null)
            {
                res = await client.PutAsync($"api/noflyzones/{existingId}", content);
                actionType = "Updated";
            }
            else
            {
                res = await client.PostAsync("api/noflyzones", content);
                actionType = "Created";
            }

            if (!res.IsSuccessStatusCode)
                return $"Failed to {actionType.ToLower()} polygon zone {name}. Status: {res.StatusCode}";

            JsonElement finalZone;
            if (existingId != null)
            {
                var fetchRes = await client.GetAsync("api/noflyzones");
                var fetchJson = await fetchRes.Content.ReadAsStringAsync();
                using var fetchDoc = JsonDocument.Parse(fetchJson);
                finalZone = fetchDoc.RootElement.EnumerateArray().First(e => e.GetProperty("id").GetString() == existingId).Clone();
            }
            else
            {
                var createdJson = await res.Content.ReadAsStringAsync();
                finalZone = JsonSerializer.Deserialize<JsonElement>(createdJson);
            }

            await NotifyBff("NoFlyZone", actionType, finalZone);
            return $"Successfully {actionType.ToLower()} Polygon No-Fly Zone '{name}' with {coordList.Count} points.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/updating polygon zone");
            return $"Error: {ex.Message}";
        }
    }

    [KernelFunction, Description("List all active points.")]
    public async Task<string> ListPoints()
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_c4iUrl);
            var res = await client.GetAsync("api/points");
            if (!res.IsSuccessStatusCode) return "Failed to list points.";

            var json = await res.Content.ReadAsStringAsync();
            return $"Active Points: {json}"; 
        }
        catch (Exception ex)
        {
             return $"Error: {ex.Message}";
        }
    }

    [KernelFunction, Description("Delete ALL active points.")]
    public async Task<string> DeleteAllPoints()
    {
        try
        {
            using var listClient = _httpClientFactory.CreateClient();
            listClient.BaseAddress = new Uri(_c4iUrl);
            var res = await listClient.GetAsync("api/points");
            if (!res.IsSuccessStatusCode) return "Failed to retrieve points list.";
            
            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            
            int count = 0;
            var ids = new List<string>();

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                ids.Add(element.GetProperty("id").GetString()!);
            }

            using var deleteClient = _httpClientFactory.CreateClient();
            deleteClient.BaseAddress = new Uri(_c4iUrl);

            foreach (var id in ids)
            {
                var delRes = await deleteClient.DeleteAsync($"api/points/{id}");
                if (delRes.IsSuccessStatusCode)
                {
                    count++;
                    await NotifyBff("Point", "Deleted", new { Id = id });
                }
            }

            return $"Successfully deleted {count} points.";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [KernelFunction, Description("Delete a point by its name.")]
    public async Task<string> DeletePointByName(
        [Description("The name of the point to delete."), Required] string name)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_c4iUrl);

            var res = await client.GetAsync("api/points");
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

            var delRes = await client.DeleteAsync($"api/points/{idToDelete}");
            if (!delRes.IsSuccessStatusCode) return $"Failed to delete point '{name}'.";

            await NotifyBff("Point", "Deleted", new { Id = idToDelete });

            return $"Successfully deleted point '{name}'.";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [KernelFunction, Description("List all active No-Fly Zones.")]
    public async Task<string> ListNoFlyZones()
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_c4iUrl);
            var res = await client.GetAsync("api/noflyzones");
            if (!res.IsSuccessStatusCode) return "Failed to list zones.";

            var json = await res.Content.ReadAsStringAsync();
            return $"Active No-Fly Zones: {json}"; 
        }
        catch (Exception ex)
        {
             return $"Error: {ex.Message}";
        }
    }

    [KernelFunction, Description("Delete ALL active No-Fly Zones.")]
    public async Task<string> DeleteAllNoFlyZones()
    {
        try
        {
            using var listClient = _httpClientFactory.CreateClient();
            listClient.BaseAddress = new Uri(_c4iUrl);
            var res = await listClient.GetAsync("api/noflyzones");
            if (!res.IsSuccessStatusCode) return "Failed to retrieve zones list.";
            
            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            
            int count = 0;
            var ids = new List<string>();

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                ids.Add(element.GetProperty("id").GetString()!);
            }

            using var deleteClient = _httpClientFactory.CreateClient();
            deleteClient.BaseAddress = new Uri(_c4iUrl);

            foreach (var id in ids)
            {
                var delRes = await deleteClient.DeleteAsync($"api/noflyzones/{id}");
                if (delRes.IsSuccessStatusCode)
                {
                    count++;
                    await NotifyBff("Point", "Deleted", new { Id = id });
                }
            }

            return $"Successfully deleted {count} zones.";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [KernelFunction, Description("Delete a No-Fly Zone by its name.")]
    public async Task<string> DeleteNoFlyZoneByName(
        [Description("The name of the zone to delete."), Required] string name)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_c4iUrl);

            var res = await client.GetAsync("api/noflyzones");
            if (!res.IsSuccessStatusCode) return "Failed to retrieve zones for lookup.";
            
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

            if (idToDelete == null) return $"Zone with name '{name}' not found.";

            var delRes = await client.DeleteAsync($"api/noflyzones/{idToDelete}");
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