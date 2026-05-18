using AGI.Api.Auth;
using AGI.Api.Endpoints;
using AGI.Api.Hubs;
using AGI.Api.Services;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<RequestQueue>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("AuthService");

builder.Services.AddAuthentication("ApiKey")
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>("ApiKey", _ => { });
builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
  {
      options.AddDefaultPolicy(policy =>
          policy.WithOrigins("http://localhost:5173")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
  });

var app = builder.Build();

app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("RequestLog");
    logger.LogInformation("{Method} {Path}", context.Request.Method, context.Request.Path);
    await next();
});

app.UseAuthentication();
app.UseAuthorization();
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHub<OperatorHub>("/hubs/operator");
ChatCompletionsEndpoint.Map(app);
AnthropicMessagesEndpoint.Map(app);
AsyncEndpoints.Map(app);

app.MapFallbackToFile("index.html");

app.Run();

public partial class Program { }
