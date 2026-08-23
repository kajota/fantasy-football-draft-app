using FantasyDraftAssistant.Core.Engine;

namespace FantasyDraftAssistant.Core.Tests;

public class BoardSlugTests
{
    [Theory]
    [InlineData("filthymothers", "filthymothers")]
    [InlineData("Football Fanatics", "football-fanatics")]
    [InlineData("Strata", "strata")]
    public void Normalizes_lowercase_and_hyphens(string raw, string expected)
    {
        Assert.True(BoardSlug.TryNormalize(raw, out var slug));
        Assert.Equal(expected, slug);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Foo_Bar")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    public void Rejects_invalid(string raw) => Assert.False(BoardSlug.TryNormalize(raw, out _));

    [Fact]
    public void PublicUrl_joins_base_and_slug()
    {
        Assert.Equal(
            "https://kellynorton.com/draft/filthymothers/",
            BoardSlug.PublicUrl("https://kellynorton.com/draft/", "FilthyMothers"));
    }
}
