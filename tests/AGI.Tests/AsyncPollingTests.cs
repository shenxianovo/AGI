using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace AGI.Tests;

public class AsyncPollingTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public AsyncPollingTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostWithAsyncMode_Returns202_ThenPollForResult()
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
            await Task.Delay(500);
            await hubConnection.InvokeAsync("Reply", id, "Async result");
        });

        await hubConnection.StartAsync();

        var requestBody = new
        {
            model = "quq-1.0",
            messages = new[] { new { role = "user", content = "Hello" } }
        };

        var response = await client.PostAsJsonAsync("/v1/chat/completions/async", requestBody);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var accepted = await response.Content.ReadFromJsonAsync<JsonElement>();
        var taskId = accepted.GetProperty("task_id").GetString();
        Assert.NotNull(taskId);

        // Poll - should be pending initially
        var pollResponse = await client.GetAsync($"/v1/tasks/{taskId}");
        Assert.Equal(HttpStatusCode.OK, pollResponse.StatusCode);
        var pollJson = await pollResponse.Content.ReadFromJsonAsync<JsonElement>();
        var status = pollJson.GetProperty("status").GetString();

        // Wait for operator to reply
        await Task.Delay(1000);

        // Poll again - should be completed
        pollResponse = await client.GetAsync($"/v1/tasks/{taskId}");
        Assert.Equal(HttpStatusCode.OK, pollResponse.StatusCode);
        pollJson = await pollResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("completed", pollJson.GetProperty("status").GetString());

        var result = pollJson.GetProperty("result");
        Assert.Equal("chat.completion", result.GetProperty("object").GetString());
        var content = result.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        Assert.Equal("Async result", content);

        await hubConnection.DisposeAsync();
    }
}
