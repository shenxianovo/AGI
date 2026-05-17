using System.Text.Json;
using AGI.Api.Services;
using Microsoft.AspNetCore.SignalR;
using AGI.Api.Hubs;

namespace AGI.Api.Endpoints;

public static class ChatCompletionsEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/v1/chat/completions", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        JsonElement body,
        RequestQueue queue,
        IHubContext<OperatorHub> hubContext)
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

        if (!stream)
        {
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
        }

        return await HandleStreamingAsync(context, pending, id, model, created);
    }

    private static async Task<IResult> HandleStreamingAsync(
        HttpContext context,
        PendingRequest pending,
        string id,
        string? model,
        long created)
    {
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers["Cache-Control"] = "no-cache";
        context.Response.Headers["Connection"] = "keep-alive";

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        using var cts = new CancellationTokenSource();
        var heartbeatTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(1000, cts.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                if (cts.Token.IsCancellationRequested) break;
                var heartbeat = new
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
                            finish_reason = (string?)null
                        }
                    }
                };
                var hbJson = JsonSerializer.Serialize(heartbeat, jsonOptions);
                await context.Response.WriteAsync($"data: {hbJson}\n\n");
                await context.Response.Body.FlushAsync();
            }
        });

        var streamContent = await pending.Completion.Task;
        cts.Cancel();
        await heartbeatTask;

        foreach (var ch in streamContent)
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
    }
}
