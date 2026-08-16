using FantasyDraftAssistant.Core.Engine;

namespace FantasyDraftAssistant.Core.Tests;

public class PracticeClockTests
{
    [Fact]
    public void User_on_clock_requires_a_remaining_user_pick()
    {
        Assert.True(PracticeClock.UserIsOnClock("16.01", 0));
        Assert.False(PracticeClock.UserIsOnClock(null, 0));
        Assert.False(PracticeClock.UserIsOnClock("16.12", 3));
    }

    [Fact]
    public void Cpu_can_play_after_the_users_last_pick()
    {
        Assert.True(PracticeClock.CpuCanPlay(practice: true, playing: false, userOnClock: false, draftOpen: true));
        Assert.False(PracticeClock.CpuCanPlay(practice: true, playing: false, userOnClock: true, draftOpen: true));
        Assert.True(PracticeClock.ShouldFinishRemaining(practice: true, userNextRoundPick: null, draftOpen: true));
        Assert.False(PracticeClock.ShouldFinishRemaining(practice: true, userNextRoundPick: "16.01", draftOpen: true));
    }
}
