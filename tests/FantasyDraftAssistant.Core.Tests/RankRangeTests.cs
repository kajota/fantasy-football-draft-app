using FantasyDraftAssistant.Core.Analytics;

namespace FantasyDraftAssistant.Core.Tests;

public class RankRangeTests
{
    [Fact]
    public void Formats_min_and_max()
    {
        Assert.Equal("8-41", AnalyticsEngine.FormatRankRange(8, 41));
        Assert.Equal("1-3", AnalyticsEngine.FormatRankRange(1, 3));
        Assert.Null(AnalyticsEngine.FormatRankRange(null, null));
    }
}
