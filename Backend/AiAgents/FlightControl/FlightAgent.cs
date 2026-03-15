using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text.RegularExpressions;
using AiAgents.Shared;

namespace AiAgents.FlightControl;

public class FlightAgent(IChatCompletionService chatSvc, FlightTools tools, NotificationService notifier)
{
    public async Task<string> ProcessAsync(string message, string correlationId)
    {
        var kernel = new Kernel();
        kernel.Plugins.AddFromObject(tools, "Flight");

        // Attach correlation ID to tools via Metadata or Scope if needed, 
        // but for now tools will be called manually so we can pass it.
        
        var history = new ChatHistory();
        history.AddSystemMessage("# MISSION\n" +
                               "Extract and execute UAV navigation, speed, and altitude commands.\n\n" +
                               "# INSTRUCTIONS\n" +
                               "- Call NavigateTo ONLY for exact location names (e.g. 'Target A', 'Home').\n" +
                               "- CRITICAL: Do not truncate names. If the user says 'target a', use 'Target A'.\n" +
                               "- CRITICAL: If a tool returns an error, pass that error message to the user.\n\n" +
                               "# CONSTRAINTS\n" +
                               "- LOW BANDWIDTH: Be extremely brief. Use absolute minimum tokens.");
        
        history.AddUserMessage(message);

        var settings = new OpenAIPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: false) };
        
        var swReasoning = System.Diagnostics.Stopwatch.StartNew();
        var response = await chatSvc.GetChatMessageContentAsync(history, settings, kernel);
        swReasoning.Stop();
        
        await notifier.NotifyAsync(correlationId, "FlightControl", "Thinking", "Extracted flight parameters from request.", swReasoning.Elapsed.TotalSeconds);
        
        var toolCalls = response.Items.OfType<FunctionCallContent>().ToList();
        var executedActions = new List<string>();

        foreach (var call in toolCalls)
        {
            var funcName = call.FunctionName;
            if (funcName.Contains("-")) funcName = funcName.Split('-').Last();

            bool allowed = true;
            if (funcName == "ChangeSpeed" && !message.Contains("speed", StringComparison.OrdinalIgnoreCase)) allowed = false;
            if (funcName == "ChangeAltitude" && !message.Contains("altitude", StringComparison.OrdinalIgnoreCase)) allowed = false;
            if (funcName == "NavigateTo" && !Regex.IsMatch(message, @"fly|go|home|return|target", RegexOptions.IgnoreCase)) allowed = false;

            if (allowed)
            {
                try 
                {
                    var argsJson = System.Text.Json.JsonSerializer.Serialize(call.Arguments);
                    await notifier.NotifyAsync(correlationId, "FlightControl", "Action", $"Executing tool: {funcName} with args: {argsJson}");
                    
                    var swTool = System.Diagnostics.Stopwatch.StartNew();
                    var result = await call.InvokeAsync(kernel);
                    swTool.Stop();
                    
                    var resultStr = "";
                    if (result is FunctionResultContent frc) resultStr = frc.Result?.ToString() ?? "";
                    else resultStr = result.ToString() ?? "";
                    
                    await notifier.NotifyAsync(correlationId, "FlightControl", "Result", resultStr, swTool.Elapsed.TotalSeconds);
                    executedActions.Add(resultStr);
                }
                catch (Exception ex)
                {
                    await notifier.NotifyAsync(correlationId, "FlightControl", "Error", $"Tool failed: {ex.Message}");
                    executedActions.Add($"Failed to execute {funcName}: {ex.Message}");
                }
            }
        }

        if (executedActions.Count > 0)
        {
            var summary = string.Join(" ", executedActions.Distinct());
            return summary;
        }
        
        return "I've received your request, but no specific flight actions were executed.";
    }
}
