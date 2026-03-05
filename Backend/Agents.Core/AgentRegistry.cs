namespace Agents.Core;

public static class AgentRegistry
{
    // Categorize intent to microservice
    public static string ResolveAgent(string intent)
    {
        var input = intent.ToLower();
        if (input.Contains("speed") || input.Contains("altitude") || input.Contains("navigate") || input.Contains("fly"))
            return "FlightControl";
        
        if (input.Contains("camera") || input.Contains("payload") || input.Contains("sensor") || input.Contains("gimbal"))
            return "Payload";

        if (input.Contains("zone") || input.Contains("point") || input.Contains("create") || input.Contains("list"))
            return "MissionControl";

        return "FlightControl"; // Default
    }
}
