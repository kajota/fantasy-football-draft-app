using FantasyDraftAssistant.Core.Ai;

namespace FantasyDraftAssistant.Core.Tests;

public class PlayerMentionScannerTests
{
    private static readonly PlayerMentionCandidate[] Pool =
    [
        new("p1", "Bijan Robinson"),
        new("p2", "Amon-Ra St. Brown"),
        new("p3", "Marvin Harrison Jr."),
        new("p4", "Brown"),
        new("p5", "Puka Nacua")
    ];

    private static readonly PlayerMentionIndex Index = PlayerMentionIndex.Build(Pool);

    [Fact]
    public void FindsRecommendationName()
    {
        var text = "Recommendation: Bijan Robinson. He is the best back left.";
        var found = Index.Scan(text);

        var mention = Assert.Single(found);
        Assert.Equal("p1", mention.PlayerId);
        Assert.Equal("Bijan Robinson", text.Substring(mention.Start, mention.Length));
    }

    [Fact]
    public void MatchesRegardlessOfPunctuation()
    {
        // The model may write the name without the hyphen or the period.
        var text = "Take Amon Ra St Brown over Marvin Harrison Jr here.";
        var found = Index.Scan(text);

        Assert.Equal(["p2", "p3"], found.Select(m => m.PlayerId));
        Assert.Equal("Amon Ra St Brown", text.Substring(found[0].Start, found[0].Length));
        Assert.Equal("Marvin Harrison Jr", text.Substring(found[1].Start, found[1].Length));
    }

    [Fact]
    public void IgnoresSingleWordNames()
    {
        // "Brown" is a real pool entry but linking it would light up ordinary prose.
        var found = Index.Scan("The brown jersey team is on the clock.");
        Assert.Empty(found);
    }

    [Fact]
    public void PrefersTheLongestName()
    {
        // "Amon-Ra St. Brown" must win over the shorter "Brown" entry.
        var found = Index.Scan("I like Amon-Ra St. Brown.");

        var mention = Assert.Single(found);
        Assert.Equal("p2", mention.PlayerId);
    }

    [Fact]
    public void FindsRepeatedMentions()
    {
        var found = Index.Scan("Puka Nacua now; Puka Nacua later.");

        Assert.Equal(2, found.Count);
        Assert.All(found, m => Assert.Equal("p5", m.PlayerId));
        Assert.True(found[0].Start < found[1].Start);
    }

    [Fact]
    public void MatchIsCaseInsensitive()
    {
        var found = Index.Scan("bijan robinson is gone");
        Assert.Equal("p1", Assert.Single(found).PlayerId);
    }

    [Fact]
    public void DoesNotMatchAcrossUnrelatedWords()
    {
        var found = Index.Scan("Bijan is not Robinson Cano.");
        Assert.Empty(found);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyTextFindsNothing(string? text)
    {
        Assert.Empty(Index.Scan(text));
    }

    [Fact]
    public void EmptyPoolFindsNothing()
    {
        Assert.Empty(PlayerMentionIndex.Build([]).Scan("Bijan Robinson"));
    }

    [Fact]
    public void SplitCoversEveryCharacter()
    {
        var text = "Take Bijan Robinson, then Puka Nacua.";
        var parts = Index.Split(text);

        Assert.Equal(text, string.Concat(parts.Select(p => p.Text)));
        Assert.Equal(2, parts.Count(p => p.Mention is not null));
    }

    [Fact]
    public void SplitWithoutMentionsReturnsWholeText()
    {
        var parts = PlayerMentionIndex.Build([]).Split("nothing here");
        var part = Assert.Single(parts);
        Assert.Equal("nothing here", part.Text);
        Assert.Null(part.Mention);
    }

    [Fact]
    public void SplitKeepsTheModelsSpelling()
    {
        var text = "Amon Ra St Brown";
        var parts = Index.Split(text);

        var part = Assert.Single(parts);
        Assert.Equal("Amon Ra St Brown", part.Text);
        Assert.Equal("Amon-Ra St. Brown", part.Mention!.Name);
    }
}
