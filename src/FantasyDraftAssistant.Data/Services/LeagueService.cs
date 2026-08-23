using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Models;
using FantasyDraftAssistant.Core.Query;
using FantasyDraftAssistant.Data.Database;
using Microsoft.Data.Sqlite;

namespace FantasyDraftAssistant.Data.Services;

public sealed class LeagueService(SqliteConnectionFactory factory, IBackupService backups, IDraftChangeNotifier notifier) : ILeagueService
{
    public Task<IReadOnlyList<LeagueSummary>> ListLeaguesAsync(CancellationToken cancellationToken = default) =>
        ListLeaguesCoreAsync(archived: false);

    public Task<IReadOnlyList<LeagueSummary>> ListArchivedLeaguesAsync(CancellationToken cancellationToken = default) =>
        ListLeaguesCoreAsync(archived: true);

    public Task ArchiveLeagueAsync(LeagueId leagueId, CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        using var cmd = db.Cmd("""
            UPDATE Leagues
            SET ArchivedAt = $at
            WHERE LeagueId = $id AND ArchivedAt IS NULL;
            """)
            .Bind("$at", DateTimeOffset.UtcNow.ToString("O"))
            .Bind("$id", leagueId.ToString());
        cmd.ExecuteNonQuery();
        return Task.CompletedTask;
    }

    public Task RestoreLeagueAsync(LeagueId leagueId, CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        using var cmd = db.Cmd("""
            UPDATE Leagues
            SET ArchivedAt = NULL
            WHERE LeagueId = $id;
            """)
            .Bind("$id", leagueId.ToString());
        cmd.ExecuteNonQuery();
        return Task.CompletedTask;
    }

