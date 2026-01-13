using Microsoft.AspNetCore.SignalR.Client;

namespace McpServer.FlightControl;

public class CommunicationService(string hubUrl, HttpClient httpClient) : IAsyncDisposable
{
    private readonly HubConnection _connection = new HubConnectionBuilder()
        .WithUrl(hubUrl)
        .WithAutomaticReconnect()
        .Build();

    public async Task StartAsync() => await _connection.StartAsync();


    public async Task NavigateTo(string location)
    {
        await _connection.SendAsync("NavigateTo", location);
    }

    public async Task ChangeSpeed(int speed)
    {
        await _connection.SendAsync("NavigateTo", speed);
    }
    public async Task ChangeAltitude(double altitude)
    {
        await _connection.SendAsync("ChangeAltitude", altitude);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        httpClient.Dispose();
    }
}
