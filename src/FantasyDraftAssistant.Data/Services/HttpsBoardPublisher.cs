using System.Net.Http.Headers;
using System.Reflection;
using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Query;
using FantasyDraftAssistant.Core.Serialization;

namespace FantasyDraftAssistant.Data.Services;

public sealed class HttpsBoardPublisher : IBoardPublisher, IDisposable
{
    public const string HttpClientName = "board-publish";

    // Once a publish fails (as opposed to being skipped because publishing isn't
    // configured), keep retrying on a backoff until it succeeds or a newer pick
    // supersedes it. Without this, a pick made while offline that outlives its
    // window never reaches the web board unless something else happens to
    // trigger another publish after connectivity returns.
    private static readonly IReadOnlyList<TimeSpan> DefaultRetryDelays =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(20),
        TimeSpan.FromSeconds(30)
    ];

    private static readonly IReadOnlyDictionary<string, string> Pages = LoadPages();
    private readonly HttpClient _http;
    private readonly IDraftStateService _drafts;
    private readonly IDraftQueryService _queries;
    private readonly IFantasyDataWriter _fantasyData;
    private readonly IAppSettingsStore _settings;
    private readonly ICredentialStore _credentials;
    private readonly TimeProvider _clock;
    private readonly IReadOnlyList<TimeSpan> _retryDelays;
    private readonly object _retryLock = new();
    private int _epoch;
    private string _lastStatus = "Not published yet.";
    private CancellationTokenSource? _retryCts;

    public HttpsBoardPublisher(
        HttpClient http,
        IDraftStateService drafts,
        IDraftQueryService queries,
        IFantasyDataWriter fantasyData,
        IAppSettingsStore settings,
        ICredentialStore credentials,
        IDraftChangeNotifier notifier,
        TimeProvider? clock = null,
        IReadOnlyList<TimeSpan>? retryDelays = null)
    {
        _http = http;
        _drafts = drafts;
        _queries = queries;
        _fantasyData = fantasyData;
        _settings = settings;
        _credentials = credentials;
        _clock = clock ?? TimeProvider.System;
        _retryDelays = retryDelays ?? DefaultRetryDelays;
        notifier.DraftChanged += (_, args) => Schedule(args.DraftId, args.BranchId);
    }

    public string LastStatus => _lastStatus;
    public event EventHandler? StatusChanged;

    public void Schedule(DraftId draftId, BranchId branchId)
    {
        var epoch = Interlocked.Increment(ref _epoch);
        _ = DebouncedAsync(draftId, branchId, epoch);
    }

    public async Task PublishNowAsync(DraftId draftId, BranchId? branchId = null, CancellationToken cancellationToken = default)
    {
        var epoch = Interlocked.Increment(ref _epoch);
        var outcome = await PublishCoreAsync(draftId, branchId, cancellationToken).ConfigureAwait(false);
        if (outcome == PublishOutcome.Failed)
            StartRetryLoop(draftId, branchId, epoch);
    }

    private async Task DebouncedAsync(DraftId draftId, BranchId branchId, int epoch)
    {
        try
        {
            await Task.Delay(250).ConfigureAwait(false);
            if (epoch != Volatile.Read(ref _epoch))
                return;
            var outcome = await PublishCoreAsync(draftId, branchId).ConfigureAwait(false);
            if (outcome == PublishOutcome.Failed)
                StartRetryLoop(draftId, branchId, epoch);
        }
        catch
        {
            // Never throw out of the notifier.
        }
    }

    // A pick made while offline is superseded the moment a newer pick (or a
    // manual publish) bumps the epoch — that newer attempt reads fresh full
    // state, so it already carries everything this loop was trying to send.
    private void StartRetryLoop(DraftId draftId, BranchId? branchId, int epoch)
    {
        CancellationTokenSource cts;
        lock (_retryLock)
        {
            _retryCts?.Cancel();
            _retryCts?.Dispose();
            cts = new CancellationTokenSource();
            _retryCts = cts;
        }

        _ = RetryLoopAsync(draftId, branchId, epoch, cts.Token);
    }

    private async Task RetryLoopAsync(DraftId draftId, BranchId? branchId, int epoch, CancellationToken cancellationToken)
    {
        try
        {
            var attempt = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                if (epoch != Volatile.Read(ref _epoch))
                    return;

                var delay = _retryDelays[Math.Min(attempt, _retryDelays.Count - 1)];
                attempt++;
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

                if (epoch != Volatile.Read(ref _epoch))
                    return;

                var outcome = await PublishCoreAsync(draftId, branchId, cancellationToken).ConfigureAwait(false);
                if (outcome != PublishOutcome.Failed)
                    return;
            }
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer publish, or disposed. Nothing left to do.
        }
    }

    private enum PublishOutcome { Success, Skipped, Failed }

    private async Task<PublishOutcome> PublishCoreAsync(DraftId draftId, BranchId? branchId, CancellationToken cancellationToken = default)
    {
        try
        {
            var state = await _drafts.GetWorkingStateAsync(draftId, branchId, cancellationToken).ConfigureAwait(false);
            if (state is null)
            {
                SetStatus("Draft was not found.");
                return PublishOutcome.Skipped;
            }

            var league = state.League;
            if (!league.PublishBoard)
                return PublishOutcome.Skipped;

            if (!BoardSlug.TryNormalize(league.BoardSlug, out var slug))
            {
                SetStatus("Publishing is on, but this league has no valid web board slug.");
                return PublishOutcome.Skipped;
            }

            var token = await _credentials.GetSecretAsync(BoardSlug.CredentialScope, BoardSlug.CredentialKey, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(token))
            {
                SetStatus("Publishing is on, but no bearer token is saved.");
                return PublishOutcome.Skipped;
            }

            var baseUrl = await _settings.GetAsync(BoardSlug.BaseUrlSettingKey, cancellationToken).ConfigureAwait(false);
            var folder = BoardSlug.PublicUrl(baseUrl, slug);
            if (folder is null)
            {
                SetStatus("Could not build the publish URL.");
                return PublishOutcome.Skipped;
            }

            var players = await _drafts.GetPlayersAsync(cancellationToken).ConfigureAwait(false);
            var now = _clock.GetUtcNow();
            var adp = await _fantasyData.GetAdpAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            var snapshot = BoardSnapshotBuilder.Build(state, players, now, adp);
            var remaining = await _queries.GetRemainingPlayersAsync(new QueryContext
            {
                DraftId = draftId,
                BranchId = state.ActiveBranch.BranchId
            }, cancellationToken).ConfigureAwait(false);
            remaining = new RemainingPlayersSnapshot
            {
                UpdatedAt = now,
                League = remaining.League,
                Practice = remaining.Practice,
                Players = remaining.Players
            };

            var root = new Uri(folder);
            await PutAsync(new Uri(root, "index.html"), Stamp(Pages["board.html"]), "text/html", token, cancellationToken).ConfigureAwait(false);
            await PutAsync(new Uri(root, "more.html"), Stamp(Pages["more.html"]), "text/html", token, cancellationToken).ConfigureAwait(false);
            await PutAsync(new Uri(root, "players.html"), Stamp(Pages["players.html"]), "text/html", token, cancellationToken).ConfigureAwait(false);
            await PutAsync(new Uri(root, "board.json"), DraftJson.Serialize(snapshot), "application/json", token, cancellationToken).ConfigureAwait(false);
            await PutAsync(new Uri(root, "available.json"), DraftJson.Serialize(remaining), "application/json", token, cancellationToken).ConfigureAwait(false);

            SetStatus($"Published {league.Name} to {folder} at {_clock.GetLocalNow():t}.");
            return PublishOutcome.Success;
        }
        catch (Exception ex)
        {
            SetStatus("Publish failed: " + Short(ex));
            return PublishOutcome.Failed;
        }
    }

    public void Dispose()
    {
        lock (_retryLock)
        {
            _retryCts?.Cancel();
            _retryCts?.Dispose();
            _retryCts = null;
        }
    }

    private async Task PutAsync(Uri url, string body, string mediaType, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, mediaType)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private void SetStatus(string status)
    {
        _lastStatus = status;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string Short(Exception ex)
    {
        var message = ex is HttpRequestException http && http.StatusCode is { } code
            ? $"{(int)code} {code}"
            : ex.Message;
        return message.Length <= 160 ? message : message[..157] + "...";
    }

    private static string Stamp(string html) =>
        html.Replace("__APP_VERSION__", DisplayVersion(), StringComparison.Ordinal);

    private static string DisplayVersion()
    {
        var full = typeof(HttpsBoardPublisher).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "0.0.0";
        return full.Split('+')[0];
    }

    private static IReadOnlyDictionary<string, string> LoadPages()
    {
        var assembly = typeof(HttpsBoardPublisher).Assembly;
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in new[] { "board.html", "more.html", "players.html" })
        {
            var name = assembly.GetManifestResourceNames()
                           .FirstOrDefault(n => n.EndsWith("." + file, StringComparison.OrdinalIgnoreCase))
                       ?? throw new InvalidOperationException($"Embedded {file} is missing.");
            using var stream = assembly.GetManifestResourceStream(name)
                               ?? throw new InvalidOperationException($"Embedded {file} could not be opened.");
            using var reader = new StreamReader(stream);
            map[file] = reader.ReadToEnd();
        }

        return map;
    }
}