    public async Task DeleteLeaguePermanentlyAsync(LeagueId leagueId, CancellationToken cancellationToken = default)
    {
        using (var db = factory.Open())
        {
            if (LoadLeague(db, null, leagueId) is null)
                return;
        }

        await backups.CreateBackupAsync("delete-league", cancellationToken);

        using var deleteDb = factory.Open();
        using var tx = deleteDb.BeginTransaction();
        var draftIds = new List<string>();
        using (var drafts = deleteDb.Cmd("SELECT DraftId FROM Drafts WHERE LeagueId = $id;", tx)
                   .Bind("$id", leagueId.ToString()))
        using (var reader = drafts.ExecuteReader())
        {
            while (reader.Read())
                draftIds.Add(reader.GetString(0));
        }

        foreach (var draftId in draftIds)
        {
            DeleteDraftScopedRows(deleteDb, tx, draftId);
        }

        using (var cmd = deleteDb.Cmd("DELETE FROM Leagues WHERE LeagueId = $id;", tx)
                   .Bind("$id", leagueId.ToString()))
        {
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public Task<League> CreateLeagueAsync(CreateLeagueRequest request, CancellationToken cancellationToken = default)
    {
        var league = new League
        {
            LeagueId = LeagueId.New(),
            Name = request.Name.Trim(),
            Platform = request.Platform,
            Season = request.Season,
            TeamCount = request.TeamCount,
            DraftType = request.DraftType,
            RoundCount = request.RoundCount,
            RosterSize = RosterRules.Preset(request.Superflex).Sum(s => s.Count)
        };

        using var db = factory.Open();
        using var tx = db.BeginTransaction();
        InsertLeague(db, tx, league);

        var teams = new List<Team>();
        for (var i = 1; i <= request.TeamCount; i++)
        {
            var isUser = request.UserTeamName is not null && i == 1;
            teams.Add(new Team
            {
                TeamId = TeamId.New(),
                LeagueId = league.LeagueId,
                Name = isUser ? request.UserTeamName! : $"Team {i}",
                OwnerName = isUser ? "You" : null,
                DraftPosition = i
            });
        }

        league.UserTeamId = teams[0].TeamId;
        using (var cmd = db.Cmd("UPDATE Leagues SET UserTeamId = $t WHERE LeagueId = $id;", tx)
                   .Bind("$t", league.UserTeamId.Value.ToString())
                   .Bind("$id", league.LeagueId.ToString()))
        {
            cmd.ExecuteNonQuery();
        }

        foreach (var team in teams)
            InsertTeam(db, tx, team);

        foreach (var spec in RosterRules.Preset(request.Superflex))
        {
            var slot = new RosterSlot
            {
                RosterSlotId = RosterSlotId.New(),
                LeagueId = league.LeagueId,
                SlotCode = spec.SlotCode,
                SlotKind = spec.SlotKind,
                Count = spec.Count,
                EligiblePositions = spec.EligiblePositions
            };
            InsertRosterSlot(db, tx, slot);
        }

        foreach (var spec in RosterRules.DefaultScoring())
        {
            InsertScoring(db, tx, new ScoringRule
            {
                ScoringRuleId = ScoringRuleId.New(),
                LeagueId = league.LeagueId,
                Category = spec.Category,
                Points = spec.Points
            });
        }

        tx.Commit();
        return Task.FromResult(league);
    }

    public Task SaveLeagueDetailsAsync(LeagueId leagueId, string name, int season, int roundCount, string? draftGuidelines = null, CancellationToken cancellationToken = default)
    {
        var notes = string.IsNullOrWhiteSpace(draftGuidelines) ? null : draftGuidelines.Trim();
        var rounds = Math.Max(1, roundCount);
        using var db = factory.Open();
        using var tx = db.BeginTransaction();
        using (var cmd = db.Cmd("""
            UPDATE Leagues
            SET Name = $name, Season = $season, RoundCount = $rounds, DraftGuidelines = $notes
            WHERE LeagueId = $id;
            """, tx)
            .Bind("$name", name.Trim())
            .Bind("$season", season)
            .Bind("$rounds", rounds)
            .Bind("$notes", notes)
            .Bind("$id", leagueId.ToString()))
        {
            cmd.ExecuteNonQuery();
        }

        var changed = SyncDraftSlotsToRoundCount(db, tx, leagueId, rounds);
        tx.Commit();
        NotifyDrafts(changed);
        return Task.CompletedTask;
    }

    public Task SaveBoardPublishAsync(LeagueId leagueId, string? boardSlug, bool publishBoard, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? slug = null;
        if (!string.IsNullOrWhiteSpace(boardSlug))
        {
            if (!BoardSlug.TryNormalize(boardSlug, out var normalized))
                throw new ArgumentException("Web board slug may contain only lowercase letters, numbers, and hyphens.");
            slug = normalized;
        }

        using var db = factory.Open();
        using var cmd = db.Cmd("""
            UPDATE Leagues
            SET BoardSlug = $slug, PublishBoard = $pub
            WHERE LeagueId = $id;
            """)
            .Bind("$slug", slug)
            .Bind("$pub", publishBoard ? 1 : 0)
            .Bind("$id", leagueId.ToString());
        cmd.ExecuteNonQuery();
        return Task.CompletedTask;
    }

    public Task<League?> GetLeagueAsync(LeagueId leagueId, CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        return Task.FromResult(LoadLeague(db, null, leagueId));
    }

    public Task<IReadOnlyList<Team>> GetTeamsAsync(LeagueId leagueId, CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        return Task.FromResult<IReadOnlyList<Team>>(LoadTeams(db, null, leagueId));
    }

    public Task SaveTeamsAsync(SaveTeamsRequest request, CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        using var tx = db.BeginTransaction();
        using (var cmd = db.Cmd("DELETE FROM Teams WHERE LeagueId = $id;", tx).Bind("$id", request.LeagueId.ToString()))
            cmd.ExecuteNonQuery();

        foreach (var spec in request.Teams.OrderBy(t => t.DraftPosition))
        {
            InsertTeam(db, tx, new Team
            {
                TeamId = spec.TeamId ?? TeamId.New(),
                LeagueId = request.LeagueId,
                Name = spec.Name,
                OwnerName = spec.OwnerName,
                DisplayLabel = spec.DisplayLabel,
                PortraitNotes = spec.PortraitNotes,
                DraftPosition = spec.DraftPosition,
                ExternalTeamId = spec.ExternalTeamId,
                PracticePersonality = spec.PracticePersonality,
                PortraitArtStyle = spec.PortraitArtStyle
            });
        }

        if (request.UserTeamId is { } user)
        {
            using var cmd = db.Cmd("UPDATE Leagues SET UserTeamId = $t, TeamCount = $c WHERE LeagueId = $id;", tx)
                .Bind("$t", user.ToString())
                .Bind("$c", request.Teams.Count)
                .Bind("$id", request.LeagueId.ToString());
            cmd.ExecuteNonQuery();
        }
        else
        {
            using var cmd = db.Cmd("UPDATE Leagues SET TeamCount = $c WHERE LeagueId = $id;", tx)
                .Bind("$c", request.Teams.Count)
                .Bind("$id", request.LeagueId.ToString());
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RosterSlot>> GetRosterSlotsAsync(LeagueId leagueId, CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        return Task.FromResult<IReadOnlyList<RosterSlot>>(LoadRoster(db, null, leagueId));
    }

    public Task SaveRosterAsync(SaveRosterRequest request, CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        using var tx = db.BeginTransaction();
        using (var cmd = db.Cmd("DELETE FROM RosterSlots WHERE LeagueId = $id;", tx).Bind("$id", request.LeagueId.ToString()))
            cmd.ExecuteNonQuery();

        var rosterSize = 0;
        foreach (var spec in request.Slots)
        {
            rosterSize += spec.Count;
            InsertRosterSlot(db, tx, new RosterSlot
            {
                RosterSlotId = RosterSlotId.New(),
                LeagueId = request.LeagueId,
                SlotCode = spec.SlotCode,
                SlotKind = spec.SlotKind,
                Count = spec.Count,
                EligiblePositions = spec.EligiblePositions
            });
        }

        var draftedSpots = RosterRules.DraftedRosterSpots(request.Slots);
        var league = LoadLeague(db, tx, request.LeagueId)
                     ?? throw new InvalidOperationException("League not found.");
        var rounds = Math.Max(league.RoundCount, Math.Max(1, draftedSpots));
        using (var cmd = db.Cmd("UPDATE Leagues SET RosterSize = $s, RoundCount = $rounds WHERE LeagueId = $id;", tx)
                   .Bind("$s", rosterSize)
                   .Bind("$rounds", rounds)
                   .Bind("$id", request.LeagueId.ToString()))
        {
            cmd.ExecuteNonQuery();
        }

        var changed = SyncDraftSlotsToRoundCount(db, tx, request.LeagueId, rounds);
        tx.Commit();
        NotifyDrafts(changed);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ScoringRule>> GetScoringRulesAsync(LeagueId leagueId, CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        return Task.FromResult<IReadOnlyList<ScoringRule>>(LoadScoring(db, null, leagueId));
    }

    public Task SaveScoringAsync(SaveScoringRequest request, CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        using var tx = db.BeginTransaction();
        using (var cmd = db.Cmd("DELETE FROM ScoringRules WHERE LeagueId = $id;", tx).Bind("$id", request.LeagueId.ToString()))
            cmd.ExecuteNonQuery();
        foreach (var spec in request.Rules)
        {
            InsertScoring(db, tx, new ScoringRule
            {
                ScoringRuleId = ScoringRuleId.New(),
                LeagueId = request.LeagueId,
                Category = spec.Category,
                Points = spec.Points
            });
        }

        tx.Commit();
        return Task.CompletedTask;
    }

    public Task SaveDraftOrderAsync(SaveDraftOrderRequest request, CancellationToken cancellationToken = default)
    {
        var ordered = request.Teams.OrderBy(t => t.DraftPosition).ToList();
        if (ordered.Count == 0)
            throw new InvalidOperationException("A draft order needs at least one team.");
        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].DraftPosition != i + 1 || ordered[i].TeamId is null)
                throw new InvalidOperationException("Draft positions must be a contiguous sequence starting at 1.");
        }

        using var db = factory.Open();
        using var tx = db.BeginTransaction();
        var league = LoadLeague(db, tx, request.LeagueId)
                     ?? throw new InvalidOperationException("League not found.");

        if (request.DraftId is { } draftId)
        {
            var draft = LoadDraft(db, tx, draftId)
                        ?? throw new InvalidOperationException("Draft not found.");
            if (!draft.LeagueId.Equals(request.LeagueId))
                throw new InvalidOperationException("That draft does not belong to this league.");
            var live = LiveBranch(db, tx, draftId) ?? LoadBranch(db, tx, draft.ActiveBranchId);
            var selections = live is null
                ? []
                : LoadSelections(db, tx, draftId, live.BranchId);
            var check = KeeperRules.ValidateCanReorderSeats(draft.Status, selections);
            if (!check.IsValid)
                throw new InvalidOperationException(check.Error);

            ReplaceSlots(db, tx, draftId, request.DraftType, ordered, league.RoundCount);
        }

        using (var cmd = db.Cmd("UPDATE Leagues SET DraftType = $type WHERE LeagueId = $id;", tx)
                   .Bind("$type", request.DraftType.ToString())
                   .Bind("$id", request.LeagueId.ToString()))
        {
            cmd.ExecuteNonQuery();
        }

        foreach (var team in ordered)
        {
            using var cmd = db.Cmd("UPDATE Teams SET DraftPosition = $p WHERE TeamId = $id AND LeagueId = $league;", tx)
                .Bind("$p", team.DraftPosition)
                .Bind("$id", team.TeamId!.Value.ToString())
                .Bind("$league", request.LeagueId.ToString());
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
        return Task.CompletedTask;
    }

    public Task<Draft> CreateDraftAsync(CreateDraftRequest request, CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        using var tx = db.BeginTransaction();
        var league = LoadLeague(db, tx, request.LeagueId)
                     ?? throw new InvalidOperationException("League not found.");
        if (league.ArchivedAt is not null)
            throw new InvalidOperationException("Restore the league before creating a draft.");
        var teams = LoadTeams(db, tx, request.LeagueId);
        if (teams.Count == 0)
            throw new InvalidOperationException("League has no teams.");

        var draftId = DraftId.New();
        var branchId = BranchId.New();
        var now = DateTimeOffset.UtcNow;
        var draft = new Draft
        {
            DraftId = draftId,
            LeagueId = request.LeagueId,
            Name = request.Name,
            Season = league.Season,
            Status = DraftStatus.NotStarted,
            ActiveBranchId = branchId,
            SourceMode = league.DraftSourcePreference == DraftSourcePreference.Yahoo
                ? DraftSourceMode.YahooSynchronized
                : DraftSourceMode.Manual,
            CreatedAt = now
        };

        using (var cmd = db.Cmd("""
            INSERT INTO Drafts(DraftId, LeagueId, Name, Season, Status, ActiveBranchId, CurrentStateVersion, SourceMode, CreatedAt)
            VALUES ($id, $league, $name, $season, $status, $branch, 0, $mode, $created);
            """, tx)
                   .Bind("$id", draftId.ToString())
                   .Bind("$league", request.LeagueId.ToString())
                   .Bind("$name", draft.Name)
                   .Bind("$season", draft.Season)
                   .Bind("$status", draft.Status.ToString())
                   .Bind("$branch", branchId.ToString())
                   .Bind("$mode", draft.SourceMode.ToString())
                   .Bind("$created", now.ToString("O")))
        {
            cmd.ExecuteNonQuery();
        }

        using (var cmd = db.Cmd("""
            INSERT INTO DraftBranches(BranchId, DraftId, Name, ParentBranchId, BranchPointOverallPick, CreatedFromStateVersion, CreatedAt)
            VALUES ($id, $draft, $name, NULL, 0, 0, $created);
            """, tx)
                   .Bind("$id", branchId.ToString())
                   .Bind("$draft", draftId.ToString())
                   .Bind("$name", "Main Draft")
                   .Bind("$created", now.ToString("O")))
        {
            cmd.ExecuteNonQuery();
        }

        var positions = teams.Select(t => new TeamDraftPosition
        {
            TeamId = t.TeamId,
            Name = t.Name,
            DraftPosition = t.DraftPosition
        }).ToList();
        var slots = DraftSlotGenerator.ToDraftSlots(draftId, DraftSlotGenerator.Generate(league.DraftType, positions, league.RoundCount));
        foreach (var slot in slots)
            InsertSlot(db, tx, slot);

        tx.Commit();
        return Task.FromResult(draft);
    }

    public Task SaveKeepersAsync(SaveKeepersRequest request, CancellationToken cancellationToken = default)
    {
        var validation = KeeperRules.Validate(request.Keepers);
        if (!validation.IsValid)
            throw new InvalidOperationException(validation.Error);

        using var db = factory.Open();
        using var tx = db.BeginTransaction();
        var draft = LoadDraft(db, tx, request.DraftId)
                    ?? throw new InvalidOperationException("Draft not found.");
        var selections = LoadSelections(db, tx, request.DraftId, draft.ActiveBranchId);
        var editCheck = KeeperRules.ValidateCanEditKeepers(draft.Status, selections);
        if (!editCheck.IsValid)
            throw new InvalidOperationException(editCheck.Error);

        var slots = LoadSlots(db, tx, request.DraftId);
        using (var clearMarks = db.Cmd("UPDATE DraftSlots SET IsKeeperSlot = 0 WHERE DraftId = $id;", tx)
                   .Bind("$id", request.DraftId.ToString()))
        {
            clearMarks.ExecuteNonQuery();
        }

        using (var cmd = db.Cmd("DELETE FROM Keepers WHERE DraftId = $id;", tx).Bind("$id", request.DraftId.ToString()))
            cmd.ExecuteNonQuery();

        foreach (var spec in request.Keepers)
        {
            var keeper = new Keeper
            {
                KeeperId = KeeperId.New(),
                DraftId = request.DraftId,
                TeamId = spec.TeamId,
                PlayerId = spec.PlayerId,
                RoundCost = spec.RoundCost,
                Notes = spec.Notes
            };
            var slot = KeeperRules.ResolveSlot(slots, keeper);
            if (slot is not null)
            {
                keeper.DraftSlotId = slot.DraftSlotId;
                using var mark = db.Cmd("UPDATE DraftSlots SET IsKeeperSlot = 1 WHERE DraftSlotId = $id;", tx)
                    .Bind("$id", slot.DraftSlotId.ToString());
                mark.ExecuteNonQuery();
            }

            InsertKeeper(db, tx, keeper);
        }

        if (draft.Status == DraftStatus.InProgress)
        {
            var state = DraftStateLoader.Load(db, tx, request.DraftId);
            var applied = DraftEngine.ApplyKeepers(state);
            if (!applied.Succeeded)
                throw new InvalidOperationException(applied.Error ?? "Could not apply keepers to the board.");

            DraftCommandService.Persist(db, tx, state);
            tx.Commit();
            notifier.Notify(state.Draft.DraftId, state.ActiveBranch.BranchId, state.Draft.CurrentStateVersion);
            return Task.CompletedTask;
        }

        tx.Commit();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Keeper>> GetKeepersAsync(DraftId draftId, CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        return Task.FromResult<IReadOnlyList<Keeper>>(LoadKeepers(db, null, draftId));
    }

    public Task<IReadOnlyList<Draft>> ListDraftsAsync(LeagueId leagueId, CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        using var cmd = db.Cmd("SELECT * FROM Drafts WHERE LeagueId = $id ORDER BY CreatedAt DESC;").Bind("$id", leagueId.ToString());
        using var reader = cmd.ExecuteReader();
        var list = new List<Draft>();
        while (reader.Read())
            list.Add(ReadDraft(reader));
        return Task.FromResult<IReadOnlyList<Draft>>(list);
    }

    public Task<Draft?> GetDraftAsync(DraftId draftId, CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        return Task.FromResult(LoadDraft(db, null, draftId));
    }

    public Task<IReadOnlyList<DraftBranch>> GetBranchesAsync(DraftId draftId, CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        return Task.FromResult<IReadOnlyList<DraftBranch>>(LoadBranches(db, null, draftId));
    }

    public Task<League?> FindByExternalIdAsync(FantasyPlatform platform, string externalLeagueId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalLeagueId))
            return Task.FromResult<League?>(null);

        using var db = factory.Open();
        using var cmd = db.Cmd("""
            SELECT LeagueId FROM Leagues
            WHERE Platform = $p AND ExternalLeagueId = $ext
            LIMIT 1;
            """)
            .Bind("$p", platform.ToString())
            .Bind("$ext", externalLeagueId.Trim());
        var id = cmd.ExecuteScalar() as string;
        return Task.FromResult(id is null ? null : LoadLeague(db, null, LeagueId.Parse(id)));
    }

    public Task<League> UpsertImportedLeagueAsync(ImportedLeagueRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ExternalLeagueId))
            throw new InvalidOperationException("Imported leagues need an external league id.");
        if (request.Teams.Count == 0)
            throw new InvalidOperationException("Imported leagues need at least one team.");

        using var db = factory.Open();
        using var tx = db.BeginTransaction();
        var existing = LoadLeagueByExternalId(db, tx, request.Platform, request.ExternalLeagueId.Trim());
        var league = existing ?? new League
        {
            LeagueId = LeagueId.New(),
            Name = request.Name.Trim(),
            Platform = request.Platform,
            ExternalLeagueId = request.ExternalLeagueId.Trim(),
            Season = request.Season,
            TeamCount = request.Teams.Count,
            DraftType = request.DraftType,
            RoundCount = request.RoundCount,
            RosterSize = request.Roster.Sum(s => s.Count),
            DraftSourcePreference = request.SourcePreference
        };

        if (existing is null)
        {
            InsertLeague(db, tx, league);
        }
        else
        {
            league.Name = request.Name.Trim();
            league.Season = request.Season;
            league.TeamCount = request.Teams.Count;
            league.DraftType = request.DraftType;
            league.RoundCount = request.RoundCount;
            league.RosterSize = request.Roster.Sum(s => s.Count);
            league.DraftSourcePreference = request.SourcePreference;
            league.ArchivedAt = null;
            UpdateImportedLeague(db, tx, league);
        }

        var existingTeams = existing is null ? [] : LoadTeams(db, tx, league.LeagueId);
        var byExternal = existingTeams
            .Where(t => !string.IsNullOrWhiteSpace(t.ExternalTeamId))
            .ToDictionary(t => t.ExternalTeamId!, StringComparer.OrdinalIgnoreCase);
        var usedPositions = new HashSet<int>();
        if (!request.ReplaceDraftOrder)
        {
            foreach (var team in existingTeams)
                usedPositions.Add(team.DraftPosition);
        }

        TeamId? userTeamId = null;
        var nextFree = 1;
        foreach (var spec in request.Teams.OrderBy(t => t.SuggestedDraftPosition))
        {
            if (!byExternal.TryGetValue(spec.ExternalTeamId, out var team))
            {
                int position;
                if (request.ReplaceDraftOrder || existing is null)
                {
                    position = spec.SuggestedDraftPosition;
                }
                else
                {
                    while (usedPositions.Contains(nextFree))
                        nextFree++;
                    position = nextFree;
                    usedPositions.Add(position);
                    nextFree++;
                }

                team = new Team
                {
                    TeamId = TeamId.New(),
                    LeagueId = league.LeagueId,
                    Name = spec.Name,
                    OwnerName = spec.OwnerName,
                    DraftPosition = position,
                    ExternalTeamId = spec.ExternalTeamId
                };
                InsertTeam(db, tx, team);
                byExternal[spec.ExternalTeamId] = team;
            }
            else
            {
                var position = request.ReplaceDraftOrder || existing is null
                    ? spec.SuggestedDraftPosition
                    : team.DraftPosition;
                using var cmd = db.Cmd("""
                    UPDATE Teams
                    SET Name = $name, OwnerName = $owner, DraftPosition = $pos, ExternalTeamId = $ext
                    WHERE TeamId = $id;
                    """, tx)
                    .Bind("$name", spec.Name)
                    .Bind("$owner", spec.OwnerName)
                    .Bind("$pos", position)
                    .Bind("$ext", spec.ExternalTeamId)
                    .Bind("$id", team.TeamId.ToString());
                cmd.ExecuteNonQuery();
            }

            if (spec.IsUserTeam)
                userTeamId = team.TeamId;
        }

        userTeamId ??= existing?.UserTeamId ?? existingTeams.FirstOrDefault()?.TeamId;
        using (var cmd = db.Cmd("UPDATE Leagues SET UserTeamId = $t, TeamCount = $c WHERE LeagueId = $id;", tx)
                   .Bind("$t", userTeamId?.ToString())
                   .Bind("$c", request.Teams.Count)
                   .Bind("$id", league.LeagueId.ToString()))
        {
            cmd.ExecuteNonQuery();
        }

        league.UserTeamId = userTeamId;

        using (var cmd = db.Cmd("DELETE FROM RosterSlotEligiblePositions WHERE RosterSlotId IN (SELECT RosterSlotId FROM RosterSlots WHERE LeagueId = $id);", tx)
                   .Bind("$id", league.LeagueId.ToString()))
        {
            cmd.ExecuteNonQuery();
        }

        using (var cmd = db.Cmd("DELETE FROM RosterSlots WHERE LeagueId = $id;", tx).Bind("$id", league.LeagueId.ToString()))
            cmd.ExecuteNonQuery();
        foreach (var spec in request.Roster.Where(s => s.Count > 0))
        {
            InsertRosterSlot(db, tx, new RosterSlot
            {
                RosterSlotId = RosterSlotId.New(),
                LeagueId = league.LeagueId,
                SlotCode = spec.SlotCode,
                SlotKind = spec.SlotKind,
                Count = spec.Count,
                EligiblePositions = spec.EligiblePositions
            });
        }

        using (var cmd = db.Cmd("DELETE FROM ScoringRules WHERE LeagueId = $id;", tx).Bind("$id", league.LeagueId.ToString()))
            cmd.ExecuteNonQuery();
        foreach (var spec in request.Scoring)
        {
            InsertScoring(db, tx, new ScoringRule
            {
                ScoringRuleId = ScoringRuleId.New(),
                LeagueId = league.LeagueId,
                Category = spec.Category,
                Points = spec.Points
            });
        }

        if (request.ReplaceDraftOrder)
        {
            var drafts = LoadDraftsForLeague(db, tx, league.LeagueId);
            foreach (var draft in drafts.Where(d => d.Status == DraftStatus.NotStarted))
            {
                var ordered = request.Teams
                    .OrderBy(t => t.SuggestedDraftPosition)
                    .Select(t =>
                    {
                        byExternal.TryGetValue(t.ExternalTeamId, out var team);
                        return new TeamDraftPosition
                        {
                            TeamId = team?.TeamId,
                            Name = t.Name,
                            DraftPosition = t.SuggestedDraftPosition
                        };
                    })
                    .Where(t => t.TeamId is not null)
                    .ToList();
                if (ordered.Count == request.Teams.Count)
                    ReplaceSlots(db, tx, draft.DraftId, request.DraftType, ordered, league.RoundCount);
            }
        }

        tx.Commit();
        return Task.FromResult(league);
    }

