using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Yahoo;
using FantasyDraftAssistant.Data;
using FantasyDraftAssistant.Data.Database;
using Microsoft.Extensions.DependencyInjection;

namespace FantasyDraftAssistant.Data.Tests;

public class YahooImportPersistenceTests : IDisposable
{
    private readonly string _root;
    private readonly ServiceProvider _services;

    public YahooImportPersistenceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "fda-yahoo-tests", Guid.NewGuid().ToString("N"));
        var collection = new ServiceCollection();
        collection.AddFantasyDraftData(_root);
        _services = collection.BuildServiceProvider();
        _services.GetRequiredService<MigrationRunner>().Apply();
    }

    [Fact]
    public async Task Import_creates_yahoo_league_and_refresh_keeps_draft_order()
    {
        var leagues = _services.GetRequiredService<ILeagueService>();
        var mapped = YahooLeagueMapper.Map(Snapshot("Gridiron", userTeam: "Blue Steel"));
        var created = await leagues.UpsertImportedLeagueAsync(mapped.Request);

        Assert.Equal(FantasyPlatform.Yahoo, created.Platform);
        Assert.Equal("461.l.99", created.ExternalLeagueId);
        var firstTeams = await leagues.GetTeamsAsync(created.LeagueId);
        Assert.Equal(3, firstTeams.Count);
        Assert.Equal("Blue Steel", firstTeams.Single(t => t.TeamId.Equals(created.UserTeamId!.Value)).Name);
        Assert.Equal("461.l.99.t.2", firstTeams.Single(t => t.Name == "Blue Steel").ExternalTeamId);
        Assert.Contains(await leagues.GetRosterSlotsAsync(created.LeagueId), s => s.SlotCode == "Q/W/R/T");

        await leagues.SaveTeamsAsync(new SaveTeamsRequest
        {
            LeagueId = created.LeagueId,
            UserTeamId = created.UserTeamId,
            Teams = firstTeams.Select(t => new TeamDraftPosition
            {
                TeamId = t.TeamId,
                Name = t.Name,
                OwnerName = t.OwnerName,
                DraftPosition = t.Name == "Red Wave" ? 1 : t.Name == "Blue Steel" ? 3 : 2,
                ExternalTeamId = t.ExternalTeamId
            }).ToList()
        });

        var refreshedMap = YahooLeagueMapper.Map(Snapshot("Gridiron Refresh", userTeam: "Blue Steel"));
        var refreshed = await leagues.UpsertImportedLeagueAsync(refreshedMap.Request);
        Assert.Equal(created.LeagueId, refreshed.LeagueId);
        Assert.Equal("Gridiron Refresh", refreshed.Name);

        var after = await leagues.GetTeamsAsync(refreshed.LeagueId);
        Assert.Equal(3, after.Single(t => t.Name == "Blue Steel").DraftPosition);
        Assert.Equal(firstTeams.Single(t => t.Name == "Blue Steel").TeamId,
            after.Single(t => t.Name == "Blue Steel").TeamId);
    }

    [Fact]
    public async Task Refresh_can_replace_draft_order_when_asked()
    {
        var leagues = _services.GetRequiredService<ILeagueService>();
        var first = YahooLeagueMapper.Map(Snapshot("Order", userTeam: "Red Wave"));
        var created = await leagues.UpsertImportedLeagueAsync(first.Request);
        var teams = await leagues.GetTeamsAsync(created.LeagueId);
        await leagues.SaveTeamsAsync(new SaveTeamsRequest
        {
            LeagueId = created.LeagueId,
            UserTeamId = created.UserTeamId,
            Teams = teams.Select(t => new TeamDraftPosition
            {
                TeamId = t.TeamId,
                Name = t.Name,
                DraftPosition = 4 - t.DraftPosition,
                ExternalTeamId = t.ExternalTeamId
            }).ToList()
        });

        var second = YahooLeagueMapper.Map(Snapshot("Order", userTeam: "Red Wave"), new YahooImportOptions
        {
            ReplaceDraftOrder = true
        });
        await leagues.UpsertImportedLeagueAsync(second.Request);
        var after = await leagues.GetTeamsAsync(created.LeagueId);
        Assert.Equal(1, after.Single(t => t.Name == "Red Wave").DraftPosition);
        Assert.Equal(2, after.Single(t => t.Name == "Blue Steel").DraftPosition);
    }

    private static YahooLeagueSnapshot Snapshot(string name, string userTeam) =>
        new()
        {
            LeagueKey = "461.l.99",
            Name = name,
            Season = 2026,
            TeamCount = 3,
            Roster =
            [
                new YahooRosterPosition { Position = "QB", Count = 1 },
                new YahooRosterPosition { Position = "Q/W/R/T", Count = 1 },
                new YahooRosterPosition { Position = "BN", Count = 4 }
            ],
            Stats = [new YahooStatModifier { StatId = 11, Value = 0.5m }],
            Teams =
            [
                new YahooTeamSnapshot { TeamKey = "461.l.99.t.1", Name = "Red Wave", TeamNumber = 1, IsCurrentUser = userTeam == "Red Wave" },
                new YahooTeamSnapshot { TeamKey = "461.l.99.t.2", Name = "Blue Steel", TeamNumber = 2, IsCurrentUser = userTeam == "Blue Steel" },
                new YahooTeamSnapshot { TeamKey = "461.l.99.t.3", Name = "Green Machine", TeamNumber = 3, IsCurrentUser = false }
            ]
        };

    public void Dispose()
    {
        _services.Dispose();
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }
        catch (IOException)
        {
        }
    }
}
