namespace FantasyDraftAssistant.Core.Engine;

public static class TeamSeatOrder
{
    public static int TargetIndex(int requestedSeat, int count)
    {
        if (count < 1)
            return 0;
        return Math.Clamp(requestedSeat, 1, count) - 1;
    }

    public static bool Move<T>(IList<T> items, int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || toIndex < 0 || fromIndex >= items.Count || toIndex >= items.Count)
            return false;
        if (fromIndex == toIndex)
            return false;

        var item = items[fromIndex];
        items.RemoveAt(fromIndex);
        items.Insert(toIndex, item);
        return true;
    }
}
