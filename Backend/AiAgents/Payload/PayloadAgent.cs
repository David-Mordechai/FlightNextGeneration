using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text.RegularExpressions;
using AiAgents.Shared;

namespace AiAgents.Payload;

public class PayloadAgent(IChatCompletionService chatSvc, PayloadTools tools, NotificationService notifier)
{
    public async Task<string> ProcessAsync(string message, string correlationId)
    {
        // Construct an isolated Kernel for this agent
        var kernel = new Kernel();
        kernel.Plugins.AddFromObject(tools, "Payload");

        var history = new ChatHistory();
        history.AddSystemMessage("You are a tactical sensor operator.\n" +
                               "# MULTI-TOOL EXTRACTION\n" +
                               "The user may provide multiple instructions in a single request. You MUST call ALL relevant tools in one response.\n\n" +
                               "# INSTRUCTIONS\n" +
                               "INSTRUCTION: Call PointPayload ONLY for exact location names (e.g. 'Target A', 'Home').\n" +
                               "CRITICAL: Do NOT include conjunctions like 'and' or 'then' in the location name. If the user says 'look at Target and set speed', use 'Target'.\n" +
                               "CRITICAL: Do not truncate names. If the user says 'target a', use 'Target A'.\n" +
                               "CRITICAL: If a tool returns an error, report that error to the user.");
        history.AddUserMessage(message);

        var settings = new OpenAIPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: false) };
        
        var swReasoning = System.Diagnostics.Stopwatch.StartNew();
        var response = await chatSvc.GetChatMessageContentAsync(history, settings, kernel);
        swReasoning.Stop();

        await notifier.NotifyAsync(correlationId, "Payload", "Thinking", "Processing gimbal/sensor commands.", swReasoning.Elapsed.TotalSeconds);
        
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
                    var argsJson = System.Text.Json.JsonSerializer.Serialize(call.Arguments);
                    await notifier.NotifyAsync(correlationId, "Payload", "Action", $"Executing tool: {funcName} with args: {argsJson}");
                    
                    var swTool = System.Diagnostics.Stopwatch.StartNew();
                    var result = await call.InvokeAsync(kernel);
                    swTool.Stop();
                    
                    var resultStr = "";
                    if (result is FunctionResultContent frc) resultStr = frc.Result?.ToString() ?? "";
                    else resultStr = result.ToString() ?? "";
                    
                    await notifier.NotifyAsync(correlationId, "Payload", "Result", resultStr, swTool.Elapsed.TotalSeconds);
                    executedActions.Add(resultStr);
                }
                catch (Exception ex)
                {
                    await notifier.NotifyAsync(correlationId, "Payload", "Error", $"Tool failed: {ex.Message}");
                    executedActions.Add($"Failed to execute {funcName}: {ex.Message}");
                }
            }
        }

        return executedActions.Count > 0 ? string.Join(" ", executedActions.Distinct()) : "";
    }
}
