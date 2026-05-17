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
            _queue.Remove(requestId);
        }
    }

    public void ReplyWithToolCalls(string requestId, string toolCallsJson)
    {
        var pending = _queue.Get(requestId);
        if (pending != null)
        {
            pending.Completion.TrySetResult(new OperatorReply { ToolCallsJson = toolCallsJson });
            _queue.Remove(requestId);
        }
    }
}
