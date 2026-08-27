namespace FantasyDraftAssistant.Data.Database;

public sealed record DataLockHolder(
    string SessionId,
    string Host,
    string User,
    int Pid,
    DateTimeOffset AcquiredAt,
    DateTimeOffset HeartbeatAt);

public enum DataLockKind
{
    Free,
    Ours,
    SameHostDead,
    Live,
    Stale,
    Corrupt
}

public sealed record DataLockInspection(DataLockKind Kind, DataLockHolder? Holder, TimeSpan Age)
{
    public bool RequiresWarning => Kind is DataLockKind.Live or DataLockKind.Stale or DataLockKind.Corrupt;
}

/// <summary>
/// Advisory lock for a Drive-synced data folder. SQLite locking does not work across
/// two computers that each have their own copy of <c>draft.db</c>.
/// </summary>
public static class DataLockEvaluator
{
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);

    public static DataLockInspection Inspect(
        DataLockHolder? existing,
        bool corrupt,
        string selfHost,
        int selfPid,
        string? selfSessionId,
        DateTimeOffset now,
        Func<int, bool> isProcessAlive)
    {
        if (corrupt)
            return new DataLockInspection(DataLockKind.Corrupt, existing, Age(existing, now));

        if (existing is null)
            return new DataLockInspection(DataLockKind.Free, null, TimeSpan.Zero);

        if (selfSessionId is not null
            && string.Equals(existing.SessionId, selfSessionId, StringComparison.Ordinal))
        {
            return new DataLockInspection(DataLockKind.Ours, existing, Age(existing, now));
        }

        var age = Age(existing, now);
        var sameHost = string.Equals(existing.Host, selfHost, StringComparison.OrdinalIgnoreCase);
        if (sameHost)
        {
            if (existing.Pid == selfPid)
                return new DataLockInspection(DataLockKind.SameHostDead, existing, age);

            var alive = false;
            try
            {
                alive = isProcessAlive(existing.Pid);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                alive = false;
            }

            if (!alive)
                return new DataLockInspection(DataLockKind.SameHostDead, existing, age);
        }

        if (age >= StaleAfter)
            return new DataLockInspection(DataLockKind.Stale, existing, age);

        return new DataLockInspection(DataLockKind.Live, existing, age);
    }

    public static DataLockDialogContent Dialog(DataLockInspection inspection, string dataRoot)
    {
        var holder = inspection.Holder;
        var who = holder is null
            ? "another session"
            : string.IsNullOrWhiteSpace(holder.User)
                ? holder.Host
                : $"{holder.Host} ({holder.User})";
        var lastSeen = holder is null
            ? "unknown"
            : holder.HeartbeatAt.ToLocalTime().ToString("g");
        var age = FormatAge(inspection.Age);

        return inspection.Kind switch
        {
            DataLockKind.Live => new DataLockDialogContent(
                "Database already open",
                $"This data folder looks like it is already open on {who}. Last heartbeat was {age} ({lastSeen}). Opening it here at the same time can corrupt leagues, picks, and history.",
                $"Folder: {dataRoot}\nClose the other copy, wait until Drive is idle, then open this app again."),
            DataLockKind.Stale => new DataLockDialogContent(
                "Previous session may still be open",
                $"A session on {who} did not close cleanly. Last heartbeat was {age} ({lastSeen}). It may have crashed, or Drive may not have synced the close yet.",
                $"Folder: {dataRoot}\nIf the other computer is still running this app, quit that copy first."),
            _ => new DataLockDialogContent(
                "Lock file could not be read",
                "A draft.lock file is in the data folder but could not be read. Another computer may be writing it, or Drive may have left a conflict copy.",
                $"Folder: {dataRoot}\nIf you know nothing else has the database open, you can continue.")
        };
    }

    public static string FormatAge(TimeSpan age)
    {
        if (age < TimeSpan.Zero)
            age = TimeSpan.Zero;
        if (age.TotalSeconds < 90)
            return $"{Math.Max(0, (int)age.TotalSeconds)} seconds ago";
        if (age.TotalMinutes < 90)
            return $"{(int)age.TotalMinutes} minutes ago";
        return $"{(int)age.TotalHours} hours ago";
    }

    private static TimeSpan Age(DataLockHolder? holder, DateTimeOffset now) =>
        holder is null ? TimeSpan.Zero : now - holder.HeartbeatAt;
}

public sealed record DataLockDialogContent(string Title, string Body, string Detail);
