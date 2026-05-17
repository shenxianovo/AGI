using System.Text.Json;
using AGI.Api.Hubs;
using AGI.Api.Services;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<RequestQueue>();

var app = builder.Build();

app.MapHub<OperatorHub>("/hubs/operator");

app.MapPost("/v1/chat/completions", async (JsonElement body, RequestQueue queue, IHubContext<OperatorHub> hubContext) =>
{
    var model = body.TryGetProperty("model", out var m) ? m.GetString() : "quq-1.0";
    var id = $"chatcmpl-{Guid.NewGuid():N}";
    var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    var pending = queue.Enqueue(id, body);

    await hubContext.Clients.All.SendAsync("NewRequest", JsonSerializer.SerializeToElement(new
    {
        id,
        model,
        messages = body.TryGetProperty("messages", out var msgs) ? msgs : default
    }));

    var content = await pending.Completion.Task;

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
});

app.Run();

public partial class Program { }
