namespace FantasyDraftAssistant.Core.Ai;

/// <summary>A player the scanner is allowed to find in advisor prose.</summary>
public sealed record PlayerMentionCandidate(string PlayerId, string Name);

/// <summary>Where a player's name sits inside a block of text.</summary>
public sealed record PlayerMention(int Start, int Length, string PlayerId, string Name);

/// <summary>
/// Finds player names inside AI advisor answers so the UI can turn them into links.
/// Matching is whole-name and punctuation-insensitive, so "Amon-Ra St. Brown" is found
/// however the model chose to punctuate it. Single-word names are ignored — they collide
/// with ordinary prose far too often to be worth linking.
///
/// Build the index once per refresh and scan with it repeatedly: answers stream in a chunk
/// at a time, so <see cref="Scan"/> runs on every partial response.
/// </summary>
public sealed class PlayerMentionIndex
{
    private const int MaxNameTokens = 6;
    private const int MinNameTokens = 2;

    private readonly Dictionary<string, PlayerMentionCandidate> _byName;
    private readonly HashSet<string> _firstTokens;
    private readonly int _longestName;

    public static PlayerMentionIndex Empty { get; } = Build([]);

    private PlayerMentionIndex(
        Dictionary<string, PlayerMentionCandidate> byName,
        HashSet<string> firstTokens,
        int longestName)
    {
        _byName = byName;
        _firstTokens = firstTokens;
        _longestName = longestName;
    }

    public int Count => _byName.Count;

    public static PlayerMentionIndex Build(IEnumerable<PlayerMentionCandidate> candidates)
    {
        var byName = new Dictionary<string, PlayerMentionCandidate>(StringComparer.Ordinal);
        var firstTokens = new HashSet<string>(StringComparer.Ordinal);
        var longest = MinNameTokens;

        foreach (var candidate in candidates)
        {
            var tokens = Tokenize(candidate.Name);
            if (tokens.Count is < MinNameTokens or > MaxNameTokens)
                continue;

            var key = string.Join(' ', tokens.Select(t => t.Text));
            // First spelling wins; duplicates are the same human often enough that
            // picking either one beats linking neither.
            if (!byName.TryAdd(key, candidate))
                continue;

            firstTokens.Add(tokens[0].Text);
            longest = Math.Max(longest, tokens.Count);
        }

        return new PlayerMentionIndex(byName, firstTokens, longest);
    }

    public IReadOnlyList<PlayerMention> Scan(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || _byName.Count == 0)
            return [];

        var tokens = Tokenize(text);
        if (tokens.Count < MinNameTokens)
            return [];

        var found = new List<PlayerMention>();
        var index = 0;
        while (index < tokens.Count)
        {
            // Most prose words start no player's name, so this gate skips nearly
            // every token before any n-gram strings get built.
            if (!_firstTokens.Contains(tokens[index].Text))
            {
                index++;
                continue;
            }

            var matched = false;
            var widest = Math.Min(_longestName, tokens.Count - index);
            for (var span = widest; span >= MinNameTokens; span--)
            {
                var key = string.Join(' ', tokens.Skip(index).Take(span).Select(t => t.Text));
                if (!_byName.TryGetValue(key, out var candidate))
                    continue;

                var start = tokens[index].Start;
                var last = tokens[index + span - 1];
                found.Add(new PlayerMention(start, last.Start + last.Length - start, candidate.PlayerId, candidate.Name));
                index += span;
                matched = true;
                break;
            }

            if (!matched)
                index++;
        }

        return found;
    }

    /// <summary>
    /// Splits text into alternating plain and player runs, in order, covering every character.
    /// A mention's text is returned exactly as the model wrote it, not as the roster spells it.
    /// </summary>
    public IReadOnlyList<(string Text, PlayerMention? Mention)> Split(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var mentions = Scan(text);
        if (mentions.Count == 0)
            return [(text, null)];

        var parts = new List<(string, PlayerMention?)>();
        var cursor = 0;
        foreach (var mention in mentions)
        {
            if (mention.Start > cursor)
                parts.Add((text[cursor..mention.Start], null));
            parts.Add((text.Substring(mention.Start, mention.Length), mention));
            cursor = mention.Start + mention.Length;
        }

        if (cursor < text.Length)
            parts.Add((text[cursor..], null));

        return parts;
    }

    private static List<(string Text, int Start, int Length)> Tokenize(string text)
    {
        var tokens = new List<(string, int, int)>();
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var isWord = i < text.Length && char.IsLetterOrDigit(text[i]);
            if (isWord && start < 0)
            {
                start = i;
            }
            else if (!isWord && start >= 0)
            {
                tokens.Add((text[start..i].ToLowerInvariant(), start, i - start));
                start = -1;
            }
        }

        return tokens;
    }
}
