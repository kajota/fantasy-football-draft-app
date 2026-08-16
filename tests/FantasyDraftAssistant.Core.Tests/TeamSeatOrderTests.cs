using FantasyDraftAssistant.Core.Engine;

namespace FantasyDraftAssistant.Core.Tests;

public class TeamSeatOrderTests
{
    [Theory]
    [InlineData(1, 10, 0)]
    [InlineData(6, 10, 5)]
    [InlineData(10, 10, 9)]
    [InlineData(0, 10, 0)]
    [InlineData(99, 10, 9)]
    public void Target_index_clamps_to_the_table(int requested, int count, int expected) =>
        Assert.Equal(expected, TeamSeatOrder.TargetIndex(requested, count));

    [Fact]
    public void Move_shifts_first_seat_to_sixth()
    {
        var seats = new List<string> { "Mine", "2", "3", "4", "5", "6", "7", "8", "9", "10" };
        Assert.True(TeamSeatOrder.Move(seats, 0, TeamSeatOrder.TargetIndex(6, seats.Count)));
        Assert.Equal(["2", "3", "4", "5", "6", "Mine", "7", "8", "9", "10"], seats);
    }

    [Fact]
    public void Same_seat_is_a_no_op()
    {
        var seats = new List<string> { "A", "B" };
        Assert.False(TeamSeatOrder.Move(seats, 0, 0));
        Assert.Equal(["A", "B"], seats);
    }
}
