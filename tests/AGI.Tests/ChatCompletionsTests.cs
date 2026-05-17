using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace AGI.Tests;

public class ChatCompletionsTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public ChatCompletionsTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostChatCompletions_ReturnsValidOpenAIFormat()
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
            await hubConnection.InvokeAsync("Reply", id, "Hello back!");
        });

        await hubConnection.StartAsync();

        var requestBody = new
        {
            model = "quq-1.0",
            messages = new[] { new { role = "user", content = "Hello" } }
        };

        var response = await client.PostAsJsonAsync("/v1/chat/completions", requestBody);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("chat.completion", json.GetProperty("object").GetString());
        Assert.True(json.TryGetProperty("id", out _));
        Assert.True(json.TryGetProperty("created", out _));
        Assert.Equal("quq-1.0", json.GetProperty("model").GetString());

        var choices = json.GetProperty("choices");
        Assert.Equal(1, choices.GetArrayLength());

        var choice = choices[0];
        Assert.Equal(0, choice.GetProperty("index").GetInt32());
        Assert.Equal("stop", choice.GetProperty("finish_reason").GetString());

        var message = choice.GetProperty("message");
        Assert.Equal("assistant", message.GetProperty("role").GetString());
        Assert.Equal("Hello back!", message.GetProperty("content").GetString());

        await hubConnection.DisposeAsync();
    }
}
