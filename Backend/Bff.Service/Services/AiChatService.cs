using System.Text.Json;
using System.Text;

namespace Bff.Service.Services;

public class AiChatService(ILogger<AiChatService> logger, IConfiguration config)
{
    private readonly HttpClient _httpClient = new();
    private readonly string _aiAgentsUrl = config["AiAgentsUrl"] ?? "http://ai-agents:8080";

    public async Task<bool> CheckReadinessAsync()
    {
        try
        {
            var res = await _httpClient.GetAsync($"{_aiAgentsUrl}/health");
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
            logger.LogInformation("[Tier 1] Unified Agent System processing: {Message}", userMessage);
            
            var response = await _httpClient.PostAsJsonAsync($"{_aiAgentsUrl}/execute", userMessage);
            if (!response.IsSuccessStatusCode) return "Unified Agent System failed to respond.";
            
            var data = await response.Content.ReadFromJsonAsync<JsonElement>();
            string finalResult = data.GetProperty("response").GetString() ?? "Request processed.";
            
            return finalResult;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fail to process user message through unified agent system");
            return "Error processing request.";
        }
    }

    public void BuildChatService(ChatType chatType, string model, string apiKey, string providerUrl)
    {
        logger.LogInformation("BFF is now operating as a Gateway to the Unified Scalable Agentic System.");
    }
}

public enum ChatType { OpenAi, GoogleGemini, Ollama }
