using System.Text;
using System.Text.Json;
using FantasyDraftAssistant.Core.Ai;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.Providers.AI;

/// Asks a model to make one practice-draft pick.
///
/// Returns null rather than throwing on every failure path, because the caller's job is
/// to fall back to the deterministic policy and keep drafting. A practice draft must
/// never stall on a bad API response.
public sealed class AiMockPickAdvisor(
    IAiProviderRegistry providers,
    ICredentialStore credentials,
    IAiUsageService usage) : IMockPickAdvisor
{
    /// Enough for a short rationale plus whatever hidden reasoning the model does.
    private const int MaxOutputTokens = 2000;

    private static readonly TimeSpan PickTimeout = TimeSpan.FromSeconds(20);

    private const string SystemPrompt = """
        You are drafting one team in a fantasy football draft. You will be given your
        private strategy, your roster so far, what your team still needs, the recent
        picks, and a numbered shortlist of available players.

        Choose exactly one player from the shortlist. Reply with a single JSON object and
        nothing else - no prose, no markdown fence:

        {"pick": <number from the shortlist>, "reason": "<one short sentence>"}

        How to choose:
        - Draft to your strategy. It is the point of your seat, not a suggestion.
        - You may take a player ranked below others on the list when your strategy, a
          roster hole, or a tier about to empty justifies it. Say so in the reason.
        - Read the recent picks. If a position is running, that changes what is still
          there when your next turn comes around.
        - Do not draft a kicker or defense before the last two rounds.
        - The reason is one sentence, specific to this pick. Not a restatement of the
          strategy.
        - Do not begin the reason with the player's name or repeat it - the name is
          already shown next to the reason. Start with why.
        - Only players under YOUR ROSTER SO FAR, or marked [YOURS] in the recent picks,
          are on your team. Everything else belongs to a rival.
        """;

    public async Task<MockAiPick?> ChooseAsync(MockAiPickRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Candidates.Count == 0)
            return null;

        var (providerKey, model) = SplitModel(request.AiModel);
        var adapter = providers.Get(providerKey);
        if (adapter is null)
            return null;

        var key = await credentials.GetSecretAsync("ai", providerKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var prompt = BuildPrompt(request);
        var started = DateTimeOffset.UtcNow;

        AiTextCompletion completion;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(PickTimeout);
            completion = await adapter.CompleteTextAsync(model, SystemPrompt, prompt, MaxOutputTokens, timeout.Token);
        }
        catch (Exception)
        {
            // Timeout, cancellation, transport failure - the caller falls back.
            return null;
        }

        if (!completion.Succeeded)
            return null;

        var parsed = Parse(completion.Text, request.Candidates);
        if (parsed is null)
            return null;

        await RecordUsageAsync(request, model, prompt, completion.Text, started, cancellationToken);

        return new MockAiPick
        {
            PlayerId = parsed.Value.PlayerId,
            Reason = parsed.Value.Reason,
            Provider = providerKey,
            Model = completion.Model ?? model ?? providerKey
        };
    }

    /// "openai:gpt-5.6-luna" -> ("openai", "gpt-5.6-luna"). A bare provider key is
    /// allowed and lets the adapter fall back to its own default model.
    public static (string Provider, string? Model) SplitModel(string? aiModel)
    {
        var text = (aiModel ?? string.Empty).Trim();
        var separator = text.IndexOf(':');
        return separator <= 0
            ? (text, null)
            : (text[..separator].Trim(), text[(separator + 1)..].Trim());
    }

    public static string FormatModel(string providerKey, string? model) =>
        string.IsNullOrWhiteSpace(model) ? providerKey : $"{providerKey}:{model}";

    private static string BuildPrompt(MockAiPickRequest request)
    {
        var builder = new StringBuilder();
        builder.Append("You are ").Append(request.TeamName)
            .Append(". Round ").Append(request.Round)
            .Append(", pick ").Append(request.RoundPick)
            .Append(" of ").Append(request.RoundCount).AppendLine(" rounds.");
        builder.AppendLine();
        builder.Append("YOUR STRATEGY: ").AppendLine(request.StrategyPrompt);
        builder.AppendLine();

        if (request.ScoringSummary is { Length: > 0 })
            builder.Append("SCORING: ").AppendLine(request.ScoringSummary).AppendLine();

        builder.AppendLine("YOUR ROSTER SO FAR:");
        builder.AppendLine(request.RosterSoFar.Count == 0 ? "  (empty)" : "  " + string.Join(", ", request.RosterSoFar));
        builder.AppendLine();

        builder.AppendLine("STILL NEEDED:");
        builder.AppendLine(request.RemainingNeeds.Count == 0 ? "  (starters filled)" : "  " + string.Join(", ", request.RemainingNeeds));
        builder.AppendLine();

        if (request.RecentPicks.Count > 0)
        {
            builder.AppendLine("RECENT PICKS (most recent first):");
            foreach (var pick in request.RecentPicks)
                builder.Append("  ").AppendLine(pick);
            builder.AppendLine();
        }

        builder.AppendLine("AVAILABLE (choose one by number):");
        for (var i = 0; i < request.Candidates.Count; i++)
        {
            var candidate = request.Candidates[i];
            builder.Append("  ").Append(i + 1).Append(". ")
                .Append(candidate.Name)
                .Append(" (").Append(candidate.Position);
            if (candidate.Team is { Length: > 0 })
                builder.Append(", ").Append(candidate.Team);
            builder.Append(") rank ").Append(candidate.OverallRank);
            if (candidate.Adp is { } adp)
                builder.Append(", ADP ").Append(adp.ToString("0.#"));
            if (candidate.YearsExp == 0)
                builder.Append(", rookie");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static (PlayerId PlayerId, string Reason)? Parse(string text, IReadOnlyList<MockAiCandidate> candidates)
    {
        try
        {
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start < 0 || end <= start)
                return null;

            using var doc = JsonDocument.Parse(text[start..(end + 1)]);
            var root = doc.RootElement;

            if (!root.TryGetProperty("pick", out var pick))
                return null;

            var index = pick.ValueKind switch
            {
                JsonValueKind.Number when pick.TryGetInt32(out var number) => number,
                JsonValueKind.String when int.TryParse(pick.GetString(), out var number) => number,
                _ => 0
            };

            // The shortlist is one-based in the prompt. Anything outside it is a
            // hallucination, and a pick the caller must not make.
            if (index < 1 || index > candidates.Count)
                return null;

            var reason = root.TryGetProperty("reason", out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()?.Trim()
                : null;

            return (candidates[index - 1].PlayerId,
                string.IsNullOrWhiteSpace(reason) ? "No reason given." : reason);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task RecordUsageAsync(
        MockAiPickRequest request,
        string? model,
        string prompt,
        string answer,
        DateTimeOffset started,
        CancellationToken cancellationToken)
    {
        try
        {
            await usage.RecordAsync(new AiUsageRecord
            {
                DraftId = request.DraftId,
                Provider = SplitModel(request.AiModel).Provider,
                Model = model ?? "",
                AnalyzedStateVersion = 0,
                EstimatedCost = AiCostEstimate.EstimateUsd(model, prompt.Length, answer.Length),
                Latency = DateTimeOffset.UtcNow - started,
                RequestStartedAt = started,
                ResponseCompletedAt = DateTimeOffset.UtcNow
            }, cancellationToken);
        }
        catch (Exception)
        {
            // Never fail a pick because bookkeeping failed.
        }
    }
}
