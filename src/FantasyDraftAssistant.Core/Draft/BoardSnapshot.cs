namespace FantasyDraftAssistant.Core.Engine;

public sealed class BoardSnapshot
{
    public required DateTimeOffset UpdatedAt { get; init; }
    public required string League { get; init; }
    public required bool Practice { get; init; }
    public int? Round { get; init; }
    public string? Pick { get; init; }
    public int? Overall { get; init; }
    public BoardClockTeam? OnTheClock { get; init; }
    public BoardLastPick? LastPick { get; init; }
    public required IReadOnlyList<BoardSnapshotTeam> Teams { get; init; }
    public required IReadOnlyList<BoardSnapshotRound> Rounds { get; init; }
}

public sealed class BoardClockTeam
{
    public required string Team { get; init; }
    public required bool IsMine { get; init; }
}

public sealed class BoardLastPick
{
    public required string Player { get; init; }
    public required string Position { get; init; }
    public required string Team { get; init; }
    public required string RoundPick { get; init; }
}

public sealed class BoardSnapshotTeam
{
    public required string Label { get; init; }
    public required bool IsMine { get; init; }
}

public sealed class BoardSnapshotRound
{
    public required int Round { get; init; }
    public required IReadOnlyList<BoardSnapshotCell> Cells { get; init; }
}

public sealed class BoardSnapshotCell
{
    public required string Player { get; init; }
    public required string Position { get; init; }
    public required string RoundPick { get; init; }
    public required bool IsCurrent { get; init; }
    public required bool IsEmpty { get; init; }
    public required bool IsMine { get; init; }
}
