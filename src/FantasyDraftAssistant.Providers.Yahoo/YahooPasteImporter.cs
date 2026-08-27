using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Yahoo;

namespace FantasyDraftAssistant.Providers.Yahoo;

/// The no-API-key import path. Mirrors <see cref="YahooLeagueImporter"/> but sources
/// its <see cref="YahooLeagueSnapshot"/> from pasted text instead of OAuth + XML, so
/// the mapper, the upsert-by-external-id behaviour, and the review items are shared.
public sealed class YahooPasteImporter(
    ILeagueService leagues,
    YahooPasteAiReader aiReader) : IYahooPasteImporter
{
    public YahooPasteParseResult Parse(YahooPasteInput input) => YahooPasteParser.Parse(input);

    public Task<IReadOnlyList<YahooAiReaderOption>> ListAiReadersAsync(CancellationToken cancellationToken = default) =>
        aiReader.ListAvailableAsync(cancellationToken);

    public Task<YahooPasteParseResult> ParseWithAiAsync(
        YahooPasteInput input,
        string providerKey,
        CancellationToken cancellationToken = default) =>
        aiReader.ReadAsync(input, providerKey, cancellationToken);

    public YahooImportPreview Preview(YahooLeagueSnapshot snapshot) =>
        new()
        {
            Mapped = YahooLeagueMapper.Map(snapshot),
            Snapshot = snapshot
        };

    public async Task<YahooImportPreview> PreviewAsync(YahooLeagueSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
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
        YahooLeagueSnapshot snapshot,
        YahooImportOptions options,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(snapshot);
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
}
