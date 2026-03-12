using System.Text.Json;
using System.Text;

namespace Bff.Service.Services;

public record AiResponse(string Response, double Duration, string CorrelationId);

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

    public async Task<AiResponse> ProcessUserMessage(string userMessage, string? correlationId = null)
    {
        var cid = correlationId ?? Guid.NewGuid().ToString().Substring(0, 8);
        try
        {
            logger.LogInformation("[{CorrelationId}] [Tier 1] Unified Agent System processing: {Message}", cid, userMessage);
            
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_aiAgentsUrl}/execute");
            request.Content = new StringContent(JsonSerializer.Serialize(userMessage), Encoding.UTF8, "application/json");
            request.Headers.Add("X-Correlation-Id", cid);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return new AiResponse("Unified Agent System failed to respond.", 0, cid);
            
            var data = await response.Content.ReadFromJsonAsync<JsonElement>();
            string finalResult = data.GetProperty("response").GetString() ?? "Request processed.";
            double duration = data.TryGetProperty("duration", out var d) ? d.GetDouble() : 0;
            
            return new AiResponse(finalResult, duration, cid);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fail to process user message through unified agent system");
            return new AiResponse("Error processing request.", 0, cid);
        }
    }

    public void BuildChatService(ChatType chatType, string model, string apiKey, string providerUrl)
    {
        logger.LogInformation("BFF is now operating as a Gateway to the Unified Scalable Agentic System.");
    }
}

public enum ChatType { OpenAi, GoogleGemini, Ollama }