    internal static League? LoadLeague(SqliteConnection db, SqliteTransaction? tx, LeagueId id)
    {
        using var cmd = db.Cmd("SELECT * FROM Leagues WHERE LeagueId = $id;", tx).Bind("$id", id.ToString());
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return null;
        return new League
        {
            LeagueId = LeagueId.Parse(reader.GetString(reader.GetOrdinal("LeagueId"))),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Platform = Enum.Parse<FantasyPlatform>(reader.GetString(reader.GetOrdinal("Platform"))),
            ExternalLeagueId = reader.GetNullString(reader.GetOrdinal("ExternalLeagueId")),
            Season = reader.GetInt32(reader.GetOrdinal("Season")),
            TeamCount = reader.GetInt32(reader.GetOrdinal("TeamCount")),
            UserTeamId = reader.IsDBNull(reader.GetOrdinal("UserTeamId")) ? null : TeamId.Parse(reader.GetString(reader.GetOrdinal("UserTeamId"))),
            DraftType = Enum.Parse<DraftType>(reader.GetString(reader.GetOrdinal("DraftType"))),
            RoundCount = reader.GetInt32(reader.GetOrdinal("RoundCount")),
            RosterSize = reader.GetInt32(reader.GetOrdinal("RosterSize")),
            DraftSourcePreference = Enum.Parse<DraftSourcePreference>(reader.GetString(reader.GetOrdinal("DraftSourcePreference"))),
            DraftGuidelines = reader.GetNullString(reader.GetOrdinal("DraftGuidelines")),
            CreatedAt = reader.GetTime(reader.GetOrdinal("CreatedAt")),
            ArchivedAt = reader.GetNullTime(reader.GetOrdinal("ArchivedAt")),
            BoardSlug = reader.GetNullString(reader.GetOrdinal("BoardSlug")),
            PublishBoard = reader.GetInt32(reader.GetOrdinal("PublishBoard")) != 0
        };
    }

