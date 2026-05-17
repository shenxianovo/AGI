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
        JsonElement body,
        RequestQueue queue,
        IHubContext<OperatorHub> hubContext)
    {
        var model = body.TryGetProperty("model", out var m) ? m.GetString() : "quq-1.0";
        var id = $"msg_{Guid.NewGuid():N}";

        var pending = queue.Enqueue(id, body);

        await hubContext.Clients.All.SendAsync("NewRequest", JsonSerializer.SerializeToElement(new
        {
            id,
            model,
            messages = body.TryGetProperty("messages", out var msgs) ? msgs : default
        }));

        var reply = await pending.Completion.Task;

        var inputText = "";
        if (body.TryGetProperty("messages", out var messages))
        {
            foreach (var msg in messages.EnumerateArray())
            {
                if (msg.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
                    inputText += c.GetString();
            }
        }

        var replyContent = reply.Content ?? "";

        return Results.Ok(new
        {
            id,
            type = "message",
            role = "assistant",
            model,
            content = new[]
            {
                new { type = "text", text = replyContent }
            },
            stop_reason = "end_turn",
            stop_sequence = (string?)null,
            usage = new
            {
                input_tokens = inputText.Length,
                output_tokens = replyContent.Length
            }
        });
    }
}
