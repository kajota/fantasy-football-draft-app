using FantasyDraftAssistant.Core.Analytics;
using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Query;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.Data.Services;

public sealed class DraftQueryService(
    IDraftStateService drafts,
    IFantasyDataWriter fantasyData,
    IAnalyticsService analytics) : IDraftQueryService
{
    public async Task<LeagueSettingsDto> GetLeagueSettingsAsync(QueryContext context, CancellationToken cancellationToken = default)
    {
        var state = await Require(context, cancellationToken);
        return MapLeague(state);
    }

    public async Task<DraftStatusDto> GetDraftStatusAsync(QueryContext context, CancellationToken cancellationToken = default)
    {
        var state = await Require(context, cancellationToken);
        var snapshot = await analytics.GetSnapshotAsync(context.DraftId, context.BranchId, cancellationToken);
        return MapStatus(state, snapshot);
    }

    public async Task<RosterDto> GetMyRosterAsync(QueryContext context, CancellationToken cancellationToken = default)
    {
        var state = await Require(context, cancellationToken);
        var user = state.League.UserTeamId ?? throw new InvalidOperationException("User team is not set.");
        return await GetTeamRosterAsync(context, user, cancellationToken);
    }

    public async Task<RosterDto> GetTeamRosterAsync(QueryContext context, TeamId teamId, CancellationToken cancellationToken = default)
    {
        var state = await Require(context, cancellationToken);
        var players = (await drafts.GetPlayersAsync(cancellationToken)).ToDictionary(p => p.PlayerId);
        var team = state.Teams.First(t => t.TeamId.Equals(teamId));
        var roster = state.SelectionsForTeam(teamId).Select(s =>
        {
            players.TryGetValue(s.PlayerId, out var player);
            return new RosterPlayerDto
            {
                Name = player?.Name ?? s.PlayerId.ToString(),
                Position = player?.PrimaryPosition.ToString() ?? "?",
                NflTeam = player?.NflTeam ?? "?",
                RoundPick = DraftSlotGenerator.FormatRoundPick(s.Round, s.RoundPick)
            };
        }).ToList();
        return new RosterDto { TeamName = team.Label, Players = roster };
    }

    public async Task<PlayerListDto> GetAvailablePlayersAsync(QueryContext context, PlayerFilter filter, CancellationToken cancellationToken = default)
    {
        var state = await Require(context, cancellationToken);
        var summaries = await Available(state, filter, cancellationToken);
        return new PlayerListDto { Players = summaries };
    }

    public async Task<PlayerDetailsDto> GetPlayerDetailsAsync(QueryContext context, PlayerId playerId, CancellationToken cancellationToken = default)
    {
        var state = await Require(context, cancellationToken);
        var player = (await drafts.GetPlayersAsync(cancellationToken)).First(p => p.PlayerId.Equals(playerId));
        var summary = (await MapPlayers(state, [player], cancellationToken)).Single();
        return new PlayerDetailsDto { Summary = summary, StatusUpdatedAt = player.StatusUpdatedAt };
    }

    public async Task<RecentPicksDto> GetRecentPicksAsync(QueryContext context, int count, CancellationToken cancellationToken = default)
    {
        var board = await GetDraftBoardAsync(context, cancellationToken);
        return new RecentPicksDto { Picks = board.Picks.Reverse().Take(count).ToList() };
    }

    public async Task<PositionSummaryDto> GetPositionSummaryAsync(QueryContext context, CancellationToken cancellationToken = default)
    {
        var snapshot = await analytics.GetSnapshotAsync(context.DraftId, context.BranchId, cancellationToken);
        return new PositionSummaryDto
        {
            Drafted = snapshot.DraftedByPosition.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
            Available = snapshot.AvailableByPosition.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)
        };
    }

    public async Task<RemainingTiersDto> GetRemainingTiersAsync(QueryContext context, CancellationToken cancellationToken = default)
    {
        var snapshot = await analytics.GetSnapshotAsync(context.DraftId, context.BranchId, cancellationToken);
        return new RemainingTiersDto { RemainingByTier = snapshot.RemainingByTier };
    }

    public async Task<UpcomingTeamsDto> GetUpcomingTeamsAsync(QueryContext context, CancellationToken cancellationToken = default)
    {
        var state = await Require(context, cancellationToken);
        var names = state.Slots
            .Where(s => !state.ActiveSelections.ContainsKey(s.OverallPick))
            .OrderBy(s => s.OverallPick)
            .Take(8)
            .Select(s => state.Teams.First(t => t.TeamId.Equals(s.TeamId)).Label)
            .ToList();
        return new UpcomingTeamsDto { Teams = names };
    }

    public async Task<DraftBoardDto> GetDraftBoardAsync(QueryContext context, CancellationToken cancellationToken = default)
    {
        var state = await Require(context, cancellationToken);
        var players = (await drafts.GetPlayersAsync(cancellationToken)).ToDictionary(p => p.PlayerId);
        var picks = state.ActiveSelections.Values.OrderBy(s => s.OverallPick).Select(s =>
        {
            players.TryGetValue(s.PlayerId, out var player);
            var team = state.Teams.First(t => t.TeamId.Equals(s.TeamId));
            return new PickDto
            {
                OverallPick = s.OverallPick,
                RoundPick = DraftSlotGenerator.FormatRoundPick(s.Round, s.RoundPick),
                Team = team.Label,
                TeamId = team.TeamId.ToString(),
                Player = player?.Name ?? s.PlayerId.ToString(),
                Position = player?.PrimaryPosition.ToString() ?? "?",
                NflTeam = player?.NflTeam ?? "—",
                Source = s.Source.ToString()
            };
        }).ToList();
        return new DraftBoardDto { Picks = picks };
    }

    public async Task<MyQueueDto> GetMyQueueAsync(QueryContext context, CancellationToken cancellationToken = default)
    {
        var state = await Require(context, cancellationToken);
        var players = (await drafts.GetPlayersAsync(cancellationToken)).ToDictionary(p => p.PlayerId);
        var queued = state.Queue
            .OrderBy(q => q.SortOrder)
            .Select(q => players.GetValueOrDefault(q.PlayerId))
            .Where(p => p is not null)
            .Select(p => p!)
            .ToList();
        return new MyQueueDto { Players = await MapPlayers(state, queued, cancellationToken) };
    }

    public async Task<DecisionContextDto> GetDecisionContextAsync(QueryContext context, CancellationToken cancellationToken = default)
    {
        var state = await Require(context, cancellationToken);
        var snapshot = await analytics.GetSnapshotAsync(context.DraftId, context.BranchId, cancellationToken);
        var format = FantasyDataFormat.FromLeague(state.ScoringRules, state.RosterSlots);
        var sourceKey = FantasyDataSourcePicker.Pick(await fantasyData.GetSourceKeysAsync(cancellationToken), format);
        var available = await Available(state, new PlayerFilter { MaxResults = 80, SourceKey = sourceKey }, cancellationToken);
        var topAvailable = available.Take(24).ToList();
        var rookies = available.Where(player => player.IsRookie).Take(16).ToList();
        var injured = available
            .Where(player => !string.Equals(player.Status, "Active", StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .ToList();
        var user = state.League.UserTeamId ?? state.Teams[0].TeamId;
        var players = (await drafts.GetPlayersAsync(cancellationToken)).ToDictionary(p => p.PlayerId);
        var queued = state.Queue
            .OrderBy(q => q.SortOrder)
            .Select(q => players.GetValueOrDefault(q.PlayerId))
            .Where(p => p is not null)
            .Select(p => p!)
            .ToList();
        var userNeeds = snapshot.TeamNeeds.FirstOrDefault(team => team.TeamId.Equals(user));
        return new DecisionContextDto
        {
            Status = MapStatus(state, snapshot),
            League = MapLeague(state),
            MyRoster = await GetTeamRosterAsync(context, user, cancellationToken),
            MyRemainingNeeds = FormatNeeds(userNeeds),
            Queue = new MyQueueDto { Players = await MapPlayers(state, queued, cancellationToken, sourceKey) },
            TopAvailable = topAvailable,
            AvailableRookies = rookies,
            InjuredAvailable = injured,
            Positions = new PositionSummaryDto
            {
                Drafted = snapshot.DraftedByPosition.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
                Available = snapshot.AvailableByPosition.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)
            },
            Tiers = new RemainingTiersDto { RemainingByTier = snapshot.RemainingByTier },
            RecentPositions = snapshot.RecentPositions.Select(position => position.ToString()).ToList(),
            InterveningTeamNeeds = snapshot.TeamNeeds
                .Select(t => $"{t.TeamName}: {string.Join(", ", t.RemainingNeeds.Select(n => $"{n.Value} {n.Key}"))}")
                .ToList(),
            Alerts = snapshot.Alerts.Select(a => a.Message).ToList(),
            RankingsSource = FantasyDataSourcePicker.Describe(sourceKey, format),
            StateVersion = state.Draft.CurrentStateVersion
        };
    }

    private async Task<DraftWorkingState> Require(QueryContext context, CancellationToken cancellationToken) =>
        await drafts.GetWorkingStateAsync(context.DraftId, context.BranchId, cancellationToken)
        ?? throw new InvalidOperationException("Draft not found.");

    private async Task<IReadOnlyList<PlayerSummaryDto>> Available(DraftWorkingState state, PlayerFilter filter, CancellationToken cancellationToken)
    {
        var players = (await drafts.GetPlayersAsync(cancellationToken))
            .Where(p => !state.UnavailablePlayers.Contains(p.PlayerId))
            .Where(p => filter.Position is null || p.PrimaryPosition == filter.Position)
            .Where(p => string.IsNullOrWhiteSpace(filter.Search) ||
                        p.Name.Contains(filter.Search, StringComparison.OrdinalIgnoreCase));
        var mapped = await MapPlayers(state, players.ToList(), cancellationToken, filter.SourceKey);
        return PlayerListSorter.Sort(mapped, filter.SortBy, filter.SortDescending, filter.MaxResults ?? 40);
    }

    private async Task<List<PlayerSummaryDto>> MapPlayers(
        DraftWorkingState state,
        IReadOnlyList<Core.Models.Player> players,
        CancellationToken cancellationToken,
        string? sourceKey = null)
    {
        var rankings = await LoadPreferredAsync(fantasyData.GetRankingsAsync, sourceKey, cancellationToken);
        var adp = await LoadPreferredAsync(fantasyData.GetAdpAsync, sourceKey, cancellationToken);
        var projections = await LoadPreferredAsync(fantasyData.GetProjectionsAsync, sourceKey, cancellationToken);
        return players.Select(player =>
        {
            var value = AnalyticsEngine.ValuePlayer(player, rankings, adp, projections, state.ScoringRules, state.League.TeamCount);
            return new PlayerSummaryDto
            {
                PlayerId = player.PlayerId.ToString(),
                Name = player.Name,
                Position = player.PrimaryPosition.ToString(),
                NflTeam = player.NflTeam,
                ByeWeek = player.ByeWeek,
                Status = player.Status.ToString(),
                OverallRank = value.OverallRank,
                PositionRank = value.PositionRank,
                Tier = value.Tier,
                OverallAdp = value.OverallAdp,
                AdpRoundPick = value.AdpRoundPick,
                ProjectedPoints = value.ProjectedPoints,
                YearsExp = player.YearsExp,
                IsRookie = player.IsRookie,
                InjuryBodyPart = player.InjuryBodyPart,
                InjuryNotes = player.InjuryNotes,
                InjuryStartedOn = player.InjuryStartedOn,
                InjuryLine = string.IsNullOrWhiteSpace(player.InjuryLine) ? null : player.InjuryLine
            };
        }).ToList();
    }

    private static async Task<IReadOnlyDictionary<PlayerId, T>> LoadPreferredAsync<T>(
        Func<string?, CancellationToken, Task<IReadOnlyDictionary<PlayerId, T>>> load,
        string? sourceKey,
        CancellationToken cancellationToken)
    {
        if (sourceKey is not null)
        {
            var exact = await load(sourceKey, cancellationToken);
            if (exact.Count > 0)
                return exact;
        }

        foreach (var fallback in new[] { "fantasypros", "sleeper", "seed" })
        {
            if (sourceKey is not null && sourceKey.Equals(fallback, StringComparison.OrdinalIgnoreCase))
                continue;
            var rows = await load(fallback, cancellationToken);
            if (rows.Count > 0)
                return rows;
        }

        return await load(null, cancellationToken);
    }

    private static IReadOnlyList<string> FormatNeeds(TeamNeedSummary? needs) =>
        needs is null
            ? []
            : needs.RemainingNeeds
                .Where(need => need.Value > 0)
                .OrderByDescending(need => need.Value)
                .Select(need => $"{need.Value} {need.Key}")
                .ToList();

    private static LeagueSettingsDto MapLeague(DraftWorkingState state) => new()
    {
        Name = state.League.Name,
        Season = state.League.Season,
        TeamCount = state.League.TeamCount,
        DraftType = state.League.DraftType.ToString(),
        RoundCount = state.League.RoundCount,
        RosterSize = state.League.RosterSize,
        SuperflexOrMultiQb = RosterRules.IsSuperflexOrMultiQb(state.RosterSlots),
        QbDemand = RosterRules.QbDemand(state.RosterSlots),
        RosterSlots = state.RosterSlots.Select(s => $"{s.Count} {s.SlotCode} ({string.Join("/", s.EligiblePositions)})").ToList(),
        Scoring = state.ScoringRules.ToDictionary(r => r.Category.ToString(), r => r.Points),
        ScoringProfile = ScoringCatalog.ProfileName(state.ScoringRules, RosterRules.IsSuperflexOrMultiQb(state.RosterSlots)),
        ScoringLines = state.ScoringRules
            .OrderBy(rule => (int)rule.Category)
            .Select(rule => ScoringCatalog.Line(rule.Category, rule.Points))
            .ToList()
    };

    private static DraftStatusDto MapStatus(DraftWorkingState state, AnalyticsSnapshot snapshot)
    {
        var currentTeam = snapshot.CurrentTeamId is { } id
            ? state.Teams.FirstOrDefault(t => t.TeamId.Equals(id))?.Label
            : null;
        return new DraftStatusDto
        {
            Status = state.Draft.Status.ToString(),
            SourceMode = state.Draft.SourceMode.ToString(),
            StateVersion = state.Draft.CurrentStateVersion,
            ActiveBranch = state.ActiveBranch.Name,
            CurrentOverallPick = snapshot.CurrentOverallPick,
            CurrentRoundPick = snapshot.CurrentRoundPick,
            CurrentTeam = currentTeam,
            UserNextRoundPick = snapshot.UserNextRoundPick,
            PicksUntilUser = snapshot.PicksUntilUser
        };
    }
}
