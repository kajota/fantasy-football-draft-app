using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.Core.Tests;

public class KeeperRulesTests
{
    [Fact]
    public void Seats_stay_editable_on_a_started_board_with_no_regular_picks()
    {
        Assert.True(KeeperRules.CanReorderSeats(DraftStatus.NotStarted, []));
        Assert.True(KeeperRules.CanReorderSeats(DraftStatus.InProgress, []));
        Assert.True(KeeperRules.CanReorderSeats(DraftStatus.InProgress, [Keeper()]));
        Assert.False(KeeperRules.CanReorderSeats(DraftStatus.InProgress, [Regular()]));
        Assert.False(KeeperRules.CanReorderSeats(DraftStatus.Completed, []));
    }

    private static ActiveSelection Keeper() => Selection(PickSource.Keeper);

    private static ActiveSelection Regular() => Selection(PickSource.Manual);

    private static ActiveSelection Selection(PickSource source) => new()
    {
        EventId = EventId.New(),
        DraftId = DraftId.New(),
        BranchId = BranchId.New(),
        DraftSlotId = DraftSlotId.New(),
        OverallPick = 1,
        Round = 1,
        RoundPick = 1,
        TeamId = TeamId.New(),
        PlayerId = PlayerId.New(),
        Source = source,
        ObservedAt = DateTimeOffset.UtcNow
    };
}
