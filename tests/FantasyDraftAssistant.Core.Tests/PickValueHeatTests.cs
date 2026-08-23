using FantasyDraftAssistant.Core.Analytics;

namespace FantasyDraftAssistant.Core.Tests;

public class PickValueHeatTests
{
    [Fact]
    public void Later_than_adp_is_a_steal()
    {
        Assert.Equal(PickHeat.Steal, PickValueHeat.From(24, 12, teamCount: 12));
        Assert.Equal(PickHeat.MildSteal, PickValueHeat.From(16, 12, teamCount: 12));
    }

    [Fact]
    public void Earlier_than_adp_is_a_reach()
    {
        Assert.Equal(PickHeat.Reach, PickValueHeat.From(12, 24, teamCount: 12));
        Assert.Equal(PickHeat.MildReach, PickValueHeat.From(12, 16, teamCount: 12));
    }

    [Fact]
    public void On_time_is_fair() =>
        Assert.Equal(PickHeat.Fair, PickValueHeat.From(12, 12, teamCount: 12));

    [Fact]
    public void Missing_adp_is_unknown() =>
        Assert.Equal(PickHeat.Unknown, PickValueHeat.From(12, null, teamCount: 12));

    [Fact]
    public void Keeper_is_not_scored_as_a_reach() =>
        Assert.Equal(PickHeat.Keeper, PickValueHeat.From(72, 8, teamCount: 12, isKeeper: true));
}
