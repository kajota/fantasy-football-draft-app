using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;

namespace FantasyDraftAssistant.Core.Yahoo;

public sealed class YahooAuthStatus
{
    public bool HasAppCredentials { get; init; }
    public bool IsSignedIn { get; init; }
    public DateTimeOffset? AccessTokenExpiresAt { get; init; }
    public string RedirectUri { get; init; } = YahooAuthDefaults.RedirectUri;
    public string? ClientIdHint { get; init; }
}

public static class YahooAuthDefaults
{
    public const string RedirectUri = "http://127.0.0.1:8765/yahoo/callback";
    public const string AttributionText = "Fantasy data provided by Yahoo Fantasy";
    public const string AttributionUrl = "https://football.fantasysports.yahoo.com/";
    public const string DeveloperAccessUrl = "https://sports.yahoo.com/developer/access/";
}

public sealed class YahooSignInStart
{
    public required string AuthorizationUrl { get; init; }
    public required string RedirectUri { get; init; }
    public required string State { get; init; }
    public bool LocalCallbackListening { get; init; }
}

public sealed class YahooLeagueListItem
{
    public required string LeagueKey { get; init; }
    public required string Name { get; init; }
    public int Season { get; init; }
    public int TeamCount { get; init; }
    public bool IsAuction { get; init; }
    public bool IsKeeper { get; init; }
    public bool AlreadyImported { get; init; }
}

public sealed record YahooRosterPosition
{
    public required string Position { get; init; }
    public required int Count { get; init; }
}

public sealed record YahooStatModifier
{
    public required int StatId { get; init; }
    public required decimal Value { get; init; }
    public string? DisplayName { get; init; }
}

public sealed record YahooTeamSnapshot
{
    public required string TeamKey { get; init; }
    public required string Name { get; init; }
    public string? OwnerName { get; init; }
    public int TeamNumber { get; init; }
    public bool IsCurrentUser { get; init; }
}

public sealed record YahooKeeperHint
{
    public required string TeamKey { get; init; }
    public required string PlayerName { get; init; }
    public string? YahooPlayerId { get; init; }
    public int? RoundCost { get; init; }
}

public sealed record YahooLeagueSnapshot
{
    public required string LeagueKey { get; init; }
    public required string Name { get; init; }
    public int Season { get; init; }
    public int TeamCount { get; init; }
    public bool IsAuction { get; init; }
    public bool IsKeeper { get; init; }
    public string? DraftTypeRaw { get; init; }
    public int? DraftRounds { get; init; }
    public IReadOnlyList<YahooRosterPosition> Roster { get; init; } = [];
    public IReadOnlyList<YahooStatModifier> Stats { get; init; } = [];
    public IReadOnlyList<YahooTeamSnapshot> Teams { get; init; } = [];
    public IReadOnlyList<YahooKeeperHint> Keepers { get; init; } = [];
}

public sealed class YahooMappedLeague
{
    public required ImportedLeagueRequest Request { get; init; }
    public IReadOnlyList<string> ReviewItems { get; init; } = [];
    public IReadOnlyList<string> UnmappedRosterSlots { get; init; } = [];
    public IReadOnlyList<string> UnmappedScoring { get; init; } = [];
}

public sealed class YahooImportPreview
{
    public required YahooMappedLeague Mapped { get; init; }
    public required YahooLeagueSnapshot Snapshot { get; init; }
    public LeagueId? ExistingLeagueId { get; init; }
    public bool ExistingIsArchived { get; init; }
}

public sealed class YahooImportOptions
{
    public bool ReplaceDraftOrder { get; init; }
}

public sealed class YahooImportResult
{
    public bool Succeeded { get; init; }
    public string? Error { get; init; }
    public LeagueId? LeagueId { get; init; }
    public string? LeagueName { get; init; }
    public bool CreatedNew { get; init; }
    public IReadOnlyList<string> ReviewItems { get; init; } = [];
}
