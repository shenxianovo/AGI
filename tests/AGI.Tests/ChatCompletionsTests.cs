using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AGI.Tests;

public class ChatCompletionsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ChatCompletionsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostChatCompletions_ReturnsValidOpenAIFormat()
    {
        var request = new
        {
            model = "quq-1.0",
            messages = new[]
            {
                new { role = "user", content = "Hello" }
            }
        };

        var response = await _client.PostAsJsonAsync("/v1/chat/completions", request);

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
        Assert.True(message.TryGetProperty("content", out _));
    }
}
