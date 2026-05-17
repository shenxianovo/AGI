using System.Text.Json;
using AGI.Api.Hubs;
using AGI.Api.Services;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<RequestQueue>();

var app = builder.Build();

app.MapHub<OperatorHub>("/hubs/operator");

app.MapPost("/v1/chat/completions", async (HttpContext context, JsonElement body, RequestQueue queue, IHubContext<OperatorHub> hubContext) =>
{
    var model = body.TryGetProperty("model", out var m) ? m.GetString() : "quq-1.0";
    var id = $"chatcmpl-{Guid.NewGuid():N}";
    var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var stream = body.TryGetProperty("stream", out var s) && s.GetBoolean();

    var pending = queue.Enqueue(id, body);

    await hubContext.Clients.All.SendAsync("NewRequest", JsonSerializer.SerializeToElement(new
    {
        id,
        model,
        messages = body.TryGetProperty("messages", out var msgs) ? msgs : default
    }));

    var content = await pending.Completion.Task;

    if (!stream)
    {
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
                    message = new { role = "assistant", content },
                    finish_reason = "stop"
                }
            }
        });
    }

    context.Response.ContentType = "text/event-stream";
    context.Response.Headers["Cache-Control"] = "no-cache";
    context.Response.Headers["Connection"] = "keep-alive";

    var writer = context.Response.BodyWriter;
    var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    foreach (var ch in content)
    {
        var chunk = new
        {
            id,
            @object = "chat.completion.chunk",
            created,
            model,
            choices = new[]
            {
                new
                {
                    index = 0,
                    delta = new { content = ch.ToString() },
                    finish_reason = (string?)null
                }
            }
        };
        var json = JsonSerializer.Serialize(chunk, jsonOptions);
        await context.Response.WriteAsync($"data: {json}\n\n");
        await context.Response.Body.FlushAsync();
    }

    var finalChunk = new
    {
        id,
        @object = "chat.completion.chunk",
        created,
        model,
        choices = new[]
        {
            new
            {
                index = 0,
                delta = new { content = (string?)null },
                finish_reason = "stop"
            }
        }
    };
    var finalJson = JsonSerializer.Serialize(finalChunk, jsonOptions);
    await context.Response.WriteAsync($"data: {finalJson}\n\n");
    await context.Response.WriteAsync("data: [DONE]\n\n");
    await context.Response.Body.FlushAsync();

    return Results.Empty;
});

app.Run();

public partial class Program { }
