using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace AGI.Tests;

public class ToolCallTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public ToolCallTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Operator_CanReplyWithToolCall()
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
                        id = "call_123",
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
            messages = new[] { new { role = "user", content = "What's the weather?" } },
            tools = new[]
            {
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "get_weather",
                        description = "Get weather for a city",
                        parameters = new { type = "object", properties = new { city = new { type = "string" } } }
                    }
                }
            }
        };

        var response = await client.PostAsJsonAsync("/v1/chat/completions", requestBody);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var choice = json.GetProperty("choices")[0];
        Assert.Equal("tool_calls", choice.GetProperty("finish_reason").GetString());

        var message = choice.GetProperty("message");
        Assert.Equal("assistant", message.GetProperty("role").GetString());

        var toolCalls = message.GetProperty("tool_calls");
        Assert.Equal(1, toolCalls.GetArrayLength());
        Assert.Equal("call_123", toolCalls[0].GetProperty("id").GetString());
        Assert.Equal("function", toolCalls[0].GetProperty("type").GetString());
        Assert.Equal("get_weather", toolCalls[0].GetProperty("function").GetProperty("name").GetString());
        Assert.Equal("{\"city\":\"Beijing\"}", toolCalls[0].GetProperty("function").GetProperty("arguments").GetString());

        await hubConnection.DisposeAsync();
    }
}
