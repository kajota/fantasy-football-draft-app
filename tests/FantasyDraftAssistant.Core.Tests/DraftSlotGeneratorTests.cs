using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;

namespace FantasyDraftAssistant.Core.Tests;

public class DraftSlotGeneratorTests
{
    [Fact]
    public void Snake_alternates_order_each_round()
    {
        var teams = Teams(12);
        var slots = DraftSlotGenerator.Generate(DraftType.Snake, teams, 3);

        Assert.Equal(36, slots.Count);
        Assert.Equal(teams[0].TeamId, slots[0].TeamId);
        Assert.Equal(teams[11].TeamId, slots[11].TeamId);
        Assert.Equal(teams[11].TeamId, slots[12].TeamId);
        Assert.Equal(teams[0].TeamId, slots[23].TeamId);
        Assert.Equal(teams[0].TeamId, slots[24].TeamId);
        Assert.Equal(1, slots[0].Round);
        Assert.Equal(1, slots[0].RoundPick);
        Assert.Equal(2, slots[12].Round);
        Assert.Equal(1, slots[12].RoundPick);
    }

    [Fact]
    public void Linear_keeps_same_order_every_round()
    {
        var teams = Teams(10);
        var slots = DraftSlotGenerator.Generate(DraftType.Linear, teams, 2);

        Assert.Equal(20, slots.Count);
        Assert.Equal(teams[0].TeamId, slots[0].TeamId);
        Assert.Equal(teams[9].TeamId, slots[9].TeamId);
        Assert.Equal(teams[0].TeamId, slots[10].TeamId);
        Assert.Equal(teams[9].TeamId, slots[19].TeamId);
    }

    [Fact]
    public void FromOverall_matches_requirements_examples()
    {
        Assert.Equal((1, 1), DraftSlotGenerator.FromOverall(1, 12));
        Assert.Equal((1, 12), DraftSlotGenerator.FromOverall(12, 12));
        Assert.Equal((2, 1), DraftSlotGenerator.FromOverall(13, 12));
        Assert.Equal((3, 1), DraftSlotGenerator.FromOverall(25, 12));
    }

    [Fact]
    public void Rejects_non_contiguous_positions()
    {
        var teams = Teams(4);
        teams[2] = teams[2] with { DraftPosition = 5 };
        Assert.Throws<ArgumentException>(() => DraftSlotGenerator.Generate(DraftType.Snake, teams, 1));
    }

    private static List<TeamDraftPosition> Teams(int count)
    {
        var list = new List<TeamDraftPosition>();
        for (var i = 1; i <= count; i++)
        {
            list.Add(new TeamDraftPosition
            {
                TeamId = TeamId.New(),
                Name = $"T{i}",
                DraftPosition = i
            });
        }

        return list;
    }
}
