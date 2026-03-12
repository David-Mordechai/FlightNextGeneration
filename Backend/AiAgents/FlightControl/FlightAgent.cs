using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text.RegularExpressions;

namespace AiAgents.FlightControl;

public class FlightAgent(IChatCompletionService chatSvc, FlightTools tools)
{
    public async Task<string> ProcessAsync(string message)
    {
        var kernel = new Kernel();
        kernel.Plugins.AddFromObject(tools, "Flight");

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
        var response = await chatSvc.GetChatMessageContentAsync(history, settings, kernel);
        
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

        if (executedActions.Count > 0)
        {
            var summary = string.Join(" ", executedActions.Distinct());
            return summary;
        }
        
        return "I've received your request, but no specific flight actions were executed.";
    }
}
