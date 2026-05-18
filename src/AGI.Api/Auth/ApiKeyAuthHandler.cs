using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AGI.Api.Auth;

public class ApiKeyAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly string _authServiceUrl;

    public ApiKeyAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _authServiceUrl = configuration["AuthServiceUrl"]
            ?? throw new InvalidOperationException("AuthServiceUrl is not configured");
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? token = null;

        if (Request.Headers.TryGetValue("x-api-key", out var xApiKey))
        {
            token = xApiKey.ToString().Trim();
        }
        else if (Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            token = authHeader.ToString().Replace("Bearer ", "").Trim();
        }

        if (string.IsNullOrEmpty(token))
            return AuthenticateResult.Fail("Missing API key");

        var cacheKey = $"apikey:{token}";
        if (_cache.TryGetValue(cacheKey, out AuthenticationTicket? cachedTicket))
            return AuthenticateResult.Success(cachedTicket!);

        var client = _httpClientFactory.CreateClient("AuthService");
        var response = await client.PostAsJsonAsync(
            $"{_authServiceUrl}/api/v1/apikeys/exchange",
            new { apiKey = token });

        if (!response.IsSuccessStatusCode)
            return AuthenticateResult.Fail("Invalid API key");

        var result = await response.Content.ReadFromJsonAsync<ExchangeResponse>();

        var claims = new[] { new Claim(ClaimTypes.Name, "caller") };
        var identity = new ClaimsIdentity(claims, "ApiKey");
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), "ApiKey");

        var ttl = result?.ExpiresIn > 0
            ? TimeSpan.FromSeconds(result.ExpiresIn / 2)
            : TimeSpan.FromMinutes(5);
        _cache.Set(cacheKey, ticket, ttl);

        return AuthenticateResult.Success(ticket);
    }

    private sealed class ExchangeResponse
    {
        [JsonPropertyName("accessToken")]
        public string AccessToken { get; set; } = "";

        [JsonPropertyName("expiresIn")]
        public long ExpiresIn { get; set; }
    }
}
