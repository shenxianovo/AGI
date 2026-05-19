using System.Text.Json;
using AGI.Api.Services;
using Microsoft.AspNetCore.SignalR;

namespace AGI.Api.Hubs;

public class OperatorHub : Hub
{
    private readonly RequestQueue _queue;

    public OperatorHub(RequestQueue queue)
    {
        _queue = queue;
    }

    public void Reply(string requestId, string content)
    {
        var pending = _queue.Get(requestId);
        if (pending != null)
        {
            pending.Completion.TrySetResult(new OperatorReply { Content = content });
        }
    }

    public void ReplyWithToolCalls(string requestId, string toolCallsJson)
    {
        var pending = _queue.Get(requestId);
        if (pending == null) return;

        var doc = JsonDocument.Parse(toolCallsJson);
        var toolCalls = new List<ToolCallRequest>();
        foreach (var tc in doc.RootElement.GetProperty("tool_calls").EnumerateArray())
        {
            toolCalls.Add(new ToolCallRequest(
                tc.GetProperty("id").GetString()!,
                tc.GetProperty("name").GetString()!,
                tc.GetProperty("arguments").Clone()
            ));
        }

        pending.Completion.TrySetResult(new OperatorReply { ToolCalls = toolCalls });
    }
}
