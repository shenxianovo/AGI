using AGI.Api.Hubs;
using AGI.Api.Services;

namespace AGI.Api.Endpoints;

public static class StatusEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/status", Handle);
    }

    private static IResult Handle(
        RequestQueue queue,
        StatsService stats,
        OperatorPresence presence,
        IConfiguration config)
    {
        var pendingCount = queue.GetAll().Count();
        var snapshot = stats.GetSnapshot(pendingCount, presence.IsOnline);

        return Results.Ok(new
        {
            processed_messages = snapshot.ProcessedMessages,
            input_tokens = snapshot.InputTokens,
            output_tokens = snapshot.OutputTokens,
            pending_messages = snapshot.PendingMessages,
            operator_online = snapshot.OperatorOnline,
            public_endpoint = config["PublicEndpoint"],
            example_api_key = config["ExampleApiKey"]
        });
    }
}