    internal static List<Team> LoadTeams(SqliteConnection db, SqliteTransaction? tx, LeagueId leagueId)
    {
        using var cmd = db.Cmd("SELECT * FROM Teams WHERE LeagueId = $id ORDER BY DraftPosition;", tx).Bind("$id", leagueId.ToString());
        using var reader = cmd.ExecuteReader();
        var list = new List<Team>();
        while (reader.Read())
        {
            list.Add(new Team
            {
                TeamId = TeamId.Parse(reader.GetString(reader.GetOrdinal("TeamId"))),
                LeagueId = LeagueId.Parse(reader.GetString(reader.GetOrdinal("LeagueId"))),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                OwnerName = reader.GetNullString(reader.GetOrdinal("OwnerName")),
                DisplayLabel = reader.GetNullString(reader.GetOrdinal("DisplayLabel")),
                PortraitNotes = reader.GetNullString(reader.GetOrdinal("PortraitNotes")),
                DraftPosition = reader.GetInt32(reader.GetOrdinal("DraftPosition")),
                ExternalTeamId = reader.GetNullString(reader.GetOrdinal("ExternalTeamId")),
                PracticePersonality = reader.GetNullString(reader.GetOrdinal("PracticePersonality")) is { } personality
                    ? Enum.Parse<MockPersonality>(personality)
                    : null,
                PortraitArtStyle = reader.GetNullString(reader.GetOrdinal("PortraitArtStyle"))
            });
        }

        return list;
    }

