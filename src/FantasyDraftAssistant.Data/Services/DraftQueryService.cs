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
        return MapRoster(state, players, teamId);
    }

    private static RosterDto MapRoster(
        DraftWorkingState state,
        IReadOnlyDictionary<PlayerId, Core.Models.Player> players,
        TeamId teamId)
    {
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

    // Deep pool so rookies and injured players past the top of the board still
    // appear; the AI only ever sees the trimmed slices below.
    private const int AvailablePoolSize = 200;
    private const int TopAvailableCount = 24;
    private const int RookieCount = 16;
    private const int InjuredCount = 20;
    private const int RecentPickCount = 12;
    private const int UpcomingPickCount = 10;

    // Intervening opponents keep their full roster in context while it is
    // small; deeper rosters are summarized to keep token cost down.
    private const int FullRosterLimit = 7;
    private const int RecentAdditionCount = 3;

    public async Task<DecisionContextDto> GetDecisionContextAsync(QueryContext context, CancellationToken cancellationToken = default)
    {
        var state = await Require(context, cancellationToken);
        var snapshot = await analytics.GetSnapshotAsync(context.DraftId, context.BranchId, cancellationToken);
        var format = FantasyDataFormat.FromLeague(state.ScoringRules, state.RosterSlots);
        var sourceKey = FantasyDataSourcePicker.Pick(await fantasyData.GetSourceKeysAsync(cancellationToken), format);
        var available = await Available(state, new PlayerFilter { MaxResults = AvailablePoolSize, SourceKey = sourceKey }, cancellationToken);
        var user = state.League.UserTeamId ?? state.Teams[0].TeamId;
        var players = (await drafts.GetPlayersAsync(cancellationToken)).ToDictionary(p => p.PlayerId);
        var queued = state.Queue
            .OrderBy(q => q.SortOrder)
            .Select(q => players.GetValueOrDefault(q.PlayerId))
            .Where(p => p is not null)
            .Select(p => p!)
            .ToList();
        var queue = await MapPlayers(state, queued, cancellationToken, sourceKey);
        var userNeeds = snapshot.TeamNeeds.FirstOrDefault(team => team.TeamId.Equals(user));

        var outlook = PickOutlookWindow(state, user);
        Annotate(available, queue, snapshot, outlook.TargetOverallPick, outlook.InterveningPicks);

        var topAvailable = available.Take(TopAvailableCount).ToList();
        var rookies = available.Where(player => player.IsRookie).Take(RookieCount).ToList();
        var injured = available
            .Where(player => !string.Equals(player.Status, "Active", StringComparison.OrdinalIgnoreCase))
            .Take(InjuredCount)
            .ToList();
        var board = await GetDraftBoardAsync(context, cancellationToken);
        var intervening = InterveningTeams(state, snapshot, players, user);
        var refreshes = await fantasyData.GetRefreshInfoAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        return new DecisionContextDto
        {
            Status = MapStatus(state, snapshot),
            League = MapLeague(state),
            MyRoster = MapRoster(state, players, user),
            MyRemainingNeeds = FormatNeeds(userNeeds),
            Queue = new MyQueueDto { Players = queue },
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
            AllTeamNeeds = snapshot.TeamNeeds
                .Select(t => $"{t.TeamName}: {string.Join(", ", t.RemainingNeeds.Select(n => $"{n.Value} {n.Key}"))}")
                .ToList(),
            Alerts = snapshot.Alerts.Select(a => a.Message).ToList(),
            RankingsSource = FantasyDataSourcePicker.Describe(sourceKey, format),
            StateVersion = state.Draft.CurrentStateVersion,
            RecentPicks = board.Picks.TakeLast(RecentPickCount).ToList(),
            UpcomingPicks = UpcomingPicks(state, user, UpcomingPickCount),
            MyUpcomingPicks = MyUpcomingPicks(state, user),
            InterveningTeams = intervening,
            CurrentTeamRoster = snapshot.CurrentTeamId is { } currentTeam
                ? MapRoster(state, players, currentTeam)
                : null,
            DataFreshness = new DataFreshnessDto
            {
                Sources = refreshes.Select(r => new DataFreshnessItemDto
                {
                    ProviderKey = r.ProviderKey,
                    Dataset = r.Dataset,
                    RefreshedAt = $"{r.RefreshedAt.ToUniversalTime():yyyy-MM-dd HH:mm} UTC",
                    Age = FreshnessAge.Describe(r.RefreshedAt, now),
                    RecordCount = r.RecordCount
                }).ToList()
            },
            TierCliffs = TierCliffSummary.Build(available),
            TierCliffDetail = TierCliffSummary.Detail(available),
            PositionThreats = PositionThreats(snapshot, intervening),
            MyByeWeeks = ByeWeeks(state, players, user),
            GeneratedAt = $"{now:yyyy-MM-dd HH:mm} UTC"
        };
    }

    private static void Annotate(
        IReadOnlyList<PlayerSummaryDto> available,
        IReadOnlyList<PlayerSummaryDto> queue,
        AnalyticsSnapshot snapshot,
        int? outlookTargetOverallPick,
        int? interveningPicks)
    {
        var leagueDemand = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var team in snapshot.TeamNeeds)
        {
            foreach (var (position, count) in team.RemainingNeeds)
            {
                if (count > 0)
                    leagueDemand[position.ToString()] = leagueDemand.GetValueOrDefault(position.ToString()) + count;
            }
        }

        var baselines = ValueOverReplacement.Baselines(available, leagueDemand);
        foreach (var player in available.Concat(queue))
        {
            if (interveningPicks == 0)
            {
                player.NextPickOutlook = PickOutlook.LikelyBack;
                player.NextPickGonePercent = 0;
            }
            else
            {
                player.NextPickOutlook = PickOutlook.For(player.OverallAdp, player.RankStd, outlookTargetOverallPick);
                player.NextPickGonePercent = PickOutlook.GoneProbability(player.OverallAdp, player.RankStd, outlookTargetOverallPick)
                    is { } gone ? (int)Math.Round(gone * 100) : null;
            }

            if (player.ProjectedPoints is { } points && baselines.TryGetValue(player.Position, out var baseline))
                player.PointsAboveReplacement = points - baseline;
        }
    }

    private static (int? TargetOverallPick, int? InterveningPicks) PickOutlookWindow(DraftWorkingState state, TeamId user)
    {
        var current = state.CurrentSlot;
        if (current is null)
            return (null, null);

        var upcomingUserPicks = state.Slots
            .Where(s => !state.ActiveSelections.ContainsKey(s.OverallPick))
            .Where(s => s.TeamId.Equals(user))
            .OrderBy(s => s.OverallPick)
            .ToList();
        var target = current.TeamId.Equals(user)
            ? upcomingUserPicks.FirstOrDefault(s => s.OverallPick > current.OverallPick)
            : upcomingUserPicks.FirstOrDefault();
        if (target is null)
            return (null, null);

        var start = current.TeamId.Equals(user)
            ? current.OverallPick + 1
            : current.OverallPick;
        var intervening = state.Slots.Count(s =>
            !state.ActiveSelections.ContainsKey(s.OverallPick)
            && s.OverallPick >= start
            && s.OverallPick < target.OverallPick
            && !s.TeamId.Equals(user));
        return (target.OverallPick, intervening);
    }

    private static List<UpcomingPickDto> UpcomingPicks(DraftWorkingState state, TeamId user, int take) =>
        state.Slots
            .Where(s => !state.ActiveSelections.ContainsKey(s.OverallPick))
            .OrderBy(s => s.OverallPick)
            .Take(take)
            .Select(s => MapUpcoming(state, s, user))
            .ToList();

    private static List<UpcomingPickDto> MyUpcomingPicks(DraftWorkingState state, TeamId user) =>
        state.Slots
            .Where(s => !state.ActiveSelections.ContainsKey(s.OverallPick))
            .Where(s => s.TeamId.Equals(user))
            .OrderBy(s => s.OverallPick)
            .Select(s => MapUpcoming(state, s, user))
            .ToList();

    private static UpcomingPickDto MapUpcoming(DraftWorkingState state, Core.Models.DraftSlot slot, TeamId user) => new()
    {
        OverallPick = slot.OverallPick,
        RoundPick = DraftSlotGenerator.FormatRoundPick(slot.Round, slot.RoundPick),
        Team = state.Teams.First(t => t.TeamId.Equals(slot.TeamId)).Label,
        TeamId = slot.TeamId.ToString(),
        IsUser = slot.TeamId.Equals(user)
    };

    private static List<InterveningTeamDto> InterveningTeams(
        DraftWorkingState state,
        AnalyticsSnapshot snapshot,
        IReadOnlyDictionary<PlayerId, Core.Models.Player> players,
        TeamId user)
    {
        if (snapshot.UserNextOverallPick is not { } userNext)
            return [];
        var current = state.CurrentSlot;
        if (current is null || current.TeamId.Equals(user))
            return [];

        var needsByTeam = snapshot.TeamNeeds.ToDictionary(t => t.TeamId, t => (TeamNeedSummary?)t);
        return state.Slots
            .Where(s => !state.ActiveSelections.ContainsKey(s.OverallPick))
            .Where(s => s.OverallPick < userNext && !s.TeamId.Equals(user))
            .OrderBy(s => s.OverallPick)
            .GroupBy(s => s.TeamId)
            .Select(group =>
            {
                var roster = MapRoster(state, players, group.Key);
                var full = roster.Players.Count <= FullRosterLimit;
                return new InterveningTeamDto
                {
                    TeamName = roster.TeamName,
                    TeamId = group.Key.ToString(),
                    PicksBeforeUser = group.Count(),
                    Roster = full ? roster.Players : null,
                    RosterPositionCounts = full
                        ? null
                        : roster.Players
                            .GroupBy(p => p.Position)
                            .ToDictionary(g => g.Key, g => g.Count()),
                    RecentAdditions = full
                        ? []
                        : roster.Players
                            .TakeLast(RecentAdditionCount)
                            .Select(p => $"{p.Name} ({p.Position}, {p.RoundPick})")
                            .ToList(),
                    RemainingNeeds = FormatNeeds(needsByTeam.GetValueOrDefault(group.Key))
                };
            })
            .ToList();
    }

    private static IReadOnlyDictionary<string, int> PositionThreats(
        AnalyticsSnapshot snapshot,
        IReadOnlyList<InterveningTeamDto> intervening)
    {
        var interveningIds = intervening.Select(t => t.TeamId).ToHashSet(StringComparer.Ordinal);
        return snapshot.TeamNeeds
            .Where(t => interveningIds.Contains(t.TeamId.ToString()))
            .SelectMany(t => t.RemainingNeeds.Where(kv => kv.Value > 0).Select(kv => kv.Key))
            .GroupBy(p => p.ToString())
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private static IReadOnlyList<string> ByeWeeks(
        DraftWorkingState state,
        IReadOnlyDictionary<PlayerId, Core.Models.Player> players,
        TeamId user) =>
        state.SelectionsForTeam(user)
            .Select(s => players.GetValueOrDefault(s.PlayerId))
            .Where(p => p?.ByeWeek is not null)
            .GroupBy(p => p!.ByeWeek!.Value)
            .OrderBy(g => g.Key)
            .Select(g => $"Week {g.Key}: {string.Join(", ", g.Select(p => p!.Name))}")
            .ToList();

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
        var catalog = (await drafts.GetPlayersAsync(cancellationToken)).ToDictionary(player => player.PlayerId);
        var roster = HandcuffRoster(state, catalog, rankings, adp);
        var byeRoster = UserByeRoster(state, catalog);
        return players.Select(player =>
        {
            var value = AnalyticsEngine.ValuePlayer(player, rankings, adp, projections, state.ScoringRules, state.League.TeamCount);
            var cuff = HandcuffMatcher.For(
                new HandcuffPlayer(player.Name, player.NflTeam, player.PrimaryPosition, value.OverallRank, value.OverallAdp),
                roster);
            var sharedBye = SharedByeMatcher.For(
                new RosterByePlayer(player.Name, player.PrimaryPosition, player.ByeWeek),
                byeRoster);
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
                RankMin = value.RankMin,
                RankMax = value.RankMax,
                RankStd = value.RankStd,
                RankRange = value.RankRange,
                OverallAdp = value.OverallAdp,
                AdpRoundPick = value.AdpRoundPick,
                ProjectedPoints = value.ProjectedPoints,
                YearsExp = player.YearsExp,
                IsRookie = player.IsRookie,
                InjuryBodyPart = player.InjuryBodyPart,
                InjuryNotes = player.InjuryNotes,
                InjuryStartedOn = player.InjuryStartedOn,
                InjuryLine = string.IsNullOrWhiteSpace(player.InjuryLine) ? null : player.InjuryLine,
                HandcuffFor = cuff?.StarterName,
                SharedByeWith = sharedBye is null ? null : SharedByeMatcher.Teammates(sharedBye),
                SharedByeWeek = sharedBye?.ByeWeek
            };
        }).ToList();
    }

    internal static IReadOnlyList<HandcuffPlayer> HandcuffRoster(
        DraftWorkingState state,
        IReadOnlyDictionary<PlayerId, Core.Models.Player> catalog,
        IReadOnlyDictionary<PlayerId, Core.Models.PlayerRanking> rankings,
        IReadOnlyDictionary<PlayerId, Core.Models.PlayerAdp> adp)
    {
        if (state.League.UserTeamId is not { } user)
            return [];

        return state.SelectionsForTeam(user)
            .Select(selection =>
            {
                if (!catalog.TryGetValue(selection.PlayerId, out var player))
                    return null;
                rankings.TryGetValue(player.PlayerId, out var ranking);
                adp.TryGetValue(player.PlayerId, out var playerAdp);
                return new HandcuffPlayer(
                    player.Name,
                    player.NflTeam,
                    player.PrimaryPosition,
                    ranking?.OverallRank,
                    playerAdp?.OverallAdp);
            })
            .Where(player => player is not null)
            .Select(player => player!)
            .ToList();
    }

    internal static IReadOnlyList<RosterByePlayer> UserByeRoster(
        DraftWorkingState state,
        IReadOnlyDictionary<PlayerId, Core.Models.Player> catalog)
    {
        if (state.League.UserTeamId is not { } user)
            return [];

        return state.SelectionsForTeam(user)
            .Select(selection =>
                catalog.TryGetValue(selection.PlayerId, out var player)
                    ? new RosterByePlayer(player.Name, player.PrimaryPosition, player.ByeWeek)
                    : null)
            .Where(player => player is not null)
            .Select(player => player!)
            .ToList();
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
            .ToList(),
        DraftGuidelines = string.IsNullOrWhiteSpace(state.League.DraftGuidelines)
            ? null
            : state.League.DraftGuidelines.Trim(),
        KeeperNote = KeeperNote(state)
    };

    private static string? KeeperNote(DraftWorkingState state)
    {
        var keeperSlots = state.Slots.Count(s => s.IsKeeperSlot);
        var configured = state.Keepers.Count;
        if (keeperSlots == 0 && configured == 0)
            return null;
        return $"Keeper league: {Math.Max(keeperSlots, configured)} keeper selections on this board, " +
               $"max {KeeperRules.MaxKeepersPerTeam} per team.";
    }

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
            UserNextOverallPick = snapshot.UserNextOverallPick,
            PicksUntilUser = snapshot.PicksUntilUser
        };
    }
}
