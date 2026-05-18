using AGI.Api.Endpoints;
using AGI.Api.Hubs;
using AGI.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<RequestQueue>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = null;
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidIssuer = "AuthService",
            ValidateAudience = true,
            ValidAudience = "AuthService",
            ValidateLifetime = true,
        };
        options.MetadataAddress = "https://auth.shenxianovo.com/.well-known/openid-configuration";
    });
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

app.UseAuthentication();
app.UseAuthorization();
app.UseCors();

app.MapHub<OperatorHub>("/hubs/operator");
ChatCompletionsEndpoint.Map(app);
AnthropicMessagesEndpoint.Map(app);
AsyncEndpoints.Map(app);

app.Run();

public partial class Program { }