    internal static List<RosterSlot> LoadRoster(SqliteConnection db, SqliteTransaction? tx, LeagueId leagueId)
    {
        using var cmd = db.Cmd("SELECT * FROM RosterSlots WHERE LeagueId = $id;", tx).Bind("$id", leagueId.ToString());
        using var reader = cmd.ExecuteReader();
        var slots = new List<RosterSlot>();
        var ids = new List<string>();
        while (reader.Read())
        {
            var id = reader.GetString(reader.GetOrdinal("RosterSlotId"));
            ids.Add(id);
            slots.Add(new RosterSlot
            {
                RosterSlotId = RosterSlotId.Parse(id),
                LeagueId = leagueId,
                SlotCode = reader.GetString(reader.GetOrdinal("SlotCode")),
                SlotKind = Enum.Parse<SlotKind>(reader.GetString(reader.GetOrdinal("SlotKind"))),
                Count = reader.GetInt32(reader.GetOrdinal("Count")),
                EligiblePositions = []
            });
        }

        var eligibility = new Dictionary<string, List<PlayerPosition>>();
        if (ids.Count > 0)
        {
            using var pos = db.Cmd("SELECT RosterSlotId, Position FROM RosterSlotEligiblePositions;", tx);
            using var posReader = pos.ExecuteReader();
            while (posReader.Read())
            {
                var id = posReader.GetString(0);
                if (!eligibility.TryGetValue(id, out var list))
                    eligibility[id] = list = [];
                list.Add(Enum.Parse<PlayerPosition>(posReader.GetString(1)));
            }
        }

        return slots.Select(s =>
        {
            s.EligiblePositions = eligibility.GetValueOrDefault(s.RosterSlotId.ToString()) ?? [];
            return s;
        }).ToList();
    }

    internal static List<ScoringRule> LoadScoring(SqliteConnection db, SqliteTransaction? tx, LeagueId leagueId)
    {
        using var cmd = db.Cmd("SELECT * FROM ScoringRules WHERE LeagueId = $id;", tx).Bind("$id", leagueId.ToString());
        using var reader = cmd.ExecuteReader();
        var list = new List<ScoringRule>();
        while (reader.Read())
        {
            list.Add(new ScoringRule
            {
                ScoringRuleId = ScoringRuleId.Parse(reader.GetString(reader.GetOrdinal("ScoringRuleId"))),
                LeagueId = leagueId,
                Category = Enum.Parse<ScoringCategory>(reader.GetString(reader.GetOrdinal("Category"))),
                Points = decimal.Parse(reader.GetString(reader.GetOrdinal("Points")))
            });
        }

        return ScoringCatalog.Complete(leagueId, list).ToList();
    }

    internal static Draft? LoadDraft(SqliteConnection db, SqliteTransaction? tx, DraftId id)
    {
        using var cmd = db.Cmd("SELECT * FROM Drafts WHERE DraftId = $id;", tx).Bind("$id", id.ToString());
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadDraft(reader) : null;
    }

    internal static Draft ReadDraft(SqliteDataReader reader) => new()
    {
        DraftId = DraftId.Parse(reader.GetString(reader.GetOrdinal("DraftId"))),
        LeagueId = LeagueId.Parse(reader.GetString(reader.GetOrdinal("LeagueId"))),
        Name = reader.GetString(reader.GetOrdinal("Name")),
        Season = reader.GetInt32(reader.GetOrdinal("Season")),
        Status = Enum.Parse<DraftStatus>(reader.GetString(reader.GetOrdinal("Status"))),
        ActiveBranchId = BranchId.Parse(reader.GetString(reader.GetOrdinal("ActiveBranchId"))),
        CurrentStateVersion = reader.GetInt32(reader.GetOrdinal("CurrentStateVersion")),
        SourceMode = Enum.Parse<DraftSourceMode>(reader.GetString(reader.GetOrdinal("SourceMode"))),
        CreatedAt = reader.GetTime(reader.GetOrdinal("CreatedAt")),
        StartedAt = reader.GetNullTime(reader.GetOrdinal("StartedAt")),
        CompletedAt = reader.GetNullTime(reader.GetOrdinal("CompletedAt"))
    };

    internal static DraftBranch? LiveBranch(SqliteConnection db, SqliteTransaction? tx, DraftId draftId)
    {
        var branches = LoadBranches(db, tx, draftId);
        return branches.FirstOrDefault(branch => branch.ParentBranchId is null)
               ?? branches.OrderBy(branch => branch.CreatedAt).FirstOrDefault();
    }

    internal static List<DraftBranch> LoadBranches(SqliteConnection db, SqliteTransaction? tx, DraftId draftId)
    {
        using var cmd = db.Cmd("SELECT * FROM DraftBranches WHERE DraftId = $id;", tx).Bind("$id", draftId.ToString());
        using var reader = cmd.ExecuteReader();
        var list = new List<DraftBranch>();
        while (reader.Read())
            list.Add(ReadBranch(reader));
        return list;
    }

    internal static DraftBranch? LoadBranch(SqliteConnection db, SqliteTransaction? tx, BranchId id)
    {
        using var cmd = db.Cmd("SELECT * FROM DraftBranches WHERE BranchId = $id;", tx).Bind("$id", id.ToString());
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadBranch(reader) : null;
    }

    internal static DraftBranch ReadBranch(SqliteDataReader reader) => new()
    {
        BranchId = BranchId.Parse(reader.GetString(reader.GetOrdinal("BranchId"))),
        DraftId = DraftId.Parse(reader.GetString(reader.GetOrdinal("DraftId"))),
        Name = reader.GetString(reader.GetOrdinal("Name")),
        ParentBranchId = reader.IsDBNull(reader.GetOrdinal("ParentBranchId")) ? null : BranchId.Parse(reader.GetString(reader.GetOrdinal("ParentBranchId"))),
        BranchPointOverallPick = reader.GetInt32(reader.GetOrdinal("BranchPointOverallPick")),
        CreatedFromStateVersion = reader.GetInt32(reader.GetOrdinal("CreatedFromStateVersion")),
        CurrentHeadEventId = reader.IsDBNull(reader.GetOrdinal("CurrentHeadEventId")) ? null : EventId.Parse(reader.GetString(reader.GetOrdinal("CurrentHeadEventId"))),
        CreatedAt = reader.GetTime(reader.GetOrdinal("CreatedAt"))
    };

    internal static List<DraftSlot> LoadSlots(SqliteConnection db, SqliteTransaction? tx, DraftId draftId)
    {
        using var cmd = db.Cmd("SELECT * FROM DraftSlots WHERE DraftId = $id ORDER BY OverallPick;", tx).Bind("$id", draftId.ToString());
        using var reader = cmd.ExecuteReader();
        var list = new List<DraftSlot>();
        while (reader.Read())
        {
            list.Add(new DraftSlot
            {
                DraftSlotId = DraftSlotId.Parse(reader.GetString(reader.GetOrdinal("DraftSlotId"))),
                DraftId = draftId,
                OverallPick = reader.GetInt32(reader.GetOrdinal("OverallPick")),
                Round = reader.GetInt32(reader.GetOrdinal("Round")),
                RoundPick = reader.GetInt32(reader.GetOrdinal("RoundPick")),
                TeamId = TeamId.Parse(reader.GetString(reader.GetOrdinal("TeamId"))),
                IsKeeperSlot = reader.GetInt32(reader.GetOrdinal("IsKeeperSlot")) == 1
            });
        }

        return list;
    }

    internal static List<Keeper> LoadKeepers(SqliteConnection db, SqliteTransaction? tx, DraftId draftId)
    {
        using var cmd = db.Cmd("SELECT * FROM Keepers WHERE DraftId = $id;", tx).Bind("$id", draftId.ToString());
        using var reader = cmd.ExecuteReader();
        var list = new List<Keeper>();
        while (reader.Read())
        {
            list.Add(new Keeper
            {
                KeeperId = KeeperId.Parse(reader.GetString(reader.GetOrdinal("KeeperId"))),
                DraftId = draftId,
                TeamId = TeamId.Parse(reader.GetString(reader.GetOrdinal("TeamId"))),
                PlayerId = PlayerId.Parse(reader.GetString(reader.GetOrdinal("PlayerId"))),
                RoundCost = reader.GetInt32(reader.GetOrdinal("RoundCost")),
                DraftSlotId = reader.IsDBNull(reader.GetOrdinal("DraftSlotId")) ? null : DraftSlotId.Parse(reader.GetString(reader.GetOrdinal("DraftSlotId"))),
                Notes = reader.GetNullString(reader.GetOrdinal("Notes"))
            });
        }

        return list;
    }

