using System.Text.Json;
using AGI.Api.Services;
using Microsoft.AspNetCore.SignalR;
using AGI.Api.Hubs;

namespace AGI.Api.Endpoints;

public static class AnthropicMessagesEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/v1/messages", HandleAsync).RequireAuthorization();
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        JsonElement body,
        RequestQueue queue,
        IHubContext<OperatorHub> hubContext,
        StatsService stats)
    {
        var model = body.TryGetProperty("model", out var m) ? m.GetString() : "quq-1.0";
        var id = $"msg_{Guid.NewGuid():N}";
        var stream = body.TryGetProperty("stream", out var s) && s.GetBoolean();

        var pending = queue.Enqueue(id, body);

        await hubContext.Clients.All.SendAsync("NewRequest", JsonSerializer.SerializeToElement(new
        {
            id,
            model,
            messages = body.TryGetProperty("messages", out var msgs) ? msgs : default
        }));

        if (!stream)
            return await HandleNonStreamAsync(body, pending, queue, id, model, stats);

        return await HandleStreamAsync(context, pending, queue, id, model, stats);
    }

    private static async Task<IResult> HandleNonStreamAsync(
        JsonElement body, PendingRequest pending, RequestQueue queue, string id, string? model, StatsService stats)
    {
        var reply = await pending.Completion.Task;
        queue.Remove(id);

        var inputTokens = TokenEstimator.EstimateInputTokens(body);

        if (reply.IsToolCall)
        {
            var contentBlocks = reply.ToolCalls!.Select(tc => new
            {
                type = "tool_use",
                id = tc.Id,
                name = tc.Name,
                input = tc.Arguments
            }).ToArray();

            stats.RecordProcessedMessage(inputTokens, 0);

            return Results.Ok(new
            {
                id,
                type = "message",
                role = "assistant",
                model,
                content = contentBlocks,
                stop_reason = "tool_use",
                stop_sequence = (string?)null,
                usage = new { input_tokens = inputTokens, output_tokens = 0 }
            });
        }

        var replyContent = reply.Content ?? "";
        stats.RecordProcessedMessage(inputTokens, replyContent.Length);

        return Results.Ok(new
        {
            id,
            type = "message",
            role = "assistant",
            model,
            content = new[] { new { type = "text", text = replyContent } },
            stop_reason = "end_turn",
            stop_sequence = (string?)null,
            usage = new { input_tokens = inputTokens, output_tokens = replyContent.Length }
        });
    }

    private static async Task<IResult> HandleStreamAsync(
        HttpContext context, PendingRequest pending, RequestQueue queue, string id, string? model, StatsService stats)
    {
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers["Cache-Control"] = "no-cache";
        context.Response.Headers["Connection"] = "keep-alive";

        var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        await WriteEventAsync(context, "message_start", new
        {
            type = "message_start",
            message = new
            {
                id,
                type = "message",
                role = "assistant",
                model,
                content = Array.Empty<object>(),
                stop_reason = (string?)null,
                stop_sequence = (string?)null,
                usage = new { input_tokens = 0, output_tokens = 0 }
            }
        }, jsonOpts);

        using var cts = new CancellationTokenSource();
        var heartbeatTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(1000, cts.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                if (cts.Token.IsCancellationRequested) break;
                await WriteEventAsync(context, "ping", new { type = "ping" }, jsonOpts);
            }
        });

        var reply = await pending.Completion.Task;
        queue.Remove(id);
        cts.Cancel();
        await heartbeatTask;

        var inputTokens = TokenEstimator.EstimateInputTokens((JsonElement)pending.RequestData);

        if (reply.IsToolCall)
        {
            var toolCall = reply.ToolCalls![0];

            await WriteEventAsync(context, "content_block_start", new
            {
                type = "content_block_start",
                index = 0,
                content_block = new { type = "tool_use", id = toolCall.Id, name = toolCall.Name }
            }, jsonOpts);

            var inputJson = JsonSerializer.Serialize(toolCall.Arguments);
            foreach (var ch in inputJson)
            {
                await WriteEventAsync(context, "content_block_delta", new
                {
                    type = "content_block_delta",
                    index = 0,
                    delta = new { type = "input_json_delta", partial_json = ch.ToString() }
                }, jsonOpts);
            }

            await WriteEventAsync(context, "content_block_stop", new
            {
                type = "content_block_stop",
                index = 0
            }, jsonOpts);

            await WriteEventAsync(context, "message_delta", new
            {
                type = "message_delta",
                delta = new { stop_reason = "tool_use", stop_sequence = (string?)null },
                usage = new { output_tokens = 0 }
            }, jsonOpts);

            await WriteEventAsync(context, "message_stop", new { type = "message_stop" }, jsonOpts);

            stats.RecordProcessedMessage(inputTokens, 0);

            return Results.Empty;
        }

        await WriteEventAsync(context, "content_block_start", new
        {
            type = "content_block_start",
            index = 0,
            content_block = new { type = "text", text = "" }
        }, jsonOpts);

        var replyContent = reply.Content ?? "";

        foreach (var ch in replyContent)
        {
            await WriteEventAsync(context, "content_block_delta", new
            {
                type = "content_block_delta",
                index = 0,
                delta = new { type = "text_delta", text = ch.ToString() }
            }, jsonOpts);
        }

        await WriteEventAsync(context, "content_block_stop", new
        {
            type = "content_block_stop",
            index = 0
        }, jsonOpts);

        await WriteEventAsync(context, "message_delta", new
        {
            type = "message_delta",
            delta = new { stop_reason = "end_turn", stop_sequence = (string?)null },
            usage = new { output_tokens = replyContent.Length }
        }, jsonOpts);

        await WriteEventAsync(context, "message_stop", new { type = "message_stop" }, jsonOpts);

        stats.RecordProcessedMessage(inputTokens, replyContent.Length);

        return Results.Empty;
    }

    private static async Task WriteEventAsync(HttpContext context, string eventType, object data, JsonSerializerOptions opts)
    {
        var json = JsonSerializer.Serialize(data, opts);
        await context.Response.WriteAsync($"event: {eventType}\ndata: {json}\n\n");
        await context.Response.Body.FlushAsync();
    }
}