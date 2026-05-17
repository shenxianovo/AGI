using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

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
    public async Task Request_WithValidToken_Returns200()
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

        var token = _factory.GenerateToken();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new
        {
            model = "quq-1.0",
            messages = new[] { new { role = "user", content = "Hello" } }
        };

        var response = await client.PostAsJsonAsync("/v1/chat/completions", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await hubConnection.DisposeAsync();
    }

    public class AuthWebAppFactory : WebApplicationFactory<Program>
    {
        public RSA Rsa { get; } = RSA.Create(2048);
        public string Issuer => "https://auth.shenxianovo.com";
        public string Audience => "agi-api";

        public string GenerateToken()
        {
            var key = new RsaSecurityKey(Rsa);
            var creds = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims: new[] { new Claim("sub", Guid.NewGuid().ToString()) },
                expires: DateTime.UtcNow.AddMinutes(15),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>(
                    Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme,
                    opts =>
                    {
                        opts.Authority = null;
                        opts.RequireHttpsMetadata = false;
                        opts.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidIssuer = Issuer,
                            ValidateAudience = true,
                            ValidAudience = Audience,
                            ValidateLifetime = true,
                            IssuerSigningKey = new RsaSecurityKey(Rsa),
                        };
                    });
            });
        }
    }
}
