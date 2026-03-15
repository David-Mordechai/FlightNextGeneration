using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text.Json;
using AiAgents.Shared;

namespace AiAgents.Router;

public class RouterAgent(IChatCompletionService chatSvc, NotificationService notifier)
{
    public async Task<List<string>> ClassifyAsync(string message, string correlationId)
    {
        var history = new ChatHistory();
        history.AddSystemMessage("# MISSION\n" +
                               "Identify required tactical domains. Return ONLY a JSON array.\n\n" +
                               "# EXAMPLES\n" +
                               "User: fly home. Result: [\"FlightControl\"]\n" +
                               "User: point camera A. Result: [\"Payload\"]\n" +
                               "User: create point Alpha. Result: [\"MissionControl\"]\n" +
                               "User: remove point Alpha. Result: [\"MissionControl\"]\n\n" +
                               "# RULES\n" +
                               "- FlightControl: Navigation, speed, altitude.\n" +
                               "- Payload: Camera and sensors.\n" +
                               "- MissionControl: ADDING or REMOVING points/zones from the map.\n" +
                               "- Output the JSON array and STOP.");
        
        history.AddUserMessage(message);

        var settings = new OpenAIPromptExecutionSettings { MaxTokens = 50 };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = await chatSvc.GetChatMessageContentAsync(history, settings);
        stopwatch.Stop();

        var content = response.Content?.Trim() ?? "";
        
        await notifier.NotifyAsync(correlationId, "Router", "Classification", $"Determined domains: {content}", stopwatch.Elapsed.TotalSeconds);

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

        // Robust Fallbacks for common model hallucinations
        if (targets.Count == 0 || content.Contains("Deletion", StringComparison.OrdinalIgnoreCase) || content.Contains("Remove", StringComparison.OrdinalIgnoreCase))
        {
            if (content.Contains("FlightControl", StringComparison.OrdinalIgnoreCase)) targets.Add("FlightControl");
            if (content.Contains("MissionControl", StringComparison.OrdinalIgnoreCase) || content.Contains("Deletion", StringComparison.OrdinalIgnoreCase)) 
                if (!targets.Contains("MissionControl")) targets.Add("MissionControl");
            if (content.Contains("Payload", StringComparison.OrdinalIgnoreCase)) targets.Add("Payload");
        }

        return targets.Distinct().ToList();
    }
}
