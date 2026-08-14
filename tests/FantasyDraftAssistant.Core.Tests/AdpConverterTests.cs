using FantasyDraftAssistant.Core.Analytics;

namespace FantasyDraftAssistant.Core.Tests;

public class AdpConverterTests
{
    [Theory]
    [InlineData(1, 12, "1.01")]
    [InlineData(12, 12, "1.12")]
    [InlineData(13, 12, "2.01")]
    [InlineData(25, 12, "3.01")]
    [InlineData(36, 12, "3.12")]
    [InlineData(37, 12, "4.01")]
    [InlineData(25, 10, "3.05")]
    [InlineData(25, 14, "2.11")]
    [InlineData(25.4, 12, "3.01")]
    public void Converts_overall_adp_to_round_pick(double overall, int teams, string expected)
    {
        Assert.Equal(expected, AdpConverter.FormatRoundPick(overall, teams));
    }
}
