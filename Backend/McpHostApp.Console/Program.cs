using GenerativeAI;
using GenerativeAI.Microsoft;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using OllamaSharp;
using OpenAI;

var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
var openAiModel = config["OpenAIModelName"] ?? "gpt-4o";
var openAiKey = config["OpenAIKey"];
var geminiModel = config["GoogleGeminiModel"] ?? "gemini-2.0-flash";
var geminiKey = config["GoogleGeminiKey"];
const string ollamaModel = "llama3.2";
var ollamaUrl = new Uri("http://localhost:11434");

var chatClient = BuildChatClient(ChatType.Ollama);

var mcpClient = await McpClient.CreateAsync(
    new HttpClientTransport(new HttpClientTransportOptions
    {
        Endpoint = new Uri("http://localhost:52001")
    }));

// List all available tools from the MCP server.
Console.WriteLine("Available tools:");
var tools = await mcpClient.ListToolsAsync();
foreach (var tool in tools)
{
    Console.WriteLine($"{tool}");
}

while (true)
{
    Console.Write("Prompt: ");
    var message = new ChatMessage(ChatRole.User, Console.ReadLine());

    await foreach (var update in chatClient.GetStreamingResponseAsync(message, new ChatOptions
                       {
                           Tools = [.. tools]
                       }))
    {
        if (update.Role != ChatRole.Tool) continue;
        
        if (update.Contents.FirstOrDefault() is FunctionResultContent text)
        {
            Console.WriteLine(text.Result);
        }
    }
}

IChatClient BuildChatClient(ChatType chatType)
{
    return chatType switch {
        ChatType.GoogleGemini => new ChatClientBuilder(new GenerativeAIChatClient(
                new GoogleAIPlatformAdapter(geminiKey), modelName: geminiModel))
            .Build(),
        ChatType.OpenAi => new ChatClientBuilder(new OpenAIClient(openAiKey)
                .GetChatClient(openAiModel).AsIChatClient())
                .UseFunctionInvocation()
            .Build(),
        ChatType.Ollama => new ChatClientBuilder(new OllamaApiClient(ollamaUrl, ollamaModel))
            .UseFunctionInvocation()
            .Build(),
        _ => throw new ArgumentOutOfRangeException(nameof(chatType), chatType, null)
    };
}

internal enum ChatType
{
    OpenAi,
    GoogleGemini,
    Ollama
}