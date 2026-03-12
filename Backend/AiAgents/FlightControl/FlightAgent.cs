using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text.RegularExpressions;

namespace AiAgents.FlightControl;

public class FlightAgent(IChatCompletionService chatSvc, FlightTools tools)
{
    public async Task<string> ProcessAsync(string message)
    {
        // Construct an isolated Kernel for this agent
        var kernel = new Kernel();
        kernel.Plugins.AddFromObject(tools, "Flight");

        var history = new ChatHistory();
        history.AddSystemMessage("You are a flight command parser. Your mission is to extract and execute tools for ALL flight parameters in the user message.\n" +
                               "RULES:\n" +
                               "- If 'fly', 'go', 'home' or a location is mentioned -> call NavigateTo.\n" +
                               "- If 'speed' is mentioned -> call ChangeSpeed with the number provided.\n" +
                               "- If 'altitude' is mentioned -> call ChangeAltitude with the number provided.");
        history.AddUserMessage(message);

        var settings = new OpenAIPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: false) };
        var response = await chatSvc.GetChatMessageContentAsync(history, settings, kernel);
        
        var toolCalls = response.Items.OfType<FunctionCallContent>().ToList();
        var executedActions = new List<string>();

        foreach (var call in toolCalls)
        {
            // Robust check for function name
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
                    await call.InvokeAsync(kernel);
                    var args = call.Arguments;
                    if (funcName == "NavigateTo") 
                    {
                        object? loc = null;
                        if (args != null && !args.TryGetValue("location", out loc)) args.TryGetValue("locationName", out loc);
                        executedActions.Add($"setting course for {loc ?? "the target"}");
                    }
                    if (funcName == "ChangeSpeed") 
                    {
                        object? speed = null;
                        args?.TryGetValue("speed", out speed);
                        executedActions.Add($"adjusting speed to {speed ?? "requested"} knots");
                    }
                    if (funcName == "ChangeAltitude") 
                    {
                        object? alt = null;
                        args?.TryGetValue("altitude", out alt);
                        executedActions.Add($"climbing to {alt ?? "requested"} feet");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FlightAgent] Error invoking {call.FunctionName}: {ex.Message}");
                }
            }
        }

        if (executedActions.Count > 0)
        {
            var summary = string.Join(", ", executedActions.Distinct());
            return char.ToUpper(summary[0]) + summary.Substring(1) + ".";
        }
        
        return "I've received your request, but no specific flight actions were executed.";
    }
}
