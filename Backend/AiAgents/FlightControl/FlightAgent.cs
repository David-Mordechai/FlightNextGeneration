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
        history.AddSystemMessage("# ROLE\n" +
                               "You are the UAV Flight Control Agent.\n\n" +
                               "# TASK\n" +
                               "Extract and execute ALL flight commands. You MUST call ALL relevant tools.\n\n" +
                               "# TOOLS AND ARGUMENTS\n" +
                               "1. NavigateTo(location: string) - Use for 'fly to', 'go to'. Example: NavigateTo(location=\"Target\").\n" +
                               "2. ChangeSpeed(speed: int) - Use for 'speed 500', 'set speed to 100'. Example: ChangeSpeed(speed=500).\n" +
                               "3. ChangeAltitude(altitude: int) - Use for 'altitude 5000', 'alt 2000'. Example: ChangeAltitude(altitude=5000).\n\n" +
                               "# CRITICAL\n" +
                               "- You MUST extract the number for speed and altitude. Do NOT leave them empty.\n" +
                               "- If the user says 'speed 500', call ChangeSpeed(speed=500).\n" +
                               "- If the user says 'altitude 5000', call ChangeAltitude(altitude=5000).\n" +
                               "- If the user says 'fly to target', call NavigateTo(location=\"target\").\n" +
                               "- DO NOT call NavigateTo for camera commands.");
        
        history.AddUserMessage(message);

        var settings = new OpenAIPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: false) };
        var executedActions = new List<string>();
        var executedToolNames = new HashSet<string>();
        
        // Iterative extraction to support models that only return one tool at a time
        int maxTurns = 5;
        int currentTurn = 0;
        bool foundNewTools = true;

        while (foundNewTools && currentTurn < maxTurns)
        {
            currentTurn++;
            var swReasoning = System.Diagnostics.Stopwatch.StartNew();
            var response = await chatSvc.GetChatMessageContentAsync(history, settings, kernel);
            swReasoning.Stop();
            
            var content = response.Content ?? "";
            Console.WriteLine($"[FlightControl] Raw Response: {content}");

            var toolCalls = response.Items.OfType<FunctionCallContent>().ToList();
            if (toolCalls.Count == 0) break;

            foundNewTools = false;
            foreach (var call in toolCalls)
            {
                var funcName = call.FunctionName;
                if (funcName.Contains("-")) funcName = funcName.Split('-').Last();

                if (executedToolNames.Contains(funcName)) continue;

                bool allowed = true;
                if (funcName == "ChangeSpeed" && !message.Contains("speed", StringComparison.OrdinalIgnoreCase)) allowed = false;
                if (funcName == "ChangeAltitude" && !message.Contains("altitude", StringComparison.OrdinalIgnoreCase)) allowed = false;
                if (funcName == "NavigateTo" && !Regex.IsMatch(message, @"fly|go|return|navigate|move", RegexOptions.IgnoreCase)) allowed = false;

                if (allowed)
                {
                    foundNewTools = true;
                    executedToolNames.Add(funcName);
                    
                    try 
                    {
                        await notifier.NotifyAsync(correlationId, "FlightControl", "Thinking", $"Extracted tool: {funcName}", swReasoning.Elapsed.TotalSeconds);
                        
                        var argsJson = System.Text.Json.JsonSerializer.Serialize(call.Arguments);
                        Console.WriteLine($"[FlightControl] Executing tool: {funcName} with args: {argsJson}");
                        await notifier.NotifyAsync(correlationId, "FlightControl", "Action", $"Executing tool: {funcName} with args: {argsJson}");
                        
                        var swTool = System.Diagnostics.Stopwatch.StartNew();
                        var result = await call.InvokeAsync(kernel);
                        swTool.Stop();
                        
                        var resultStr = "";
                        if (result is FunctionResultContent frc) resultStr = frc.Result?.ToString() ?? "";
                        else resultStr = result.ToString() ?? "";
                        
                        await notifier.NotifyAsync(correlationId, "FlightControl", "Result", resultStr, swTool.Elapsed.TotalSeconds);
                        executedActions.Add(resultStr);

                        // Add the tool result back to history so the model knows it was executed
                        history.Add(response); // Add the assistant message with tool call
                        history.Add(new ChatMessageContent(AuthorRole.Tool, resultStr) { Items = { new FunctionResultContent(call, result) } });
                    }
                    catch (Exception ex)
                    {
                        await notifier.NotifyAsync(correlationId, "FlightControl", "Error", $"Tool failed: {ex.Message}");
                        executedActions.Add($"Failed to execute {funcName}: {ex.Message}");
                    }
                }
            }

            if (!foundNewTools) break;
        }

        if (executedActions.Count > 0)
        {
            var summary = string.Join(" ", executedActions.Distinct());
            return summary;
        }
        
        return "I've received your request, but no specific flight actions were executed.";
    }
}
