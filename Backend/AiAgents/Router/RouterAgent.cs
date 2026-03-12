using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text.Json;

namespace AiAgents.Router;

public class RouterAgent(IChatCompletionService chatSvc)
{
    public async Task<List<string>> ClassifyAsync(string message)
    {
        var history = new ChatHistory();
        history.AddSystemMessage("You are the Tier 1 Router Agent. Decisions: [\"FlightControl\", \"MissionControl\", \"Payload\"].\n" +
                               "ROUTING RULES:\n" +
                               "1. For navigation (fly, go, return, home, speed, altitude), ALWAYS use [\"FlightControl\", \"Payload\"].\n" +
                               "2. For map management (create, add, delete, remove, clear, points, zones), use [\"MissionControl\"].\n" +
                               "3. CRITICAL: DO NOT use MissionControl for flight commands unless it's explicitly to CREATE or DELETE a point/zone.\n" +
                               "4. CRITICAL: Return ONLY a JSON string array of required agents (e.g. [\"FlightControl\"]). DO NOT include any other text, explanation, or extra JSON fields.");
        history.AddUserMessage(message);

        var response = await chatSvc.GetChatMessageContentAsync(history);
        var content = response.Content?.Trim() ?? "";
        
        Console.WriteLine($"[RouterAgent] Raw Classification: {content}");

        var targets = new List<string>();
        if (content.Contains("[") && content.Contains("]"))
        {
            try 
            {
                var start = content.IndexOf("[");
                var end = content.LastIndexOf("]") + 1;
                var json = content.Substring(start, end - start);
                targets = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch { }
        }

        if (targets.Count == 0)
        {
            if (content.Contains("FlightControl", StringComparison.OrdinalIgnoreCase)) targets.Add("FlightControl");
            if (content.Contains("MissionControl", StringComparison.OrdinalIgnoreCase)) targets.Add("MissionControl");
            if (content.Contains("Payload", StringComparison.OrdinalIgnoreCase)) targets.Add("Payload");
        }

        return targets;
    }
}
