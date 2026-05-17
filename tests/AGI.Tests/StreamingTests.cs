using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;

namespace AGI.Tests;

public class StreamingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public StreamingTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostChatCompletions_WithStreamTrue_ReturnsSSEChunks()
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
            await hubConnection.InvokeAsync("Reply", id, "Hi there");
        });

        await hubConnection.StartAsync();

        var requestBody = new
        {
            model = "quq-1.0",
            messages = new[] { new { role = "user", content = "Hello" } },
            stream = true
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = JsonContent.Create(requestBody)
        };

        var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var dataLines = lines.Where(l => l.StartsWith("data: ")).ToList();
        Assert.True(dataLines.Count >= 2, $"Expected at least 2 data lines, got {dataLines.Count}");

        var lastDataLine = dataLines.Last();
        Assert.Equal("data: [DONE]", lastDataLine);

        var contentChunks = new StringBuilder();
        foreach (var line in dataLines.Where(l => l != "data: [DONE]"))
        {
            var json = JsonDocument.Parse(line["data: ".Length..]).RootElement;
            Assert.Equal("chat.completion.chunk", json.GetProperty("object").GetString());
            Assert.Equal("quq-1.0", json.GetProperty("model").GetString());

            var delta = json.GetProperty("choices")[0].GetProperty("delta");
            if (delta.TryGetProperty("content", out var c))
            {
                contentChunks.Append(c.GetString());
            }
        }

        Assert.Equal("Hi there", contentChunks.ToString());

        await hubConnection.DisposeAsync();
    }
}
