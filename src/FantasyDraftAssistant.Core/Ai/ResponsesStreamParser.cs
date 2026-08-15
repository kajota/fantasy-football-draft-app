using System.Text.Json;

namespace FantasyDraftAssistant.Core.Ai;

public static class ResponsesStreamParser
{
    public static string? ExtractVisibleDelta(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var type = ReadString(root, "type") ?? ReadString(root, "event");

            if (type is not null)
            {
                if (IsReasoning(type) || IsSnapshot(type))
                    return null;
                if (IsOutputDelta(type))
                    return ReadString(root, "delta") ?? ReadNestedDelta(root);
                return null;
            }

            if (root.TryGetProperty("choices", out var choices)
                && choices.ValueKind == JsonValueKind.Array
                && choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("delta", out var chatDelta)
                    && chatDelta.TryGetProperty("content", out var content)
                    && content.ValueKind == JsonValueKind.String)
                {
                    return content.GetString();
                }
            }

            return ReadString(root, "delta");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsReasoning(string type)
    {
        var value = type.ToLowerInvariant();
        return value.Contains("reasoning", StringComparison.Ordinal)
               || value.Contains("thinking", StringComparison.Ordinal);
    }

    private static bool IsSnapshot(string type)
    {
        var value = type.ToLowerInvariant();
        return value.Contains("done", StringComparison.Ordinal)
               || value.Contains("completed", StringComparison.Ordinal)
               || value.Contains("output_item", StringComparison.Ordinal);
    }

    private static bool IsOutputDelta(string type)
    {
        var value = type.ToLowerInvariant();
        return value.Contains("output_text.delta", StringComparison.Ordinal)
               || value.Equals("response.output_text.delta", StringComparison.Ordinal)
               || value.EndsWith(".content.delta", StringComparison.Ordinal);
    }

    private static string? ReadNestedDelta(JsonElement root)
    {
        if (root.TryGetProperty("delta", out var delta) && delta.ValueKind == JsonValueKind.Object)
            return ReadString(delta, "text") ?? ReadString(delta, "content");
        return null;
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
