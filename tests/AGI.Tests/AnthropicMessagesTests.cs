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

    [Fact]
    public async Task PostMessages_WithToolCall_ReturnsToolUseContentBlock()
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
            var toolCall = new
            {
                tool_calls = new[]
                {
                    new
                    {
                        id = "toolu_01",
                        name = "get_weather",
                        arguments = new { city = "Beijing" }
                    }
                }
            };
            await hubConnection.InvokeAsync("ReplyWithToolCalls", id, JsonSerializer.Serialize(toolCall));
        });

        await hubConnection.StartAsync();

        var requestBody = new
        {
            model = "quq-1.0",
            max_tokens = 1024,
            messages = new[] { new { role = "user", content = "What's the weather?" } },
            tools = new[]
            {
                new
                {
                    name = "get_weather",
                    description = "Get weather for a city",
                    input_schema = new { type = "object", properties = new { city = new { type = "string" } } }
                }
            }
        };

        var response = await client.PostAsJsonAsync("/v1/messages", requestBody);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("message", json.GetProperty("type").GetString());
        Assert.Equal("assistant", json.GetProperty("role").GetString());
        Assert.Equal("tool_use", json.GetProperty("stop_reason").GetString());

        var content = json.GetProperty("content");
        Assert.Equal(1, content.GetArrayLength());
        Assert.Equal("tool_use", content[0].GetProperty("type").GetString());
        Assert.Equal("toolu_01", content[0].GetProperty("id").GetString());
        Assert.Equal("get_weather", content[0].GetProperty("name").GetString());

        var input = content[0].GetProperty("input");
        Assert.Equal("Beijing", input.GetProperty("city").GetString());

        await hubConnection.DisposeAsync();
    }
}
