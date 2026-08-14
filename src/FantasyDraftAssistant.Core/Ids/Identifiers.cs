namespace FantasyDraftAssistant.Core.Ids;

public readonly record struct LeagueId(Guid Value)
{
    public static LeagueId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
    public static LeagueId Parse(string value) => new(Guid.Parse(value));
}

public readonly record struct TeamId(Guid Value)
{
    public static TeamId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
    public static TeamId Parse(string value) => new(Guid.Parse(value));
}

public readonly record struct RosterSlotId(Guid Value)
{
    public static RosterSlotId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
    public static RosterSlotId Parse(string value) => new(Guid.Parse(value));
}

public readonly record struct ScoringRuleId(Guid Value)
{
    public static ScoringRuleId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
    public static ScoringRuleId Parse(string value) => new(Guid.Parse(value));
}

public readonly record struct PlayerId(Guid Value)
{
    public static PlayerId New() => new(Guid.NewGuid());
    public static PlayerId FromName(string name, string nflTeam, string primaryPosition)
    {
        var input = $"{name.Trim().ToUpperInvariant()}|{nflTeam.Trim().ToUpperInvariant()}|{primaryPosition.Trim().ToUpperInvariant()}";
        return new PlayerId(DeterministicGuid(input));
    }

    public override string ToString() => Value.ToString("D");
    public static PlayerId Parse(string value) => new(Guid.Parse(value));

    private static Guid DeterministicGuid(string input)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }
}

public readonly record struct DraftId(Guid Value)
{
    public static DraftId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
    public static DraftId Parse(string value) => new(Guid.Parse(value));
}

public readonly record struct BranchId(Guid Value)
{
    public static BranchId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
    public static BranchId Parse(string value) => new(Guid.Parse(value));
}

public readonly record struct DraftSlotId(Guid Value)
{
    public static DraftSlotId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
    public static DraftSlotId Parse(string value) => new(Guid.Parse(value));
}

public readonly record struct EventId(Guid Value)
{
    public static EventId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
    public static EventId Parse(string value) => new(Guid.Parse(value));
}

public readonly record struct KeeperId(Guid Value)
{
    public static KeeperId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
    public static KeeperId Parse(string value) => new(Guid.Parse(value));
}

public readonly record struct QueueItemId(Guid Value)
{
    public static QueueItemId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
    public static QueueItemId Parse(string value) => new(Guid.Parse(value));
}
