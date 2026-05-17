using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace AGI.Tests;

public class RequestQueueTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public RequestQueueTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CallerReceivesOperatorReply()
    {
        var server = _factory.Server;
        var callerClient = _factory.CreateClient();

        var hubConnection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(server.BaseAddress, "/hubs/operator"),
                opts => opts.HttpMessageHandlerFactory = _ => server.CreateHandler())
            .Build();

        string? receivedRequestId = null;
        var requestReceived = new TaskCompletionSource<JsonElement>();

        hubConnection.On<JsonElement>("NewRequest", request =>
        {
            requestReceived.SetResult(request);
        });

        await hubConnection.StartAsync();

        var callerTask = Task.Run(async () =>
        {
            var request = new
            {
                model = "quq-1.0",
                messages = new[] { new { role = "user", content = "What is 2+2?" } }
            };
            return await callerClient.PostAsJsonAsync("/v1/chat/completions", request);
        });

        var incomingRequest = await requestReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var requestId = incomingRequest.GetProperty("id").GetString();

        await hubConnection.InvokeAsync("Reply", requestId, "4");

        var response = await callerTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var content = json.GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        Assert.Equal("4", content);

        await hubConnection.DisposeAsync();
    }
}
