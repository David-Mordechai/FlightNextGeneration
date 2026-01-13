using ModelContextProtocol.Server;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace McpServer.FlightControl;

[McpServerToolType]
public class Tools(ILogger<Tools> logger, CommunicationService communicationService)
{
    [McpServerTool, Description("Navigate the UAV to a specific city or location.")]
    public async Task<string> NavigateTo(
        [Description("The name of the city or location to fly to (e.g., 'Tel Aviv', 'Haifa')."), Required] 
        string location)
    {
        try
        {
            //await communicationService.NavigateTo(location);
            logger.LogInformation("Mission updated! Flying to {Location}.", location);
            return $"Mission updated! Flying to {location}.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fail to communicate with the client");
            return "Fail to communicate with the client";
        }
    }

    [McpServerTool, Description("Change the UAV's target speed in knots.")]
    public async Task<string> ChangeSpeed(
        [Description("Target speed in knots (e.g., 150)."), Required]int speed)
    {
        try
        {
            //await communicationService.ChangeSpeed(speed);
            logger.LogInformation("Acknowledged. Adjusting speed to {Speed} kts.", speed);
            return $"Acknowledged. Adjusting speed to {speed} kts.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fail to communicate with the client");
            return "Fail to communicate with the client";
        }
    }

    [McpServerTool, Description("Change the UAV's target altitude in feet.")]
    public async Task<string> ChangeAltitude(
        [Description("Target altitude in feet (e.g., 5000)."), Required] int altitude)
    {
        try
        {
            //await communicationService.ChangeAltitude(altitude);
            logger.LogInformation("Acknowledged. Changing altitude to {Altitude} ft.", altitude);
            return $"Acknowledged. Changing altitude to {altitude} feet.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fail to communicate with the client");
            return "Fail to communicate with the client";
        }
    }
}