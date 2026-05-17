using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace AGI.Tests;

public class AnthropicMessagesTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public AnthropicMessagesTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostMessages_ReturnsValidAnthropicFormat()
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
            await hubConnection.InvokeAsync("Reply", id, "Hello from human");
        });

        await hubConnection.StartAsync();

        var requestBody = new
        {
            model = "quq-1.0",
            max_tokens = 1024,
            messages = new[] { new { role = "user", content = "Hello" } }
        };

        var response = await client.PostAsJsonAsync("/v1/messages", requestBody);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("message", json.GetProperty("type").GetString());
        Assert.True(json.TryGetProperty("id", out _));
        Assert.Equal("quq-1.0", json.GetProperty("model").GetString());
        Assert.Equal("assistant", json.GetProperty("role").GetString());
        Assert.Equal("end_turn", json.GetProperty("stop_reason").GetString());

        var content = json.GetProperty("content");
        Assert.Equal(1, content.GetArrayLength());
        Assert.Equal("text", content[0].GetProperty("type").GetString());
        Assert.Equal("Hello from human", content[0].GetProperty("text").GetString());

        var usage = json.GetProperty("usage");
        Assert.True(usage.TryGetProperty("input_tokens", out _));
        Assert.True(usage.TryGetProperty("output_tokens", out _));

        await hubConnection.DisposeAsync();
    }
}
