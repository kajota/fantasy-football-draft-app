using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.Core.Engine;

public static class LeagueFactory
{
    public static DraftWorkingState CreateStandardState(
        int teamCount = 12,
        int roundCount = 16,
        DraftType draftType = DraftType.Snake,
        int userDraftPosition = 4,
        bool superflex = true)
    {
        var leagueId = LeagueId.New();
        var teams = new List<Team>();
        var positions = new List<TeamDraftPosition>();
        for (var i = 1; i <= teamCount; i++)
        {
            var team = new Team
            {
                TeamId = TeamId.New(),
                LeagueId = leagueId,
                Name = i == userDraftPosition ? "My Team" : $"Team {i}",
                OwnerName = i == userDraftPosition ? "You" : $"Owner {i}",
                DraftPosition = i
            };
            teams.Add(team);
            positions.Add(new TeamDraftPosition
            {
                TeamId = team.TeamId,
                Name = team.Name,
                OwnerName = team.OwnerName,
                DraftPosition = i
            });
        }

        var userTeam = teams[userDraftPosition - 1];
        var league = new League
        {
            LeagueId = leagueId,
            Name = superflex ? "Superflex League" : "Standard League",
            Season = 2026,
            TeamCount = teamCount,
            UserTeamId = userTeam.TeamId,
            DraftType = draftType,
            RoundCount = roundCount,
            RosterSize = superflex ? 16 : 15
        };

        var roster = RosterRules.Preset(superflex)
            .Select(s => new RosterSlot
            {
                RosterSlotId = RosterSlotId.New(),
                LeagueId = leagueId,
                SlotCode = s.SlotCode,
                SlotKind = s.SlotKind,
                Count = s.Count,
                EligiblePositions = s.EligiblePositions
            })
            .ToList();

        var scoring = RosterRules.DefaultScoring().Select(s => new ScoringRule
        {
            ScoringRuleId = ScoringRuleId.New(),
            LeagueId = leagueId,
            Category = s.Category,
            Points = s.Points
        }).ToList();

        var draftId = DraftId.New();
        var branchId = BranchId.New();
        var generated = DraftSlotGenerator.Generate(draftType, positions, roundCount);
        var slots = DraftSlotGenerator.ToDraftSlots(draftId, generated);

        var draft = new Draft
        {
            DraftId = draftId,
            LeagueId = leagueId,
            Name = "Main Draft",
            Season = 2026,
            Status = DraftStatus.NotStarted,
            ActiveBranchId = branchId
        };

        var branch = new DraftBranch
        {
            BranchId = branchId,
            DraftId = draftId,
            Name = "Main Draft"
        };

        return new DraftWorkingState
        {
            League = league,
            Draft = draft,
            ActiveBranch = branch,
            Teams = teams,
            RosterSlots = roster,
            ScoringRules = scoring,
            Slots = slots,
            Keepers = []
        };
    }
}
