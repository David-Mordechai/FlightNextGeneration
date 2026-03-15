using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text.Json;
using AiAgents.Shared;

namespace AiAgents.Router;

public class RouterAgent(IChatCompletionService chatSvc, NotificationService notifier)
{
    private static readonly string[] AllowedDomains = { "FlightControl", "Payload", "MissionControl" };

    public async Task<List<string>> ClassifyAsync(string message, string correlationId)
    {
        var history = new ChatHistory();
        history.AddSystemMessage("# MISSION\n" +
                               "Identify required tactical domains. Return ONLY a JSON array.\n\n" +
                               "# VALID DOMAINS\n" +
                               "- FlightControl (UAV MOVEMENT, speed, altitude)\n" +
                               "- Payload (CAMERA and sensors, GIMBAL control)\n" +
                               "- MissionControl (Adding or removing points/zones)\n\n" +
                               "# EXAMPLES\n" +
                               "User: fly home. Result: [\"FlightControl\"]\n" +
                               "User: point camera A. Result: [\"Payload\"]\n" +
                               "User: fly home and look at Target. Result: [\"FlightControl\", \"Payload\"]\n" +
                               "User: look at Target. Result: [\"Payload\"]\n\n" +
                               "# RULES\n" +
                               "- Use 'FlightControl' ONLY if the UAV itself needs to MOVE.\n" +
                               "- Use 'Payload' if the CAMERA needs to turn or look at something.\n" +
                               "- Use ONLY the valid domains listed above.\n" +
                               "- Output the JSON array and STOP.");
        
        history.AddUserMessage(message);

        var settings = new OpenAIPromptExecutionSettings { MaxTokens = 50 };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = await chatSvc.GetChatMessageContentAsync(history, settings);
        stopwatch.Stop();

        var content = response.Content?.Trim() ?? "";
        
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

        // 1. Strict Filter against Allowed Domains
        targets = targets.Where(t => AllowedDomains.Contains(t)).ToList();

        // 2. Fallback logic if parsing failed or model hallucinated unknown strings
        if (targets.Count == 0)
        {
            if (content.Contains("FlightControl", StringComparison.OrdinalIgnoreCase)) targets.Add("FlightControl");
            if (content.Contains("Payload", StringComparison.OrdinalIgnoreCase) || content.Contains("Camera", StringComparison.OrdinalIgnoreCase)) targets.Add("Payload");
            if (content.Contains("MissionControl", StringComparison.OrdinalIgnoreCase) || content.Contains("Point", StringComparison.OrdinalIgnoreCase)) targets.Add("MissionControl");
        }

        await notifier.NotifyAsync(correlationId, "Router", "Classification", $"Determined domains: {JsonSerializer.Serialize(targets)}", stopwatch.Elapsed.TotalSeconds);

        return targets.Distinct().ToList();
    }
}
