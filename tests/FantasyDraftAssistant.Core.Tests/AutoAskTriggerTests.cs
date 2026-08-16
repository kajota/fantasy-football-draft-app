using FantasyDraftAssistant.Core.Ai;
using FantasyDraftAssistant.Core.Ids;

namespace FantasyDraftAssistant.Core.Tests;

public class AutoAskTriggerTests
{
    [Fact]
    public void Asks_once_per_branch_and_pick()
    {
        var branch = BranchId.New();
        Assert.True(AutoAskTrigger.ShouldAsk(true, true, branch, 24, null, -1));
        Assert.False(AutoAskTrigger.ShouldAsk(true, true, branch, 24, branch, 24));
        Assert.True(AutoAskTrigger.ShouldAsk(true, true, branch, 1, branch, 24));
    }

    [Fact]
    public void New_practice_branch_asks_again_at_the_same_overall_pick()
    {
        var first = BranchId.New();
        var second = BranchId.New();
        Assert.True(AutoAskTrigger.ShouldAsk(true, true, second, 24, first, 24));
        Assert.False(AutoAskTrigger.ShouldAsk(true, false, second, 24, second, -1));
    }

    [Fact]
    public void Disabled_never_asks_even_on_the_clock()
    {
        var branch = BranchId.New();
        Assert.False(AutoAskTrigger.ShouldAsk(false, true, branch, 1, null, -1));
    }
}
