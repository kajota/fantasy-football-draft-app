using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;

namespace FantasyDraftAssistant.Core.Engine;

/// <param name="Key">Stable identifier, stored on the seat policy.</param>
/// <param name="Title">Shown only after the draft ends.</param>
/// <param name="PromptLine">The private brief handed to the model.</param>
/// <param name="Proxy">
/// Nearest deterministic personality. Stands in for the seat wherever a pick has to be
/// guessed for free — above all the turn outlook, which simulates dozens of picks
/// synchronously and must never reach an AI provider.
/// </param>
public sealed record MockAiStrategy(
    string Key,
    string Title,
    string PromptLine,
    MockPersonality Proxy);

/// The strategy an AI seat drafts to. Picked deterministically from the draft, branch,
/// and team so a seat behaves consistently for a whole draft and differently in the
/// next one — without spending a call to invent one.
public static class MockAiStrategyCatalog
{
    public static IReadOnlyList<MockAiStrategy> All { get; } =
    [
        new("balanced", "Best player available",
            "Take the best player on the board. Deviate from raw rank only when roster need or a tier cliff justifies it.",
            MockPersonality.BestAvailable),
        new("zero-rb", "Zero RB",
            "Avoid running backs early. Load up on receivers and elite pass-catchers, then attack running back in the middle rounds where the value is.",
            MockPersonality.ZeroRb),
        new("rb-anchor", "RB anchor",
            "Secure one workhorse running back early, then let the position go and take value elsewhere until late.",
            MockPersonality.HeroRb),
        new("wr-stack", "Receiver heavy",
            "Prioritise wide receivers through the early and middle rounds. Accept a thinner backfield to win at receiver.",
            MockPersonality.WrHeavy),
        new("contrarian", "Contrarian",
            "Fade whatever the room is doing. When a run starts at a position, deliberately take value at a different one and come back when the run exhausts.",
            MockPersonality.BestAvailable),
        new("upside", "Upside chaser",
            "Prefer young players and ceiling over safe floor. Reach a round early for someone who could break out rather than take the steady veteran.",
            MockPersonality.RookieHunter),
        new("floor", "Floor hunter",
            "Prefer proven, durable producers. Avoid rookies and injury risks even when the upside case is loud.",
            MockPersonality.AdpHunter),
        new("run-rider", "Run rider",
            "Watch for positional runs and get ahead of them. If a position is emptying fast, take yours before the tier breaks.",
            MockPersonality.BestAvailable),
        new("late-qb", "Late quarterback",
            "Wait on quarterback as long as the format allows, spending early picks on running backs and receivers.",
            MockPersonality.LateQb),
        new("early-qb", "Early quarterback",
            "Take a quarterback earlier than the room does and build around that edge — but respect the format: in a 1-QB league that means rounds 3-5, not the first round.",
            MockPersonality.QbEarly)
    ];

    public static MockAiStrategy Find(string? key) =>
        All.FirstOrDefault(strategy => string.Equals(strategy.Key, key, StringComparison.OrdinalIgnoreCase))
        ?? All[0];

    /// Deterministic for a given seat in a given branch, so the seat does not change
    /// character mid-draft and a re-run of the same branch behaves the same way.
    public static MockAiStrategy ForSeat(DraftId draftId, BranchId branchId, TeamId teamId)
    {
        var seed = HashCode.Combine(draftId.Value, branchId.Value, teamId.Value);
        var index = (int)((uint)seed % (uint)All.Count);
        return All[index];
    }
}
