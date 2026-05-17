using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;

namespace AGI.Tests;

public class HeartbeatTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HeartbeatTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task StreamingRequest_SendsHeartbeatWhileWaiting()
    {
        var server = _factory.Server;
        var client = _factory.CreateClient();

        var hubConnection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(server.BaseAddress, "/hubs/operator"),
                opts => opts.HttpMessageHandlerFactory = _ => server.CreateHandler())
            .Build();

        hubConnection.On<JsonElement>("NewRequest", async request =>
        {
            var id = request.GetProperty("id").GetString();
            await Task.Delay(3500);
            await hubConnection.InvokeAsync("Reply", id, "Done");
        });

        await hubConnection.StartAsync();

        var requestBody = new
        {
            model = "quq-1.0",
            messages = new[] { new { role = "user", content = "Think hard" } },
            stream = true
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = JsonContent.Create(requestBody)
        };

        var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var dataLines = lines.Where(l => l.StartsWith("data: ")).ToList();

        var heartbeatLines = dataLines.Where(l =>
        {
            if (l == "data: [DONE]") return false;
            var json = JsonDocument.Parse(l["data: ".Length..]).RootElement;
            var choices = json.GetProperty("choices");
            if (choices.GetArrayLength() == 0) return false;
            var delta = choices[0].GetProperty("delta");
            return !delta.TryGetProperty("content", out _) ||
                   delta.GetProperty("content").ValueKind == JsonValueKind.Null;
        }).ToList();

        Assert.True(heartbeatLines.Count >= 2, $"Expected at least 2 heartbeat chunks, got {heartbeatLines.Count}");

        await hubConnection.DisposeAsync();
    }
}
