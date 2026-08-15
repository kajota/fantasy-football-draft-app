using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.Core.Ai;

public static class DraftWatcherTrigger
{
    public const string PromptKind = "watch";

    public static string Fingerprint(DraftAlert alert) => $"{alert.Kind}|{alert.Message}";

    public static IReadOnlyList<DraftAlert> Unseen(
        IEnumerable<DraftAlert> current,
        IReadOnlySet<string> seen) =>
        current.Where(alert => !seen.Contains(Fingerprint(alert))).ToList();
}