using FantasyDraftAssistant.App.ViewModels;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;

namespace FantasyDraftAssistant.App.Tests;

public class SessionDraftTests
{
    [Fact]
    public void SelectDraft_prefers_newest_draft_even_if_an_older_one_is_still_InProgress()
    {
        var leagueId = LeagueId.New();
        var abandoned = new Draft
        {
            DraftId = DraftId.New(),
            LeagueId = leagueId,
            Name = "Abandoned early attempt",
            Status = DraftStatus.InProgress,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-14)
        };
        var real = new Draft
        {
            DraftId = DraftId.New(),
            LeagueId = leagueId,
            Name = "The real draft",
            Status = DraftStatus.Completed,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-3)
        };

        var selected = SessionDraft.SelectDraft(preferred: null, drafts: [abandoned, real]);

        Assert.Equal(real.DraftId, selected!.DraftId);
    }

    [Fact]
    public void SelectDraft_honors_an_explicit_preference_when_it_still_exists()
    {
        var leagueId = LeagueId.New();
        var older = new Draft
        {
            DraftId = DraftId.New(),
            LeagueId = leagueId,
            Name = "Older",
            Status = DraftStatus.Completed,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-10)
        };
        var newer = new Draft
        {
            DraftId = DraftId.New(),
            LeagueId = leagueId,
            Name = "Newer",
            Status = DraftStatus.Completed,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };

        var selected = SessionDraft.SelectDraft(preferred: older.DraftId, drafts: [older, newer]);

        Assert.Equal(older.DraftId, selected!.DraftId);
    }
}
