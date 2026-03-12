using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text.RegularExpressions;

namespace AiAgents.Payload;

public class PayloadAgent(IChatCompletionService chatSvc, PayloadTools tools)
{
    public async Task<string> ProcessAsync(string message)
    {
        // Construct an isolated Kernel for this agent
        var kernel = new Kernel();
        kernel.Plugins.AddFromObject(tools, "Payload");

        var history = new ChatHistory();
        history.AddSystemMessage("You are a tactical sensor operator.\n" +
                               "INSTRUCTION: Call PointPayload ONLY for exact location names (e.g. 'Target A', 'Home').\n" +
                               "CRITICAL: Do not truncate names. If the user says 'target a', use 'Target A'.\n" +
                               "CRITICAL: If a tool returns an error, report that error to the user.");
        history.AddUserMessage(message);

        var settings = new OpenAIPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: false) };
        var response = await chatSvc.GetChatMessageContentAsync(history, settings, kernel);
        
        var toolCalls = response.Items.OfType<FunctionCallContent>().ToList();
        var executedActions = new List<string>();

        foreach (var call in toolCalls)
        {
            var funcName = call.FunctionName;
            if (funcName.Contains("-")) funcName = funcName.Split('-').Last();

            bool allowed = true;
            if (funcName == "ResetPayload" && !message.Contains("reset", StringComparison.OrdinalIgnoreCase)) allowed = false;
            if (funcName == "PointPayload" && !Regex.IsMatch(message, @"home|target|alpha|beta|A|B", RegexOptions.IgnoreCase)) allowed = false;

            if (allowed)
            {
                try 
                {
                    var result = await call.InvokeAsync(kernel);
                    
                    // Extract actual string content from the FunctionResultContent
                    var resultStr = "";
                    if (result is FunctionResultContent frc) resultStr = frc.Result?.ToString() ?? "";
                    else resultStr = result.ToString() ?? "";
                    
                    executedActions.Add(resultStr);
                }
                catch (Exception ex)
                {
                    executedActions.Add($"Failed to execute {funcName}: {ex.Message}");
                }
            }
        }

        return executedActions.Count > 0 ? string.Join(" ", executedActions.Distinct()) : "";
    }
}
