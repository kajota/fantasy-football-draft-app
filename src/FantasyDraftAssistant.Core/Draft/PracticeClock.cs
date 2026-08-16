namespace FantasyDraftAssistant.Core.Engine;

public static class PracticeClock
{
    public static bool UserIsOnClock(string? userNextRoundPick, int picksUntilUser) =>
        userNextRoundPick is not null && picksUntilUser == 0;

    public static bool CpuCanPlay(bool practice, bool playing, bool userOnClock, bool draftOpen) =>
        practice && !playing && !userOnClock && draftOpen;

    public static bool ShouldFinishRemaining(bool practice, string? userNextRoundPick, bool draftOpen) =>
        practice && userNextRoundPick is null && draftOpen;
}
