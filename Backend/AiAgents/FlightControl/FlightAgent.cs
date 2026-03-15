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
                               "Extract and execute ALL flight commands from the user message.\n\n" +
                               "# TOOLS TO CALL\n" +
                               "1. If user explicitly wants to FLY/MOVE to a destination -> Call 'NavigateTo(location=\"NAME\")'.\n" +
                               "2. If user mentions speed -> Call 'ChangeSpeed(speed=VALUE)'.\n" +
                               "3. If user mentions altitude -> Call 'ChangeAltitude(altitude=VALUE)'.\n\n" +
                               "# MULTI-TOOL EXTRACTION (CRITICAL)\n" +
                               "The user will often provide multiple parameters at once. You MUST scan the entire request and call EVERY tool that applies.\n" +
                               "Example: 'fly to Target A, speed 500, alt 5000' -> Call NavigateTo, ChangeSpeed, and ChangeAltitude.\n\n" +
                               "# CRITICAL RULES\n" +
                               "- DO NOT call 'NavigateTo' just because a location (like 'Home') is mentioned. Only call it if the user wants to MOVE there.\n" +
                               "- If the user says 'point camera at Home', DO NOT call 'NavigateTo'. That is a payload command, not a flight command.\n" +
                               "- If the user provides multiple flight commands, you MUST call multiple tools in a single response.");
        
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