    internal static List<DraftEventRecord> LoadEvents(SqliteConnection db, SqliteTransaction? tx, DraftId draftId, BranchId branchId)
    {
        using var cmd = db.Cmd("""
            SELECT * FROM DraftEvents
            WHERE DraftId = $d AND (BranchId = $b OR EventType = 'DraftStarted')
            ORDER BY SequenceNumber;
            """, tx)
            .Bind("$d", draftId.ToString())
            .Bind("$b", branchId.ToString());
        using var reader = cmd.ExecuteReader();
        var list = new List<DraftEventRecord>();
        while (reader.Read())
        {
            list.Add(new DraftEventRecord
            {
                EventId = EventId.Parse(reader.GetString(reader.GetOrdinal("EventId"))),
                DraftId = draftId,
                BranchId = BranchId.Parse(reader.GetString(reader.GetOrdinal("BranchId"))),
                EventType = Enum.Parse<DraftEventType>(reader.GetString(reader.GetOrdinal("EventType"))),
                StateVersion = reader.GetInt32(reader.GetOrdinal("StateVersion")),
                SequenceNumber = reader.GetInt32(reader.GetOrdinal("SequenceNumber")),
                CreatedAt = reader.GetTime(reader.GetOrdinal("CreatedAt")),
                CreatedBy = reader.GetString(reader.GetOrdinal("CreatedBy")),
                CorrelationId = reader.GetNullString(reader.GetOrdinal("CorrelationId")),
                PayloadJson = reader.GetString(reader.GetOrdinal("PayloadJson"))
            });
        }

        return list;
    }

    /// <summary>
    /// True once a branch's own selection rows have been written at least once, which makes
    /// them authoritative even when empty. See migration 013.
    /// </summary>
    internal static bool SelectionsMaterialized(SqliteConnection db, SqliteTransaction? tx, BranchId branchId)
    {
        using var cmd = db.Cmd("SELECT SelectionsMaterialized FROM DraftBranches WHERE BranchId = $b;", tx)
            .Bind("$b", branchId.ToString());
        return cmd.ExecuteScalar() is long flag && flag != 0;
    }

    internal static void MarkSelectionsMaterialized(SqliteConnection db, SqliteTransaction? tx, BranchId branchId)
    {
        using var cmd = db.Cmd("UPDATE DraftBranches SET SelectionsMaterialized = 1 WHERE BranchId = $b;", tx)
            .Bind("$b", branchId.ToString());
        cmd.ExecuteNonQuery();
    }

    internal static List<ActiveSelection> LoadSelections(SqliteConnection db, SqliteTransaction? tx, DraftId draftId, BranchId branchId)
    {
        using var cmd = db.Cmd("""
            SELECT * FROM ActiveDraftSelections WHERE DraftId = $d AND BranchId = $b ORDER BY OverallPick;
            """, tx)
            .Bind("$d", draftId.ToString())
            .Bind("$b", branchId.ToString());
        using var reader = cmd.ExecuteReader();
        var list = new List<ActiveSelection>();
        while (reader.Read())
        {
            list.Add(new ActiveSelection
            {
                EventId = EventId.Parse(reader.GetString(reader.GetOrdinal("EventId"))),
                DraftId = draftId,
                BranchId = branchId,
                DraftSlotId = DraftSlotId.Parse(reader.GetString(reader.GetOrdinal("DraftSlotId"))),
                OverallPick = reader.GetInt32(reader.GetOrdinal("OverallPick")),
                Round = reader.GetInt32(reader.GetOrdinal("Round")),
                RoundPick = reader.GetInt32(reader.GetOrdinal("RoundPick")),
                TeamId = TeamId.Parse(reader.GetString(reader.GetOrdinal("TeamId"))),
                PlayerId = PlayerId.Parse(reader.GetString(reader.GetOrdinal("PlayerId"))),
                Source = Enum.Parse<PickSource>(reader.GetString(reader.GetOrdinal("Source"))),
                ExternalSourceId = reader.GetNullString(reader.GetOrdinal("ExternalSourceId")),
                ObservedAt = reader.GetTime(reader.GetOrdinal("ObservedAt"))
            });
        }

        return list;
    }

    internal static List<DraftQueueItem> LoadQueue(SqliteConnection db, SqliteTransaction? tx, DraftId draftId, BranchId branchId)
    {
        using var cmd = db.Cmd("""
            SELECT * FROM DraftQueueItems WHERE DraftId = $d AND BranchId = $b ORDER BY SortOrder;
            """, tx)
            .Bind("$d", draftId.ToString())
            .Bind("$b", branchId.ToString());
        using var reader = cmd.ExecuteReader();
        var list = new List<DraftQueueItem>();
        while (reader.Read())
        {
            list.Add(new DraftQueueItem
            {
                QueueItemId = QueueItemId.Parse(reader.GetString(reader.GetOrdinal("QueueItemId"))),
                DraftId = draftId,
                BranchId = branchId,
                PlayerId = PlayerId.Parse(reader.GetString(reader.GetOrdinal("PlayerId"))),
                SortOrder = reader.GetInt32(reader.GetOrdinal("SortOrder")),
                CreatedAt = reader.GetTime(reader.GetOrdinal("CreatedAt"))
            });
        }

        return list;
    }

    internal static List<Player> LoadPlayers(SqliteConnection db, SqliteTransaction? tx = null)
    {
        using var cmd = db.Cmd("SELECT * FROM Players ORDER BY Name;", tx);
        using var reader = cmd.ExecuteReader();
        var players = new List<Player>();
        while (reader.Read())
        {
            players.Add(new Player
            {
                PlayerId = PlayerId.Parse(reader.GetString(reader.GetOrdinal("PlayerId"))),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                NflTeam = reader.GetString(reader.GetOrdinal("NflTeam")),
                PrimaryPosition = Enum.Parse<PlayerPosition>(reader.GetString(reader.GetOrdinal("PrimaryPosition"))),
                EligiblePositions = [Enum.Parse<PlayerPosition>(reader.GetString(reader.GetOrdinal("PrimaryPosition")))],
                ByeWeek = reader.GetNullInt(reader.GetOrdinal("ByeWeek")),
                YearsExp = reader.GetNullInt(reader.GetOrdinal("YearsExp")),
                Status = Enum.Parse<PlayerStatus>(reader.GetString(reader.GetOrdinal("Status"))),
                StatusUpdatedAt = reader.GetNullTime(reader.GetOrdinal("StatusUpdatedAt")),
                InjuryBodyPart = reader.GetNullString(reader.GetOrdinal("InjuryBodyPart")),
                InjuryNotes = reader.GetNullString(reader.GetOrdinal("InjuryNotes")),
                InjuryStartedOn = reader.GetNullString(reader.GetOrdinal("InjuryStartedOn"))
            });
        }

        return players;
    }

