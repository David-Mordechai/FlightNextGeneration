using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ModelContextProtocol.Server;

namespace McpServer.FlightControl;

[McpServerToolType]
public class Tools(ILogger<Tools> logger, CommunicationService communicationService)
{
    [McpServerTool, Description("Navigate the UAV to a specific city or location.")]
    public async Task<string> NavigateTo(
        [Description("The name of the city or location to fly to (e.g., 'Tel Aviv', 'Haifa')."), Required] string location)
    {
        try
        {
            var lowerMsg = location.ToLowerInvariant();
            if (lowerMsg.Contains("fly to") || lowerMsg.Contains("go to") || lowerMsg.Contains("fly over") || lowerMsg.Contains("over "))
            {
                var extractedLocation = Helper.ExtractLocation(lowerMsg);
                if (string.IsNullOrWhiteSpace(extractedLocation)) return "Location not found";
                await communicationService.NavigateTo(extractedLocation);
                logger.LogInformation("Mission updated! Flying to {Location}.", extractedLocation);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fail to communicate with the client");
            return $"Error contacting backend: {ex.Message}";
        }

        return "Location not found";
    }

    [McpServerTool, Description("Change the UAV's target speed in knots.")]
    public async Task<string> ChangeSpeed(string message)
    {
        try
        {
            var lowerMsg = message.ToLowerInvariant();
            if (lowerMsg.Contains("speed") || lowerMsg.Contains("knots"))
            {
                var speed = Helper.ExtractFirstNumber(message);
                if (speed.HasValue)
                {
                    await communicationService.ChangeSpeed(speed.Value);
                    logger.LogInformation("Acknowledged. Adjusting speed to {Speed} kts.", speed.Value);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fail to communicate with the client");
            return $"Error contacting backend: {ex.Message}";
        }

        return "Invalid speed request.";
    }

    [McpServerTool, Description("Change the UAV's target altitude in feet.")]
    public async Task<string> ChangeAltitude(string message)
    {
        try
        {
            var lowerMsg = message.ToLowerInvariant();
            if (lowerMsg.Contains("altitude") || lowerMsg.Contains("feet") || lowerMsg.Contains("climb") || lowerMsg.Contains("descend"))
            {
                var alt = Helper.ExtractFirstNumber(message);
                if (alt.HasValue)
                {
                    await communicationService.ChangeAltitude(alt.Value);
                    logger.LogInformation("Acknowledged. Changing altitude to {Altitude} ft.", alt.Value);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fail to communicate with the client");
            return $"Error contacting backend: {ex.Message}";
        }
        return "Invalid altitude request.";
    }
}