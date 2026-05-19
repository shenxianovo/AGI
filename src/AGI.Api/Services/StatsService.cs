namespace AGI.Api.Services;

public class StatsService
{
    private long _processedMessages = 0;
    private long _inputTokens = 0;
    private long _outputTokens = 0;

    public void RecordProcessedMessage(int inputTokens, int outputTokens)
    {
        Interlocked.Increment(ref _processedMessages);
        Interlocked.Add(ref _inputTokens, inputTokens);
        Interlocked.Add(ref _outputTokens, outputTokens);
    }

    public StatsSnapshot GetSnapshot(int pendingCount, bool operatorOnline)
    {
        return new StatsSnapshot(
            Interlocked.Read(ref _processedMessages),
            Interlocked.Read(ref _inputTokens),
            Interlocked.Read(ref _outputTokens),
            pendingCount,
            operatorOnline
        );
    }
}

public record StatsSnapshot(
    long ProcessedMessages,
    long InputTokens,
    long OutputTokens,
    int PendingMessages,
    bool OperatorOnline
);
