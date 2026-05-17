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

    public async Task Reply(string requestId, string content)
    {
        var pending = _queue.Get(requestId);
        if (pending != null)
        {
            pending.Completion.TrySetResult(content);
            _queue.Remove(requestId);
        }
    }
}
