using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace AGI.Tests;

public class AuthTests : IClassFixture<AuthTests.AuthWebAppFactory>
{
    private readonly AuthWebAppFactory _factory;

    public AuthTests(AuthWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Request_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var request = new
        {
            model = "quq-1.0",
            messages = new[] { new { role = "user", content = "Hello" } }
        };

        var response = await client.PostAsJsonAsync("/v1/chat/completions", request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithInvalidKey_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "ak_invalid_key");

        var request = new
        {
            model = "quq-1.0",
            messages = new[] { new { role = "user", content = "Hello" } }
        };

        var response = await client.PostAsJsonAsync("/v1/chat/completions", request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithValidKey_Returns200()
    {
        var server = _factory.Server;
        var client = _factory.CreateClient();

        var hubConnection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(server.BaseAddress, "/hubs/operator"),
                opts => opts.HttpMessageHandlerFactory = _ => server.CreateHandler())
            .Build();

        hubConnection.On<JsonElement>("NewRequest", async req =>
        {
            var id = req.GetProperty("id").GetString();
            await hubConnection.InvokeAsync("Reply", id, "Authenticated!");
        });

        await hubConnection.StartAsync();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthWebAppFactory.ValidApiKey);

        var request = new
        {
            model = "quq-1.0",
            messages = new[] { new { role = "user", content = "Hello" } }
        };

        var response = await client.PostAsJsonAsync("/v1/chat/completions", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await hubConnection.DisposeAsync();
    }

    [Fact]
    public async Task Request_WithValidKey_UsesCacheOnSecondCall()
    {
        var server = _factory.Server;
        var client = _factory.CreateClient();

        var hubConnection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(server.BaseAddress, "/hubs/operator"),
                opts => opts.HttpMessageHandlerFactory = _ => server.CreateHandler())
            .Build();

        hubConnection.On<JsonElement>("NewRequest", async req =>
        {
            var id = req.GetProperty("id").GetString();
            await hubConnection.InvokeAsync("Reply", id, "OK");
        });

        await hubConnection.StartAsync();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthWebAppFactory.CacheTestApiKey);

        var request = new
        {
            model = "quq-1.0",
            messages = new[] { new { role = "user", content = "Hello" } }
        };

        var callCountBefore = _factory.ExchangeCallCount;
        await client.PostAsJsonAsync("/v1/chat/completions", request);
        await client.PostAsJsonAsync("/v1/chat/completions", request);
        var callCountAfter = _factory.ExchangeCallCount;

        Assert.Equal(1, callCountAfter - callCountBefore);

        await hubConnection.DisposeAsync();
    }

    public class AuthWebAppFactory : WebApplicationFactory<Program>
    {
        public const string ValidApiKey = "ak_testpfx1_validSecretForTesting123456";
        public const string CacheTestApiKey = "ak_testpfx2_cacheTestSecretValue789012";
        public int ExchangeCallCount;

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting("AuthServiceUrl", "https://mock-auth.local");

            builder.ConfigureServices(services =>
            {
                services.ConfigureAll<Microsoft.Extensions.Http.HttpClientFactoryOptions>(opts =>
                {
                    opts.HttpMessageHandlerBuilderActions.Add(b =>
                    {
                        b.PrimaryHandler = new MockAuthServiceHandler(this);
                    });
                });
            });
        }

        private class MockAuthServiceHandler : HttpMessageHandler
        {
            private readonly AuthWebAppFactory _factory;

            public MockAuthServiceHandler(AuthWebAppFactory factory) => _factory = factory;

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref _factory.ExchangeCallCount);

                var content = request.Content!.ReadAsStringAsync(cancellationToken).Result;
                var json = JsonDocument.Parse(content);
                var apiKey = json.RootElement.GetProperty("apiKey").GetString();

                if (apiKey == ValidApiKey || apiKey == CacheTestApiKey)
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new { accessToken = "jwt-token", expiresIn = 600 })
                    };
                    return Task.FromResult(response);
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            }
        }
    }
}
