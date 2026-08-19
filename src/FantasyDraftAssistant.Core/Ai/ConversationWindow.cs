using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.Core.Ai;

/// <summary>
/// Picks the short conversation history sent with a manual ask: the same
/// provider's recent advice turns, oldest first, with long answers trimmed.
/// Taunt and watch turns never enter the window.
/// </summary>
public static class ConversationWindow
{
    public const int MaxTurns = 3;
    public const int MaxAnswerChars = 1200;
    public const string TruncationMarker = " …[truncated]";

    public static IReadOnlyList<AiConversationExchange> Select(
        IReadOnlyList<AiSavedResponse> saved,
        string providerKey,
        int maxTurns = MaxTurns,
        int maxAnswerChars = MaxAnswerChars)
    {
        return saved
            .Where(r => string.Equals(r.Provider, providerKey, StringComparison.OrdinalIgnoreCase))
            .Where(r => string.IsNullOrEmpty(r.PromptKind))
            .Where(r => !string.IsNullOrWhiteSpace(r.Body))
            .OrderBy(r => r.RequestStartedAt)
            .TakeLast(maxTurns)
            .Select(r => new AiConversationExchange
            {
                Question = string.IsNullOrWhiteSpace(r.Prompt) ? "(earlier question)" : r.Prompt.Trim(),
                Answer = Truncate(r.Body.Trim(), maxAnswerChars),
                StateVersion = r.AnalyzedStateVersion
            })
            .ToList();
    }

    public static string Truncate(string text, int maxChars) =>
        text.Length <= maxChars
            ? text
            : text[..maxChars] + TruncationMarker;
}
