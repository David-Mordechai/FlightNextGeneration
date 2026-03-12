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
        history.AddSystemMessage("DANGER: DO NOT CALL ResetPayload unless the user message contains the word 'reset'.\n" +
                               "INSTRUCTION: Call PointPayload ONLY for location names (e.g. 'Target A').");
        history.AddUserMessage(message);

        var settings = new OpenAIPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: false) };
        var response = await chatSvc.GetChatMessageContentAsync(history, settings, kernel);
        
        var toolCalls = response.Items.OfType<FunctionCallContent>().ToList();
        var executedActions = new List<string>();

        foreach (var call in toolCalls)
        {
            // Robust check for function name (handles Plugin-Function format)
            var funcName = call.FunctionName;
            if (funcName.Contains("-")) funcName = funcName.Split('-').Last();

            bool allowed = true;
            if (funcName == "ResetPayload" && !message.Contains("reset", StringComparison.OrdinalIgnoreCase)) allowed = false;
            if (funcName == "PointPayload" && !Regex.IsMatch(message, @"home|target|alpha|beta|A|B", RegexOptions.IgnoreCase)) allowed = false;

            if (allowed)
            {
                try 
                {
                    await call.InvokeAsync(kernel);
                    var args = call.Arguments;
                    if (funcName == "PointPayload")
                    {
                        object? loc = null;
                        if (args != null && !args.TryGetValue("location", out loc)) args.TryGetValue("locationName", out loc);
                        executedActions.Add($"I've locked the camera gimbal on {loc ?? "the target"} for you.");
                    }
                    else if (funcName == "ResetPayload")
                    {
                        executedActions.Add("Sensors have been reset and calibrated.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PayloadAgent] Error invoking {call.FunctionName}: {ex.Message}");
                }
            }
        }

        return executedActions.Count > 0 ? string.Join(" ", executedActions.Distinct()) : "";
    }
}
