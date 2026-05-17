using System.Collections.Concurrent;

namespace AGI.Api.Services;

public class RequestQueue
{
    private readonly ConcurrentDictionary<string, PendingRequest> _pending = new();

    public PendingRequest Enqueue(string id, object requestData)
    {
        var pending = new PendingRequest(id, requestData);
        _pending[id] = pending;
        return pending;
    }

    public PendingRequest? Get(string id)
    {
        _pending.TryGetValue(id, out var pending);
        return pending;
    }

    public void Remove(string id)
    {
        _pending.TryRemove(id, out _);
    }

    public IEnumerable<PendingRequest> GetAll() => _pending.Values;
}

public class PendingRequest
{
    public string Id { get; }
    public object RequestData { get; }
    public TaskCompletionSource<string> Completion { get; } = new();

    public PendingRequest(string id, object requestData)
    {
        Id = id;
        RequestData = requestData;
    }
}
