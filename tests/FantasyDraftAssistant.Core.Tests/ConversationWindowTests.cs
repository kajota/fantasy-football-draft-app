using FantasyDraftAssistant.Core.Ai;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;

namespace FantasyDraftAssistant.Core.Tests;

public class ConversationWindowTests
{
    private static readonly DateTimeOffset Base = new(2026, 8, 17, 19, 0, 0, TimeSpan.Zero);
    private static readonly DraftId Draft = DraftId.New();
    private static readonly BranchId Branch = BranchId.New();

    private static AiSavedResponse Saved(
        string provider,
        string prompt,
        string body,
        int minute,
        string? kind = null,
        int version = 1)
    {
        return new AiSavedResponse
        {
            ResponseId = Guid.NewGuid().ToString("D"),
            DraftId = Draft,
            BranchId = Branch,
            Provider = provider,
            Model = "test-model",
            AnalyzedStateVersion = version,
            Prompt = prompt,
            Body = body,
            RequestStartedAt = Base.AddMinutes(minute),
            PromptKind = kind
        };
    }

    [Fact]
    public void Window_keeps_only_the_same_providers_advice_turns()
    {
        var saved = new[]
        {
            Saved("xai", "Who should I take?", "Recommendation: Bijan", 1),
            Saved("openai", "Who should I take?", "Recommendation: Saquon", 2),
            Saved("xai", "Taunt Team 4", "Your roster is a museum of reaches.", 3, kind: "taunt"),
            Saved("xai", "", "RB RUN\nThree RBs in five picks.", 4, kind: "watch"),
            Saved("xai", "Compare Gibbs and Jeanty", "Gibbs by a hair.", 5, version: 3)
        };

        var window = ConversationWindow.Select(saved, "xai");

        Assert.Equal(2, window.Count);
        Assert.Equal("Who should I take?", window[0].Question);
        Assert.Equal(1, window[0].StateVersion);
        Assert.Equal("Compare Gibbs and Jeanty", window[1].Question);
        Assert.Equal(3, window[1].StateVersion);
        Assert.DoesNotContain(window, turn => turn.Answer.Contains("museum", StringComparison.Ordinal));
    }

    [Fact]
    public void Window_takes_the_latest_turns_oldest_first()
    {
        var saved = Enumerable.Range(1, 6)
            .Select(i => Saved("xai", $"Question {i}", $"Answer {i}", i, version: i))
            .ToArray();

        var window = ConversationWindow.Select(saved, "xai");

        Assert.Equal(ConversationWindow.MaxTurns, window.Count);
        Assert.Equal("Question 4", window[0].Question);
        Assert.Equal("Question 6", window[^1].Question);
    }

    [Fact]
    public void Long_answers_are_truncated_with_a_marker()
    {
        var saved = new[] { Saved("xai", "Deep dive?", new string('x', 5000), 1) };

        var window = ConversationWindow.Select(saved, "xai");

        var answer = Assert.Single(window).Answer;
        Assert.EndsWith(ConversationWindow.TruncationMarker, answer);
        Assert.True(answer.Length <= ConversationWindow.MaxAnswerChars + ConversationWindow.TruncationMarker.Length);
    }

    [Fact]
    public void Empty_prompts_get_a_placeholder_and_empty_bodies_are_skipped()
    {
        var saved = new[]
        {
            Saved("xai", "   ", "A real answer.", 1),
            Saved("xai", "Lost one", "", 2)
        };

        var window = ConversationWindow.Select(saved, "xai");

        var turn = Assert.Single(window);
        Assert.Equal("(earlier question)", turn.Question);
    }
}
