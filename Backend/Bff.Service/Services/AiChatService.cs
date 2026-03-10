using System.Text.Json;
using System.Text;

namespace Bff.Service.Services;

public class AiChatService(ILogger<AiChatService> logger, IConfiguration config)
{
    private readonly HttpClient _httpClient = new();
    private readonly string _routerUrl = config["AgentsRouterUrl"] ?? "http://agents.router:8080";
    private readonly string _flightAgentUrl = config["AgentsFlightUrl"] ?? "http://agents.flightcontrol:8080";
    private readonly string _missionAgentUrl = config["AgentsMissionUrl"] ?? "http://agents.missioncontrol:8080";
    private readonly string _payloadAgentUrl = config["AgentsPayloadUrl"] ?? "http://agents.payload:8080";

    public async Task<bool> CheckReadinessAsync()
    {
        try
        {
            var res = await _httpClient.GetAsync($"{_routerUrl}/health");
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> ProcessUserMessage(string userMessage)
    {
        try
        {
            logger.LogInformation("[Tier 1] Routing message: {Message}", userMessage);
            
            var routerRes = await _httpClient.PostAsJsonAsync($"{_routerUrl}/route", userMessage);
            if (!routerRes.IsSuccessStatusCode) return "Router failed to assign agent.";
            
            var routerData = await routerRes.Content.ReadFromJsonAsync<JsonElement>();
            var targets = routerData.GetProperty("targets").EnumerateArray()
                .Select(x => x.GetString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct() // Deduplicate
                .ToList();
            
            if (targets.Count == 0) return "No agents assigned to this request.";

            var responses = new List<string>();
            var agentTasks = targets.Select(async targetAgent =>
            {
                if (string.IsNullOrWhiteSpace(targetAgent)) return;

                logger.LogInformation("[Tier 1] Dispatching to Agent: {Agent}", targetAgent);

                string agentUrl = targetAgent switch
                {
                    "MissionControl" => _missionAgentUrl,
                    "Payload" => _payloadAgentUrl,
                    _ => _flightAgentUrl
                };

                try 
                {
                    var agentRes = await _httpClient.PostAsJsonAsync($"{agentUrl}/execute", userMessage);
                    if (agentRes.IsSuccessStatusCode)
                    {
                        var agentData = await agentRes.Content.ReadFromJsonAsync<JsonElement>();
                        string response = agentData.GetProperty("response").GetString() ?? "";
                        if (!string.IsNullOrWhiteSpace(response))
                        {
                            lock (responses) { responses.Add(response); }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error calling agent {Agent}", targetAgent);
                }
            });

            await Task.WhenAll(agentTasks);

            // Intelligent Merging: Filter out errors if we have successes
            var filteredResponses = responses.Where(r => !string.IsNullOrWhiteSpace(r)).ToList();
            var successfulResponses = filteredResponses.Where(r => 
                !r.Contains("Error:", StringComparison.OrdinalIgnoreCase) && 
                !r.Contains("Action failed", StringComparison.OrdinalIgnoreCase)).ToList();
            
            var baseResponses = (successfulResponses.Count > 0) ? successfulResponses : filteredResponses;

            // If we have specific flight/payload responses, we can potentially ignore generic mission control noise
            if (baseResponses.Any(r => r.StartsWith("Executed:", StringComparison.OrdinalIgnoreCase) || r.Contains("locked", StringComparison.OrdinalIgnoreCase)))
            {
                // If we have real actions, remove the generic "Entities updated" noise
                baseResponses = baseResponses.Where(r => !r.Equals("Entities updated.", StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // Deduplicate and join responses
            var finalResult = string.Join(". ", baseResponses.Distinct().Select(r => r.Trim().TrimEnd('.'))) + ".";
            
            return string.IsNullOrWhiteSpace(finalResult) || finalResult == "." ? "Request processed." : finalResult;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fail to process user message through agentic system");
            return "Error processing request.";
        }
    }

    public void BuildChatService(ChatType chatType, string model, string apiKey, string providerUrl)
    {
        logger.LogInformation("BFF is now operating as a Gateway to the Scalable Agentic System.");
    }
}

public enum ChatType { OpenAi, GoogleGemini, Ollama }
