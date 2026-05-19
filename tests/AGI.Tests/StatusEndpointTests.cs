using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AGI.Tests;

public class StatusEndpointTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public StatusEndpointTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetStatus_ReturnsStatsAndConfig()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(json.TryGetProperty("processed_messages", out _));
        Assert.True(json.TryGetProperty("input_tokens", out _));
        Assert.True(json.TryGetProperty("output_tokens", out _));
        Assert.True(json.TryGetProperty("pending_messages", out _));
        Assert.True(json.TryGetProperty("operator_online", out _));
        Assert.True(json.TryGetProperty("public_endpoint", out _));
        Assert.True(json.TryGetProperty("example_api_key", out _));

        Assert.Equal("https://chat.shenxianovo.com", json.GetProperty("public_endpoint").GetString());
        Assert.Equal("sk-example-xxxxxxxxxxxxxxxx", json.GetProperty("example_api_key").GetString());
    }
}
