using System.Text.Json;

namespace AGI.Api.Services;

public static class TokenEstimator
{
    public static int EstimateInputTokens(JsonElement body)
    {
        var length = 0;
        if (!body.TryGetProperty("messages", out var messages)) return length;

        foreach (var msg in messages.EnumerateArray())
        {
            if (!msg.TryGetProperty("content", out var c)) continue;

            if (c.ValueKind == JsonValueKind.String)
            {
                length += c.GetString()?.Length ?? 0;
            }
            else if (c.ValueKind == JsonValueKind.Array)
            {
                foreach (var block in c.EnumerateArray())
                {
                    if (block.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                        length += text.GetString()?.Length ?? 0;
                }
            }
        }

        return length;
    }
}
