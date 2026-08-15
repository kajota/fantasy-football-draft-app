using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Yahoo;

namespace FantasyDraftAssistant.Providers.Yahoo;

public sealed class YahooLeagueImporter(
    YahooAuthService auth,
    YahooFantasyClient api,
    ILeagueService leagues) : IYahooLeagueImporter
{
    public async Task<IReadOnlyList<YahooLeagueListItem>> ListLeaguesAsync(CancellationToken cancellationToken = default)
    {
        var token = await auth.GetAccessTokenAsync(cancellationToken);
        var xml = await api.GetUserLeaguesXmlAsync(token, cancellationToken);
        var snapshots = YahooFantasyXml.ParseLeagueList(xml);
        var items = new List<YahooLeagueListItem>();
        foreach (var snapshot in snapshots)
        {
            var existing = await leagues.FindByExternalIdAsync(FantasyPlatform.Yahoo, snapshot.LeagueKey, cancellationToken);
            items.Add(new YahooLeagueListItem
            {
                LeagueKey = snapshot.LeagueKey,
                Name = snapshot.Name,
                Season = snapshot.Season,
                TeamCount = snapshot.TeamCount,
                IsAuction = snapshot.IsAuction,
                IsKeeper = snapshot.IsKeeper,
                AlreadyImported = existing is not null
            });
        }

        return items
            .OrderByDescending(item => item.Season)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<YahooImportPreview> PreviewAsync(string leagueKey, CancellationToken cancellationToken = default)
    {
        var snapshot = await LoadSnapshotAsync(leagueKey, cancellationToken);
        var mapped = YahooLeagueMapper.Map(snapshot);
        var existing = await leagues.FindByExternalIdAsync(FantasyPlatform.Yahoo, snapshot.LeagueKey, cancellationToken);
        return new YahooImportPreview
        {
            Mapped = mapped,
            Snapshot = snapshot,
            ExistingLeagueId = existing?.LeagueId,
            ExistingIsArchived = existing?.ArchivedAt is not null
        };
    }

    public async Task<YahooImportResult> ImportAsync(
        string leagueKey,
        YahooImportOptions options,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshot = await LoadSnapshotAsync(leagueKey, cancellationToken);
            var mapped = YahooLeagueMapper.Map(snapshot, options);
            var existing = await leagues.FindByExternalIdAsync(FantasyPlatform.Yahoo, snapshot.LeagueKey, cancellationToken);
            var request = mapped.Request with { ReplaceDraftOrder = options.ReplaceDraftOrder };
            var league = await leagues.UpsertImportedLeagueAsync(request, cancellationToken);
            return new YahooImportResult
            {
                Succeeded = true,
                LeagueId = league.LeagueId,
                LeagueName = league.Name,
                CreatedNew = existing is null,
                ReviewItems = mapped.ReviewItems
            };
        }
        catch (Exception ex)
        {
            return new YahooImportResult
            {
                Succeeded = false,
                Error = ex.Message
            };
        }
    }

    private async Task<YahooLeagueSnapshot> LoadSnapshotAsync(string leagueKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(leagueKey))
            throw new InvalidOperationException("Choose a Yahoo league first.");

        var token = await auth.GetAccessTokenAsync(cancellationToken);
        var settingsXml = await api.GetLeagueSettingsXmlAsync(token, leagueKey.Trim(), cancellationToken);
        var teamsXml = await api.GetLeagueTeamsXmlAsync(token, leagueKey.Trim(), cancellationToken);
        return YahooFantasyXml.Merge(
            YahooFantasyXml.ParseLeague(settingsXml),
            YahooFantasyXml.ParseLeague(teamsXml));
    }
}
