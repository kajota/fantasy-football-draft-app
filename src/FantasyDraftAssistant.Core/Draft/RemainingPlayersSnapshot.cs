namespace FantasyDraftAssistant.Core.Engine;

public sealed class RemainingPlayersSnapshot
{
    public required DateTimeOffset UpdatedAt { get; init; }
    public required string League { get; init; }
    public required bool Practice { get; init; }
    public required IReadOnlyList<RemainingPlayerRow> Players { get; init; }
}

public sealed class RemainingPlayerRow
{
    public int? Rank { get; init; }
    public required string Name { get; init; }
    public required string Position { get; init; }
    public required string NflTeam { get; init; }
}
