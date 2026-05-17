using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapPost("/v1/chat/completions", (JsonElement body) =>
{
    var model = body.TryGetProperty("model", out var m) ? m.GetString() : "quq-1.0";
    var id = $"chatcmpl-{Guid.NewGuid():N}";
    var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    return Results.Ok(new
    {
        id,
        @object = "chat.completion",
        created,
        model,
        choices = new[]
        {
            new
            {
                index = 0,
                message = new { role = "assistant", content = (string?)null },
                finish_reason = "stop"
            }
        }
    });
});

app.Run();

public partial class Program { }
