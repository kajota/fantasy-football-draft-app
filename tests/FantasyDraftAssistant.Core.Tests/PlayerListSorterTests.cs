using FantasyDraftAssistant.Core.Query;

namespace FantasyDraftAssistant.Core.Tests;

public class PlayerListSorterTests
{
    [Fact]
    public void Default_rank_sort_puts_best_rank_first_and_missing_last()
    {
        var sorted = PlayerListSorter.Sort(Sample(), PlayerListSort.Rank, descending: false);
        Assert.Equal(["B", "A", "C"], sorted.Select(p => p.Name));
    }

    [Fact]
    public void Projected_points_descending_puts_highest_first()
    {
        var sorted = PlayerListSorter.Sort(Sample(), PlayerListSort.ProjectedPoints, descending: true);
        Assert.Equal(["C", "A", "B"], sorted.Select(p => p.Name));
    }

    [Fact]
    public void Adp_ascending_uses_numeric_adp_not_display_text()
    {
        var sorted = PlayerListSorter.Sort(Sample(), PlayerListSort.Adp, descending: false);
        Assert.Equal(["A", "B", "C"], sorted.Select(p => p.Name));
    }

    [Fact]
    public void Take_applies_after_sort()
    {
        var sorted = PlayerListSorter.Sort(Sample(), PlayerListSort.ProjectedPoints, descending: true, maxResults: 1);
        Assert.Equal("C", Assert.Single(sorted).Name);
    }

    private static List<PlayerSummaryDto> Sample() =>
    [
        new()
        {
            PlayerId = "a",
            Name = "A",
            Position = "WR",
            NflTeam = "DAL",
            Status = "Active",
            OverallRank = 12,
            OverallAdp = 8.1,
            AdpRoundPick = "1.08",
            ProjectedPoints = 200
        },
        new()
        {
            PlayerId = "b",
            Name = "B",
            Position = "RB",
            NflTeam = "ATL",
            Status = "Active",
            OverallRank = 3,
            OverallAdp = 14.4,
            AdpRoundPick = "2.02",
            ProjectedPoints = 180
        },
        new()
        {
            PlayerId = "c",
            Name = "C",
            Position = "QB",
            NflTeam = "BUF",
            Status = "Active",
            OverallRank = null,
            OverallAdp = null,
            ProjectedPoints = 320
        }
    ];
}
