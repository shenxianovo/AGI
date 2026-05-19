using System.Text.Json;
using AGI.Api.Services;
using Microsoft.AspNetCore.SignalR;
using AGI.Api.Hubs;

namespace AGI.Api.Endpoints;

public static class AsyncEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/v1/chat/completions/async", HandleAsyncSubmit).RequireAuthorization();
        app.MapGet("/v1/tasks/{taskId}", HandlePoll).RequireAuthorization();
    }

    private static async Task<IResult> HandleAsyncSubmit(
        JsonElement body,
        RequestQueue queue,
        IHubContext<OperatorHub> hubContext)
    {
        var model = body.TryGetProperty("model", out var m) ? m.GetString() : "quq-1.0";
        var id = $"chatcmpl-{Guid.NewGuid():N}";

        queue.Enqueue(id, body);

        await hubContext.Clients.All.SendAsync("NewRequest", JsonSerializer.SerializeToElement(new
        {
            id,
            model,
            messages = body.TryGetProperty("messages", out var msgs) ? msgs : default
        }));

        return Results.Accepted(value: new { task_id = id });
    }

    private static IResult HandlePoll(string taskId, RequestQueue queue, StatsService stats)
    {
        var pending = queue.Get(taskId);

        if (pending == null)
        {
            if (!CompletedTasks.TryGet(taskId, out var completed))
                return Results.NotFound(new { error = "Task not found" });

            return Results.Ok(new { status = "completed", result = completed });
        }

        if (pending.Completion.Task.IsCompleted)
        {
            var reply = pending.Completion.Task.Result;
            queue.Remove(taskId);

            var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var model = "quq-1.0";
            var inputTokens = 0;
            if (pending.RequestData is JsonElement body)
            {
                if (body.TryGetProperty("model", out var m))
                    model = m.GetString();
                inputTokens = EstimateInputTokens(body);
            }

            object result;
            if (reply.IsToolCall)
            {
                var toolCalls = reply.ToolCalls!.Select(tc => new
                {
                    id = tc.Id,
                    type = "function",
                    function = new
                    {
                        name = tc.Name,
                        arguments = JsonSerializer.Serialize(tc.Arguments)
                    }
                }).ToArray();

                result = new
                {
                    id = taskId,
                    @object = "chat.completion",
                    created,
                    model,
                    choices = new object[]
                    {
                        new
                        {
                            index = 0,
                            message = new { role = "assistant", content = (string?)null, tool_calls = toolCalls },
                            finish_reason = "tool_calls"
                        }
                    }
                };

                stats.RecordProcessedMessage(inputTokens, 0);
            }
            else
            {
                var replyContent = reply.Content ?? "";
                result = new
                {
                    id = taskId,
                    @object = "chat.completion",
                    created,
                    model,
                    choices = new object[]
                    {
                        new
                        {
                            index = 0,
                            message = new { role = "assistant", content = replyContent },
                            finish_reason = "stop"
                        }
                    }
                };

                stats.RecordProcessedMessage(inputTokens, replyContent.Length);
            }

            CompletedTasks.Store(taskId, result);
            return Results.Ok(new { status = "completed", result });
        }

        return Results.Ok(new { status = "pending" });
    }

    private static int EstimateInputTokens(JsonElement body)
    {
        var length = 0;
        if (body.TryGetProperty("messages", out var messages))
        {
            foreach (var msg in messages.EnumerateArray())
            {
                if (msg.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
                    length += c.GetString()?.Length ?? 0;
            }
        }
        return length;
    }
}

public static class CompletedTasks
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, object> _store = new();

    public static void Store(string id, object result) => _store[id] = result;

    public static bool TryGet(string id, out object? result) => _store.TryGetValue(id, out result);
}
