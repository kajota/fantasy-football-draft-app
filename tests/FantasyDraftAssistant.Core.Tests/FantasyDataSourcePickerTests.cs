using FantasyDraftAssistant.Core.Analytics;

namespace FantasyDraftAssistant.Core.Tests;

public class FantasyDataSourcePickerTests
{
    private static readonly FantasyDataFormat OneQbHalf = new(ConsensusScoring.HalfPpr, Superflex: false);
    private static readonly FantasyDataFormat SuperflexHalf = new(ConsensusScoring.HalfPpr, Superflex: true);

    [Fact]
    public void Prefers_the_league_sheet_over_generic_fantasypros()
    {
        var picked = FantasyDataSourcePicker.Pick(
            ["sleeper", "fantasypros", "fantasypros-half", "fantasypros-half-sf"],
            OneQbHalf);

        Assert.Equal("fantasypros-half", picked);
    }

    [Fact]
    public void Superflex_league_does_not_use_one_qb_sheet()
    {
        var picked = FantasyDataSourcePicker.Pick(
            ["fantasypros-half", "fantasypros-half-sf", "fantasypros"],
            SuperflexHalf);

        Assert.Equal("fantasypros-half-sf", picked);
    }

    [Fact]
    public void Does_not_fall_back_to_a_different_fantasypros_sheet()
    {
        var picked = FantasyDataSourcePicker.Pick(
            ["fantasypros-ppr-sf", "sleeper"],
            OneQbHalf);

        Assert.Equal("sleeper", picked);
    }

    [Fact]
    public void Superflex_falls_back_to_sleeper_not_one_qb_fantasypros()
    {
        Assert.Equal("sleeper", FantasyDataSourcePicker.Pick(["fantasypros", "sleeper", "fantasypros-half"], SuperflexHalf));
    }

    [Fact]
    public void Falls_back_to_generic_fantasypros_then_sleeper()
    {
        Assert.Equal("fantasypros", FantasyDataSourcePicker.Pick(["sleeper", "fantasypros"], OneQbHalf));
        Assert.Equal("sleeper", FantasyDataSourcePicker.Pick(["seed", "sleeper"], OneQbHalf));
        Assert.Equal("seed", FantasyDataSourcePicker.Pick(["seed"], OneQbHalf));
        Assert.Null(FantasyDataSourcePicker.Pick([], OneQbHalf));
    }

    [Fact]
    public void Display_label_keeps_the_users_current_choice()
    {
        var labels = new[] { "FantasyPros (Half PPR 1-QB)", "Sleeper" };
        var chosen = FantasyDataSourcePicker.ChooseDisplayLabel(
            labels,
            currentLabel: "Sleeper",
            sessionLabel: "FantasyPros (Half PPR 1-QB)",
            leagueLabel: "FantasyPros (Half PPR 1-QB)");
        Assert.Equal("Sleeper", chosen);
    }

    [Fact]
    public void Describe_names_the_matched_sheet()
    {
        Assert.Equal(
            "FantasyPros Half PPR 1-QB",
            FantasyDataSourcePicker.Describe("fantasypros-half", OneQbHalf));
        Assert.Equal("Sleeper", FantasyDataSourcePicker.Describe("sleeper", OneQbHalf));
        Assert.Equal("no cached ranks", FantasyDataSourcePicker.Describe(null, OneQbHalf));
    }
}