    internal static int NextSequence(SqliteConnection db, SqliteTransaction? tx, DraftId draftId)
    {
        using var cmd = db.Cmd("SELECT COALESCE(MAX(SequenceNumber), 0) + 1 FROM DraftEvents WHERE DraftId = $id;", tx)
            .Bind("$id", draftId.ToString());
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private Task<IReadOnlyList<LeagueSummary>> ListLeaguesCoreAsync(bool archived)
    {
        using var db = factory.Open();
        using var cmd = db.Cmd("""
            SELECT l.LeagueId, l.Name, l.Season, l.TeamCount, l.DraftType, l.Platform,
                   d.DraftId, d.Status, l.ArchivedAt,
                   (SELECT COUNT(*) FROM Drafts WHERE LeagueId = l.LeagueId) AS DraftCount
            FROM Leagues l
            LEFT JOIN Drafts d ON d.LeagueId = l.LeagueId
              AND d.CreatedAt = (SELECT MAX(CreatedAt) FROM Drafts WHERE LeagueId = l.LeagueId)
            WHERE ($archived = 1 AND l.ArchivedAt IS NOT NULL)
               OR ($archived = 0 AND l.ArchivedAt IS NULL)
            ORDER BY COALESCE(l.ArchivedAt, l.CreatedAt) DESC;
            """)
            .Bind("$archived", archived ? 1 : 0);
        using var reader = cmd.ExecuteReader();
        var list = new List<LeagueSummary>();
        while (reader.Read())
        {
            list.Add(new LeagueSummary
            {
                LeagueId = LeagueId.Parse(reader.GetString(0)),
                Name = reader.GetString(1),
                Season = reader.GetInt32(2),
                TeamCount = reader.GetInt32(3),
                DraftType = Enum.Parse<DraftType>(reader.GetString(4)),
                Platform = Enum.Parse<FantasyPlatform>(reader.GetString(5)),
                ActiveDraftId = reader.IsDBNull(6) ? null : DraftId.Parse(reader.GetString(6)),
                ActiveDraftStatus = reader.IsDBNull(7) ? null : Enum.Parse<DraftStatus>(reader.GetString(7)),
                ArchivedAt = reader.GetNullTime(8),
                DraftCount = Convert.ToInt32(reader.GetValue(9))
            });
        }

        return Task.FromResult<IReadOnlyList<LeagueSummary>>(list);
    }

    private static void DeleteDraftScopedRows(SqliteConnection db, SqliteTransaction tx, string draftId)
    {
        foreach (var sql in new[]
        {
            "DELETE FROM PlayerAvailabilityProjection WHERE DraftId = $id;",
            "DELETE FROM TeamRosterProjection WHERE DraftId = $id;",
            "DELETE FROM ActiveDraftSelections WHERE DraftId = $id;",
            "DELETE FROM AiResponses WHERE DraftId = $id;",
            "DELETE FROM AiUsageRecords WHERE DraftId = $id;",
            "DELETE FROM DraftQueueItems WHERE DraftId = $id;",
            "DELETE FROM DraftCheckpoints WHERE DraftId = $id;"
        })
        {
            using var cmd = db.Cmd(sql, tx).Bind("$id", draftId);
            cmd.ExecuteNonQuery();
        }
    }

    private static League? LoadLeagueByExternalId(
        SqliteConnection db,
        SqliteTransaction tx,
        FantasyPlatform platform,
        string externalLeagueId)
    {
        using var cmd = db.Cmd("""
            SELECT LeagueId FROM Leagues
            WHERE Platform = $p AND ExternalLeagueId = $ext
            LIMIT 1;
            """, tx)
            .Bind("$p", platform.ToString())
            .Bind("$ext", externalLeagueId);
        var id = cmd.ExecuteScalar() as string;
        return id is null ? null : LoadLeague(db, tx, LeagueId.Parse(id));
    }

    private static List<Draft> LoadDraftsForLeague(SqliteConnection db, SqliteTransaction tx, LeagueId leagueId)
    {
        using var cmd = db.Cmd("SELECT * FROM Drafts WHERE LeagueId = $id ORDER BY CreatedAt DESC;", tx)
            .Bind("$id", leagueId.ToString());
        using var reader = cmd.ExecuteReader();
        var list = new List<Draft>();
        while (reader.Read())
            list.Add(ReadDraft(reader));
        return list;
    }

    private static void UpdateImportedLeague(SqliteConnection db, SqliteTransaction tx, League league)
    {
        using var cmd = db.Cmd("""
            UPDATE Leagues
            SET Name = $name,
                Season = $season,
                TeamCount = $teams,
                DraftType = $type,
                RoundCount = $rounds,
                RosterSize = $roster,
                DraftSourcePreference = $pref,
                ExternalLeagueId = $ext,
                Platform = $platform,
                ArchivedAt = NULL
            WHERE LeagueId = $id;
            """, tx)
            .Bind("$name", league.Name)
            .Bind("$season", league.Season)
            .Bind("$teams", league.TeamCount)
            .Bind("$type", league.DraftType.ToString())
            .Bind("$rounds", league.RoundCount)
            .Bind("$roster", league.RosterSize)
            .Bind("$pref", league.DraftSourcePreference.ToString())
            .Bind("$ext", league.ExternalLeagueId)
            .Bind("$platform", league.Platform.ToString())
            .Bind("$id", league.LeagueId.ToString());
        cmd.ExecuteNonQuery();
    }

    private static void InsertLeague(SqliteConnection db, SqliteTransaction tx, League league)
    {
        using var cmd = db.Cmd("""
            INSERT INTO Leagues(LeagueId, Name, Platform, ExternalLeagueId, Season, TeamCount, UserTeamId, DraftType, RoundCount, RosterSize, DraftSourcePreference, CreatedAt)
            VALUES ($id, $name, $platform, $ext, $season, $teams, $user, $type, $rounds, $roster, $pref, $created);
            """, tx)
            .Bind("$id", league.LeagueId.ToString())
            .Bind("$name", league.Name)
            .Bind("$platform", league.Platform.ToString())
            .Bind("$ext", league.ExternalLeagueId)
            .Bind("$season", league.Season)
            .Bind("$teams", league.TeamCount)
            .Bind("$user", league.UserTeamId?.ToString())
            .Bind("$type", league.DraftType.ToString())
            .Bind("$rounds", league.RoundCount)
            .Bind("$roster", league.RosterSize)
            .Bind("$pref", league.DraftSourcePreference.ToString())
            .Bind("$created", league.CreatedAt.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    private static void InsertTeam(SqliteConnection db, SqliteTransaction tx, Team team)
    {
        using var cmd = db.Cmd("""
            INSERT INTO Teams(TeamId, LeagueId, Name, OwnerName, DisplayLabel, PortraitNotes, DraftPosition, ExternalTeamId, PracticePersonality, PortraitArtStyle)
            VALUES ($id, $league, $name, $owner, $label, $notes, $pos, $ext, $cpu, $art);
            """, tx)
            .Bind("$id", team.TeamId.ToString())
            .Bind("$league", team.LeagueId.ToString())
            .Bind("$name", team.Name)
            .Bind("$owner", team.OwnerName)
            .Bind("$label", team.DisplayLabel)
            .Bind("$notes", team.PortraitNotes)
            .Bind("$pos", team.DraftPosition)
            .Bind("$ext", team.ExternalTeamId)
            .Bind("$cpu", team.PracticePersonality?.ToString())
            .Bind("$art", team.PortraitArtStyle);
        cmd.ExecuteNonQuery();
    }

    private static void InsertRosterSlot(SqliteConnection db, SqliteTransaction tx, RosterSlot slot)
    {
        using (var cmd = db.Cmd("""
            INSERT INTO RosterSlots(RosterSlotId, LeagueId, SlotCode, SlotKind, Count)
            VALUES ($id, $league, $code, $kind, $count);
            """, tx)
                   .Bind("$id", slot.RosterSlotId.ToString())
                   .Bind("$league", slot.LeagueId.ToString())
                   .Bind("$code", slot.SlotCode)
                   .Bind("$kind", slot.SlotKind.ToString())
                   .Bind("$count", slot.Count))
        {
            cmd.ExecuteNonQuery();
        }

        foreach (var position in slot.EligiblePositions)
        {
            using var cmd = db.Cmd("""
                INSERT INTO RosterSlotEligiblePositions(RosterSlotId, Position) VALUES ($id, $p);
                """, tx)
                .Bind("$id", slot.RosterSlotId.ToString())
                .Bind("$p", position.ToString());
            cmd.ExecuteNonQuery();
        }
    }

    private static void InsertScoring(SqliteConnection db, SqliteTransaction tx, ScoringRule rule)
    {
        using var cmd = db.Cmd("""
            INSERT INTO ScoringRules(ScoringRuleId, LeagueId, Category, Points)
            VALUES ($id, $league, $cat, $pts);
            """, tx)
            .Bind("$id", rule.ScoringRuleId.ToString())
            .Bind("$league", rule.LeagueId.ToString())
            .Bind("$cat", rule.Category.ToString())
            .Bind("$pts", rule.Points.ToString(System.Globalization.CultureInfo.InvariantCulture));
        cmd.ExecuteNonQuery();
    }

    private void NotifyDrafts(IEnumerable<Draft> drafts)
    {
        foreach (var draft in drafts)
            notifier.Notify(draft.DraftId, draft.ActiveBranchId, draft.CurrentStateVersion);
    }

    private static List<Draft> SyncDraftSlotsToRoundCount(
        SqliteConnection db,
        SqliteTransaction tx,
        LeagueId leagueId,
        int roundCount)
    {
        var changed = new List<Draft>();
        var league = LoadLeague(db, tx, leagueId);
        if (league is null)
            return changed;

        var teams = LoadTeams(db, tx, leagueId);
        foreach (var draft in LoadDraftsForLeague(db, tx, leagueId))
        {
            if (draft.Status == DraftStatus.Completed)
                continue;
            if (AlignDraftSlots(db, tx, draft, league.DraftType, teams, roundCount))
                changed.Add(draft);
        }

        return changed;
    }

    private static bool AlignDraftSlots(
        SqliteConnection db,
        SqliteTransaction tx,
        Draft draft,
        DraftType draftType,
        IReadOnlyList<Team> teams,
        int roundCount)
    {
        var existing = LoadSlots(db, tx, draft.DraftId);
        var keepers = LoadKeepers(db, tx, draft.DraftId);
        var occupied = MaxOccupiedRound(db, tx, draft.DraftId, keepers);
        var target = Math.Max(Math.Max(1, roundCount), occupied);
        var ordered = SlotTeamOrder(existing, teams);
        if (ordered.Count == 0)
            return false;

        var generated = DraftSlotGenerator.Generate(draftType, ordered, target);
        var byOverall = existing.ToDictionary(slot => slot.OverallPick);
        var inserted = false;
        foreach (var slot in DraftSlotGenerator.ToDraftSlots(draft.DraftId, generated))
        {
            if (byOverall.ContainsKey(slot.OverallPick))
                continue;
            InsertSlot(db, tx, slot);
            inserted = true;
        }

        var extra = existing.Any(slot => slot.Round > target);
        if (extra)
        {
            using var cmd = db.Cmd("DELETE FROM DraftSlots WHERE DraftId = $id AND Round > $round;", tx)
                .Bind("$id", draft.DraftId.ToString())
                .Bind("$round", target);
            cmd.ExecuteNonQuery();
        }

        return inserted || extra;
    }

    private static int MaxOccupiedRound(
        SqliteConnection db,
        SqliteTransaction tx,
        DraftId draftId,
        IReadOnlyList<Keeper> keepers)
    {
        var max = keepers.Count == 0 ? 0 : keepers.Max(keeper => keeper.RoundCost);
        using var cmd = db.Cmd("SELECT MAX(Round) FROM ActiveDraftSelections WHERE DraftId = $id;", tx)
            .Bind("$id", draftId.ToString());
        var value = cmd.ExecuteScalar();
        if (value is not null and not DBNull)
            max = Math.Max(max, Convert.ToInt32(value));
        return max;
    }

    private static List<TeamDraftPosition> SlotTeamOrder(IReadOnlyList<DraftSlot> slots, IReadOnlyList<Team> teams)
    {
        var byId = teams.ToDictionary(team => team.TeamId);
        var roundOne = slots.Where(slot => slot.Round == 1).OrderBy(slot => slot.RoundPick).ToList();
        if (roundOne.Count > 0)
        {
            return roundOne.Select(slot =>
            {
                byId.TryGetValue(slot.TeamId, out var team);
                return new TeamDraftPosition
                {
                    TeamId = slot.TeamId,
                    Name = team?.Name ?? $"Team {slot.RoundPick}",
                    DraftPosition = slot.RoundPick
                };
            }).ToList();
        }

        return teams
            .OrderBy(team => team.DraftPosition)
            .Select(team => new TeamDraftPosition
            {
                TeamId = team.TeamId,
                Name = team.Name,
                DraftPosition = team.DraftPosition
            })
            .ToList();
    }

    private static void ReplaceSlots(
        SqliteConnection db,
        SqliteTransaction tx,
        DraftId draftId,
        DraftType draftType,
        IReadOnlyList<TeamDraftPosition> teams,
        int roundCount)
    {
        var keepers = LoadKeepers(db, tx, draftId);
        using (var cmd = db.Cmd("DELETE FROM DraftSlots WHERE DraftId = $id;", tx)
                   .Bind("$id", draftId.ToString()))
        {
            cmd.ExecuteNonQuery();
        }

        var slots = DraftSlotGenerator.ToDraftSlots(draftId, DraftSlotGenerator.Generate(draftType, teams, roundCount));
        foreach (var slot in slots)
            InsertSlot(db, tx, slot);

        foreach (var keeper in keepers)
        {
            keeper.DraftSlotId = null;
            var slot = KeeperRules.ResolveSlot(slots, keeper);
            if (slot is not null)
            {
                keeper.DraftSlotId = slot.DraftSlotId;
                slot.IsKeeperSlot = true;
                using var mark = db.Cmd("UPDATE DraftSlots SET IsKeeperSlot = 1 WHERE DraftSlotId = $id;", tx)
                    .Bind("$id", slot.DraftSlotId.ToString());
                mark.ExecuteNonQuery();
            }

            using var update = db.Cmd("UPDATE Keepers SET DraftSlotId = $slot WHERE KeeperId = $id;", tx)
                .Bind("$slot", keeper.DraftSlotId?.ToString())
                .Bind("$id", keeper.KeeperId.ToString());
            update.ExecuteNonQuery();
        }
    }

    private static void InsertSlot(SqliteConnection db, SqliteTransaction tx, DraftSlot slot)
    {
        using var cmd = db.Cmd("""
            INSERT INTO DraftSlots(DraftSlotId, DraftId, OverallPick, Round, RoundPick, TeamId, IsKeeperSlot)
            VALUES ($id, $draft, $overall, $round, $rp, $team, $keeper);
            """, tx)
            .Bind("$id", slot.DraftSlotId.ToString())
            .Bind("$draft", slot.DraftId.ToString())
            .Bind("$overall", slot.OverallPick)
            .Bind("$round", slot.Round)
            .Bind("$rp", slot.RoundPick)
            .Bind("$team", slot.TeamId.ToString())
            .Bind("$keeper", slot.IsKeeperSlot ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    private static void InsertKeeper(SqliteConnection db, SqliteTransaction tx, Keeper keeper)
    {
        using var cmd = db.Cmd("""
            INSERT INTO Keepers(KeeperId, DraftId, TeamId, PlayerId, RoundCost, DraftSlotId, Notes)
            VALUES ($id, $draft, $team, $player, $round, $slot, $notes);
            """, tx)
            .Bind("$id", keeper.KeeperId.ToString())
            .Bind("$draft", keeper.DraftId.ToString())
            .Bind("$team", keeper.TeamId.ToString())
            .Bind("$player", keeper.PlayerId.ToString())
            .Bind("$round", keeper.RoundCost)
            .Bind("$slot", keeper.DraftSlotId?.ToString())
            .Bind("$notes", keeper.Notes);
        cmd.ExecuteNonQuery();
    }
}
