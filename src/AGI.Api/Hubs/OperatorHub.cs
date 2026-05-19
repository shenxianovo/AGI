using System.Text.Json;
using AGI.Api.Services;
using Microsoft.AspNetCore.SignalR;

namespace AGI.Api.Hubs;

public class OperatorHub : Hub
{
    private readonly RequestQueue _queue;
    private readonly OperatorPresence _presence;

    public OperatorHub(RequestQueue queue, OperatorPresence presence)
    {
        _queue = queue;
        _presence = presence;
    }

    public override Task OnConnectedAsync()
    {
        _presence.OnConnected();
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _presence.OnDisconnected();
        return base.OnDisconnectedAsync(exception);
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

public class OperatorPresence
{
    private int _connectionCount = 0;

    public void OnConnected() => Interlocked.Increment(ref _connectionCount);
    public void OnDisconnected() => Interlocked.Decrement(ref _connectionCount);
    public bool IsOnline => Interlocked.CompareExchange(ref _connectionCount, 0, 0) > 0;
}
