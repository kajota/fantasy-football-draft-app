using FantasyDraftAssistant.Core.Ai;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.Core.Tests;

public class DraftWatcherTriggerTests
{
    [Fact]
    public void Unseen_returns_only_new_alert_messages()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal)
        {
            DraftWatcherTrigger.Fingerprint(Alert(AlertKind.Value, "old"))
        };
        var fresh = DraftWatcherTrigger.Unseen(
        [
            Alert(AlertKind.Value, "old"),
            Alert(AlertKind.PickApproaching, "Your pick is next.")
        ], seen);

        var next = Assert.Single(fresh);
        Assert.Equal(AlertKind.PickApproaching, next.Kind);
        Assert.Equal("Your pick is next.", next.Message);
    }

    [Fact]
    public void Watcher_role_is_detected()
    {
        Assert.True(AiAnalysisMode.IsWatcher(AiAnalysisMode.DraftWatcher));
        Assert.False(AiAnalysisMode.IsWatcher(AiAnalysisMode.FastAdvisor));
        Assert.False(AiAnalysisMode.IsDeep(askDeep: false, AiAnalysisMode.DraftWatcher));
    }

    private static DraftAlert Alert(AlertKind kind, string message) =>
        new() { Kind = kind, Message = message, Severity = 1 };
}