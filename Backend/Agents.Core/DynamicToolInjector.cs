using System.Text.Json;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Client;

namespace Agents.Core;

public static class DynamicToolInjector
{
    public static async Task<List<KernelFunction>> GetToolsAsync(string mcpEndpoint, string agentName, string userMessage)
    {
        var functions = new List<KernelFunction>();
        try 
        {
            Console.WriteLine($"[{agentName}] Injecting tools from {mcpEndpoint}...");
            var mcpClient = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(mcpEndpoint) }));
            var tools = await mcpClient.ListToolsAsync();

            // Semantic JIT Simulation: In a real system (1000+ tools), we'd embed userMessage and tools.Description.
            // Here we inject the available tools to keep context small and targeted.
            foreach (var tool in tools)
            {
                var parameters = new List<KernelParameterMetadata>();
                var schema = tool.JsonSchema;

                if (schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("properties", out var props))
                {
                    foreach (var prop in props.EnumerateObject())
                    {
                        var typeStr = prop.Value.TryGetProperty("type", out var t) ? t.GetString() : "string";
                        var type = typeStr switch
                        {
                            "integer" => typeof(int),
                            "number" => typeof(double),
                            "boolean" => typeof(bool),
                            _ => typeof(string)
                        };
                        
                        parameters.Add(new KernelParameterMetadata(prop.Name)
                        {
                            ParameterType = type,
                            Description = prop.Value.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                            IsRequired = schema.TryGetProperty("required", out var req) && req.EnumerateArray().Any(x => x.GetString() == prop.Name)
                        });
                    }
                }

                var function = KernelFunctionFactory.CreateFromMethod(async (KernelArguments args) =>
                {
                    var toolArgs = args.ToDictionary(k => k.Key, v => v.Value);
                    Console.WriteLine($"[{agentName}] Executing {tool.Name} with {JsonSerializer.Serialize(toolArgs)}");
                    var result = await mcpClient.CallToolAsync(tool.Name, toolArgs);
                    
                    // Return the text content of the tool result. 
                    // This is CRITICAL for the agent to see if the tool failed (e.g. "Could not find coordinates").
                    var content = result.Content.FirstOrDefault();
                    if (content != null) 
                    { 
                        try 
                        { 
                            var text = ((dynamic)content).Text as string;
                            return text ?? "Success";
                        } 
                        catch { return "Success"; } 
                    }
                    return "Success";
                }, 
                functionName: tool.Name, 
                description: tool.Description,
                parameters: parameters);

                functions.Add(function);
            }
            Console.WriteLine($"[{agentName}] Successfully injected {functions.Count} tools.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{agentName}] Dynamic Injection Error: {ex.Message}");
        }
        return functions;
    }
}
