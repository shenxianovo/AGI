using AGI.Api.Endpoints;
using AGI.Api.Hubs;
using AGI.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<RequestQueue>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"] ?? "https://auth.shenxianovo.com";
        options.Audience = builder.Configuration["Auth:Audience"] ?? "agi-api";
        options.RequireHttpsMetadata = true;
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapHub<OperatorHub>("/hubs/operator");
ChatCompletionsEndpoint.Map(app);
AnthropicMessagesEndpoint.Map(app);

app.Run();

public partial class Program { }
