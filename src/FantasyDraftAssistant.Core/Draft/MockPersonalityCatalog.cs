using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;

namespace FantasyDraftAssistant.Core.Engine;

public static class MockPersonalityCatalog
{
    // MockPersonality.Ai is intentionally not here. "Random" in the UI means
    // "draw from this bag", so leaving it out is what keeps an AI seat something
    // you opt into rather than something you get handed.
    public static readonly MockPersonality[] DealBag =
    [
        MockPersonality.BestAvailable,
        MockPersonality.BestAvailable,
        MockPersonality.RbFirst,
        MockPersonality.HeroRb,
        MockPersonality.ZeroRb,
        MockPersonality.WrHeavy,
        MockPersonality.QbEarly,
        MockPersonality.LateQb,
        MockPersonality.RookieHunter,
        MockPersonality.AdpHunter
    ];

    public static string Title(MockPersonality personality) => personality switch
    {
        MockPersonality.BestAvailable => "Best available",
        MockPersonality.RbFirst => "RB first",
        MockPersonality.HeroRb => "Hero RB",
        MockPersonality.ZeroRb => "Zero RB",
        MockPersonality.WrHeavy => "WR heavy",
        MockPersonality.QbEarly => "QB early",
        MockPersonality.LateQb => "Late QB",
        MockPersonality.RookieHunter => "Rookie hunter",
        MockPersonality.AdpHunter => "Chases ADP",
        MockPersonality.Ai => "AI drafter",
        _ => personality.ToString()
    };

    public static string Blurb(MockPersonality personality) => personality switch
    {
        MockPersonality.BestAvailable => "Takes the highest-ranked player, with a light nod to open starters.",
        MockPersonality.RbFirst => "Loads running backs early, then fills the rest.",
        MockPersonality.HeroRb => "Grabs one early RB, then waits on the position.",
        MockPersonality.ZeroRb => "Avoids running backs until the middle rounds.",
        MockPersonality.WrHeavy => "Stacks receivers early.",
        MockPersonality.QbEarly => "Takes a quarterback early, especially in Superflex.",
        MockPersonality.LateQb => "Waits on quarterback unless the well is dry.",
        MockPersonality.RookieHunter => "Boosts true rookies.",
        MockPersonality.AdpHunter => "Follows ADP more than expert rank.",
        MockPersonality.Ai => "An AI model drafts this seat to a strategy it keeps to itself.",
        _ => ""
    };

    public static IReadOnlyList<MockPersonality> Deal(int cpuCount, int seed)
    {
        if (cpuCount <= 0)
            return [];

        var rng = new Random(seed);
        var bag = DealBag.ToList();
        var dealt = new List<MockPersonality>(cpuCount);
        while (dealt.Count < cpuCount)
        {
            foreach (var personality in bag.OrderBy(_ => rng.Next()))
            {
                dealt.Add(personality);
                if (dealt.Count == cpuCount)
                    break;
            }
        }

        return dealt;
    }

    public static IReadOnlyList<MockSeatPolicy> Assign(
        IReadOnlyList<Team> teams,
        TeamId? userTeamId,
        int seed,
        DraftId? draftId = null,
        BranchId? branchId = null)
    {
        // Teams with a saved practice personality keep it; only the rest of
        // the CPU seats draw from the random deal.
        var randomCpu = teams
            .Where(team => userTeamId is not { } user || !team.TeamId.Equals(user))
            .Count(team => team.PracticePersonality is null);
        var personalities = Deal(randomCpu, seed);
        var policies = new List<MockSeatPolicy>(teams.Count);
        var index = 0;
        foreach (var team in teams.OrderBy(item => item.DraftPosition))
        {
            var isUser = userTeamId is { } user && team.TeamId.Equals(user);
            var personality = isUser
                ? team.PracticePersonality ?? MockPersonality.BestAvailable
                : team.PracticePersonality ?? personalities[index++];

            // The seat's model and strategy are pinned here rather than read live from
            // the team, so a branch keeps drafting the way it started even if the team
            // is edited later.
            string? strategyKey = null;
            if (personality == MockPersonality.Ai && draftId is { } draft && branchId is { } branch)
                strategyKey = MockAiStrategyCatalog.ForSeat(draft, branch, team.TeamId).Key;

            policies.Add(new MockSeatPolicy
            {
                TeamId = team.TeamId,
                Personality = personality,
                IsCpu = !isUser,
                AiModel = personality == MockPersonality.Ai ? team.PracticeAiModel : null,
                AiStrategy = strategyKey
            });
        }

        return policies;
    }

    public static string LabelWithPersonality(string teamLabel, MockSeatPolicy? policy)
    {
        if (policy is null || !policy.IsCpu)
            return teamLabel;
        return $"{teamLabel} · {Title(policy.Personality)}";
    }
}
