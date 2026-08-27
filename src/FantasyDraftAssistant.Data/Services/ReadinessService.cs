using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Results;
using FantasyDraftAssistant.Data.Database;

namespace FantasyDraftAssistant.Data.Services;

public sealed class ReadinessService(
    ILeagueService leagues,
    IDraftStateService drafts,
    IFantasyDataWriter fantasyData,
    IBackupService backups,
    IAiConfigStore aiConfigs,
    ICredentialStore credentials,
    AppPaths paths,
    DataLockService dataLock,
    IBoardPublisher publisher) : IReadinessService
{
    public async Task<IReadOnlyList<ReadinessItem>> CheckAsync(DraftId? draftId, CancellationToken cancellationToken = default)
    {
        var items = new List<ReadinessItem>();
        items.Add(Item("Data directory", ReadinessLevel.Ready, paths.Root, false));
        items.Add(LockItem(dataLock, paths.LockFilePath));

        if (draftId is { } id)
        {
            var draft = await leagues.GetDraftAsync(id, cancellationToken);
            if (draft is null)
            {
                items.Add(Item("League configuration", ReadinessLevel.Failed, "Draft was not found.", true));
            }
            else
            {
                var league = await leagues.GetLeagueAsync(draft.LeagueId, cancellationToken);
                var teams = league is null ? [] : await leagues.GetTeamsAsync(league.LeagueId, cancellationToken);
                var keepers = await leagues.GetKeepersAsync(id, cancellationToken);
                items.Add(Item("League configuration",
                    league is null || teams.Count == 0 ? ReadinessLevel.Failed : ReadinessLevel.Ready,
                    league is null ? "League missing." : $"{teams.Count} teams, {league.RoundCount} rounds.",
                    true));
                items.Add(Item("Draft order",
                    teams.Count == 0 ? ReadinessLevel.Failed : ReadinessLevel.Ready,
                    teams.Count == 0 ? "No teams configured." : "Draft slots generated.",
                    true));
                items.Add(Item("Keepers",
                    keepers.Count == 0 ? ReadinessLevel.Optional : ReadinessLevel.Ready,
                    keepers.Count == 0 ? "No keepers assigned." : $"{keepers.Count} keeper(s) assigned.",
                    false));

                var integrity = await drafts.ValidateAsync(id, cancellationToken);
                items.Add(Item("Active draft database",
                    integrity.IsHealthy ? ReadinessLevel.Ready : ReadinessLevel.Failed,
                    integrity.IsHealthy ? "Healthy." : string.Join(" ", integrity.Issues),
                    true));
            }
        }
        else
        {
            items.Add(Item("League configuration", ReadinessLevel.Attention, "No draft selected.", false));
        }

        var players = await drafts.GetPlayersAsync(cancellationToken);
        items.Add(Item("Local player database",
            players.Count == 0 ? ReadinessLevel.Failed : ReadinessLevel.Ready,
            players.Count == 0 ? "Missing." : $"{players.Count} players cached.",
            true));

        var refreshes = await fantasyData.GetRefreshInfoAsync(cancellationToken);
        var fantasyProsKey = await credentials.GetSecretAsync("fantasydata", "fantasypros", cancellationToken);
        items.Add(Item("FantasyPros",
            !string.IsNullOrWhiteSpace(fantasyProsKey) ? ReadinessLevel.Ready : ReadinessLevel.Optional,
            !string.IsNullOrWhiteSpace(fantasyProsKey)
                ? "API key saved. Refresh on Player Data to update ranks."
                : "Optional. Add a key on Player Data for expert ranks and tiers.",
            false));
        items.Add(Freshness("Rankings", refreshes.FirstOrDefault(r => r.Dataset == "rankings"), required: true));
        items.Add(Freshness("ADP", refreshes.FirstOrDefault(r => r.Dataset == "adp"), required: true));
        items.Add(Freshness("Projections", refreshes.FirstOrDefault(r => r.Dataset == "projections"), required: false));
        var statusStamp = players.Select(p => p.StatusUpdatedAt).Where(t => t.HasValue).Select(t => t!.Value).DefaultIfEmpty().Max();
        items.Add(Item("Player status",
            statusStamp == default ? ReadinessLevel.Attention : AgeLevel(statusStamp, 2, 7, required: false),
            statusStamp == default ? "No status timestamp." : $"Cached, last updated {statusStamp:g}.",
            false));

        var yahooAuth = await credentials.GetSecretAsync("yahoo", "refresh_token", cancellationToken)
                        ?? await credentials.GetSecretAsync("yahoo", "access_token", cancellationToken);
        var yahooApp = await credentials.GetSecretAsync("yahoo", "client_id", cancellationToken);
        items.Add(Item("Yahoo authentication",
            !string.IsNullOrWhiteSpace(yahooAuth) ? ReadinessLevel.Ready
                : !string.IsNullOrWhiteSpace(yahooApp) ? ReadinessLevel.Attention
                : ReadinessLevel.Optional,
            !string.IsNullOrWhiteSpace(yahooAuth) ? "Yahoo account is signed in."
                : !string.IsNullOrWhiteSpace(yahooApp) ? "App credentials saved. Sign in on the Yahoo page."
                : "Optional until you import a Yahoo league.",
            false));

        var currentLeague = draftId is { } selectedDraft
            ? await leagues.GetDraftAsync(selectedDraft, cancellationToken) is { } selected
                ? await leagues.GetLeagueAsync(selected.LeagueId, cancellationToken)
                : null
            : null;
        items.Add(Item("Yahoo league access",
            currentLeague?.Platform == FantasyPlatform.Yahoo ? ReadinessLevel.Ready : ReadinessLevel.Optional,
            currentLeague?.Platform == FantasyPlatform.Yahoo
                ? $"Local league linked to {currentLeague.ExternalLeagueId}."
                : "No Yahoo-imported league is selected.",
            false));
        items.Add(Item("Yahoo draft access",
            ReadinessLevel.Optional,
            "Live Yahoo pick sync is not in this slice. Manual entry stays available.",
            false));

        var configs = await aiConfigs.ListAsync(cancellationToken);
        foreach (var descriptor in Core.Ai.AiProviderCatalog.All)
        {
            var config = configs.FirstOrDefault(c => c.ProviderKey == descriptor.ProviderKey);
            var key = await credentials.GetSecretAsync("ai", descriptor.ProviderKey, cancellationToken);
            items.Add(Item(descriptor.ProductName,
                config is null || !config.Enabled ? ReadinessLevel.Disabled :
                string.IsNullOrWhiteSpace(key) ? ReadinessLevel.Failed : ReadinessLevel.Ready,
                config is null || !config.Enabled ? "Disabled." :
                string.IsNullOrWhiteSpace(key) ? "API key missing." : $"Model {config.Model}.",
                false));
        }

        var backupList = await backups.ListBackupsAsync(cancellationToken);
        items.Add(Item("Latest backup",
            backupList.Count == 0 ? ReadinessLevel.Attention : ReadinessLevel.Ready,
            backupList.Count == 0 ? "No backups yet." : backupList[0].CreatedAt.ToString("g"),
            false));
        items.Add(Item("Manual fallback", ReadinessLevel.Ready, "Manual draft entry is available.", true));
        items.Add(Item("Credential store",
            credentials.IsSecure ? ReadinessLevel.Ready : ReadinessLevel.Attention,
            credentials.Description,
            false));
        var publishToken = await credentials.GetSecretAsync(BoardSlug.CredentialScope, BoardSlug.CredentialKey, cancellationToken);
        items.Add(Item("Web draft board",
            string.IsNullOrWhiteSpace(publishToken) ? ReadinessLevel.Optional : ReadinessLevel.Ready,
            string.IsNullOrWhiteSpace(publishToken)
                ? "No bearer token. Save one on Player Data if you want the TV board."
                : publisher.LastStatus,
            false));

        return items;
    }

    private static ReadinessItem Freshness(string name, FantasyDraftAssistant.Core.Models.FantasyDataRefreshInfo? info, bool required)
    {
        if (info is null)
            return Item(name, required ? ReadinessLevel.Failed : ReadinessLevel.Optional, "Missing.", required);
        return Item(name, AgeLevel(info.RefreshedAt, 14, 30, required), $"Cached {info.RecordCount} rows at {info.RefreshedAt:g}.", required);
    }

    private static ReadinessLevel AgeLevel(DateTimeOffset stamp, int attentionDays, int failedDays, bool required)
    {
        var age = DateTimeOffset.UtcNow - stamp;
        if (age.TotalDays > failedDays)
            return required ? ReadinessLevel.Failed : ReadinessLevel.Attention;
        if (age.TotalDays > attentionDays)
            return ReadinessLevel.Attention;
        return ReadinessLevel.Ready;
    }

    private static ReadinessItem LockItem(DataLockService dataLock, string lockPath)
    {
        if (dataLock.WasStolen)
        {
            return Item(
                "Data lock",
                ReadinessLevel.Attention,
                "Another session took over this database. Close this copy so you do not write from two computers at once.",
                false);
        }

        if (dataLock.Current is { } current)
        {
            return Item(
                "Data lock",
                ReadinessLevel.Ready,
                $"Held on {current.Host} since {current.AcquiredAt.ToLocalTime():g}. {lockPath}",
                false);
        }

        return Item("Data lock", ReadinessLevel.Attention, "Not acquired.", false);
    }

    private static ReadinessItem Item(string name, ReadinessLevel level, string detail, bool critical) => new()
    {
        Name = name,
        Level = level,
        Detail = detail,
        IsCritical = critical && level == ReadinessLevel.Failed
    };
}
