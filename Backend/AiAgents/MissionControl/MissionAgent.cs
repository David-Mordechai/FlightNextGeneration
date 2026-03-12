using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using AiAgents.Shared;

namespace AiAgents.MissionControl;

public class MissionAgent(IChatCompletionService chatSvc, MissionTools tools, NotificationService notifier)
{
    public async Task<string> ProcessAsync(string message, string correlationId)
    {
        // Construct an isolated Kernel for this agent
        var kernel = new Kernel();
        kernel.Plugins.AddFromObject(tools, "Mission");

        var history = new ChatHistory();
        history.AddSystemMessage("You are the Mission Control Agent. Available Tools: [CreatePoint, ListPoints, DeletePointByName, CreateRectangleZone, CreatePolygonZone, ListNoFlyZones, DeleteNoFlyZoneByName, DeleteAllPoints, DeleteAllNoFlyZones].\n" +
                               "RULE: Resolve descriptive names (e.g. 'the test entity') to exact stored names (e.g. 'test') before calling tools.\n" +
                               "RULE: You MUST use the available tools to perform actions based on the user's intent.\n" +
                               "RULE: If the user intent is NOT about points or no-fly zones, DO NOT call any tools and return an empty string.\n" +
                               "RESPONSE RULE: Return ONLY a technical confirmation string after calling a tool. If no tool is called, return an empty string.");
        history.AddUserMessage(message);

        var settings = new OpenAIPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };
        
        await notifier.NotifyAsync(correlationId, "MissionControl", "Thinking", "Analyzing mission data requests...");
        
        await chatSvc.GetChatMessageContentAsync(history, settings, kernel);

        var toolCalls = history.SelectMany(m => m.Items).OfType<FunctionCallContent>().ToList();

        var successfulCallNames = toolCalls.Select(f => {
                                            var name = f.FunctionName;
                                            if (name.Contains("-")) name = name.Split('-').Last();
                                            return name;
                                        })
                                        .ToList();

        if (successfulCallNames.Count > 0)
        {
            var actions = new List<string>();
            foreach (var name in successfulCallNames)
            {
                await notifier.NotifyAsync(correlationId, "MissionControl", "Result", $"Action completed: {name}");
                
                if (name == "CreatePoint") actions.Add("updated map points");
                if (name == "DeletePointByName") actions.Add("removed map points");
                if (name == "CreateRectangleZone") actions.Add("updated restricted zones");
                if (name == "DeleteAllPoints") actions.Add("cleared map points");
                if (name == "DeleteAllNoFlyZones") actions.Add("cleared restricted zones");
            }

            if (actions.Count > 0)
            {
                return "Mission status: " + string.Join(" and ", actions.Distinct()) + ".";
            }
            return "Mission database updated.";
        }

        return "";
    }
}
