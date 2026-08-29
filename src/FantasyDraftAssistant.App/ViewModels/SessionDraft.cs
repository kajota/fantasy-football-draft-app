using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Models;

namespace FantasyDraftAssistant.App.ViewModels;

public static class SessionDraft
{
    public static async Task<Draft?> AttachLeagueAsync(
        SessionState session,
        ILeagueService leagues,
        LeagueId leagueId,
        string? leagueName = null,
        CancellationToken cancellationToken = default)
    {
        var league = await leagues.GetLeagueAsync(leagueId, cancellationToken);
        var drafts = await leagues.ListDraftsAsync(leagueId, cancellationToken);
        var draft = SelectDraft(session.DraftId, drafts);
        session.LeagueId = leagueId;
        session.LeagueName = string.IsNullOrWhiteSpace(leagueName) ? league?.Name : leagueName;
        BindDraft(session, draft);
        return draft;
    }

    public static async Task<Draft?> EnsureDraftAsync(
        SessionState session,
        ILeagueService leagues,
        IDraftCommandService commands,
        bool startIfNeeded,
        CancellationToken cancellationToken = default)
    {
        if (session.LeagueId is not { } leagueId)
            return null;

        var draft = await AttachLeagueAsync(session, leagues, leagueId, session.LeagueName, cancellationToken);
        if (draft is null)
        {
            draft = await leagues.CreateDraftAsync(new CreateDraftRequest
            {
                LeagueId = leagueId,
                Name = $"{session.LeagueName ?? "League"} Draft"
            }, cancellationToken);
            BindDraft(session, draft);
        }

        if (startIfNeeded && draft.Status == DraftStatus.NotStarted)
        {
            var started = await commands.StartDraftAsync(new StartDraftCommand(draft.DraftId), cancellationToken);
            if (started.Succeeded)
                draft = await leagues.GetDraftAsync(draft.DraftId, cancellationToken) ?? draft;
            BindDraft(session, draft);
        }
        else if (session.BranchId is not { } branch
                 || (await leagues.GetBranchesAsync(draft.DraftId, cancellationToken))
                     .All(item => !item.BranchId.Equals(branch)))
        {
            session.BranchId = draft.ActiveBranchId;
        }

        return draft;
    }

    public static void BindDraft(SessionState session, Draft? draft)
    {
        session.DraftId = draft?.DraftId;
        session.DraftName = draft?.Name;
        session.BranchId = draft?.ActiveBranchId;
    }

    public static Draft? SelectDraft(DraftId? preferred, IReadOnlyList<Draft> drafts)
    {
        if (preferred is { } id)
        {
            var match = drafts.FirstOrDefault(draft => draft.DraftId.Equals(id));
            if (match is not null)
                return match;
        }

        // Newest wins. An old abandoned draft left sitting at InProgress must never
        // outrank the real, newer draft just because "InProgress" sounds more current.
        return drafts
            .OrderByDescending(draft => draft.CreatedAt)
            .FirstOrDefault();
    }
}
