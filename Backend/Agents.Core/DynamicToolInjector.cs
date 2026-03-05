using System.Text.Json;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Client;

namespace Agents.Core;

public static class DynamicToolInjector
{
    public static async Task<List<KernelFunction>> GetToolsAsync(string mcpEndpoint, string agentName, string? filter = null)
    {
        var functions = new List<KernelFunction>();
        try 
        {
            Console.WriteLine($"[{agentName}] Injecting tools from {mcpEndpoint}...");
            var mcpClient = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(mcpEndpoint) }));
            var tools = await mcpClient.ListToolsAsync();

            foreach (var tool in tools)
            {
                // Optional filter (e.g. for Payload agent to only get 'Payload' tools)
                if (filter != null && !tool.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;

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
                    var content = result.Content.FirstOrDefault();
                    if (content != null) { try { return ((dynamic)content).Text as string ?? string.Empty; } catch { return string.Empty; } }
                    return string.Empty;
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
