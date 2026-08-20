using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Models;
using FantasyDraftAssistant.Data;
using FantasyDraftAssistant.Data.Database;
using Microsoft.Extensions.DependencyInjection;

namespace FantasyDraftAssistant.Data.Tests;

public class PracticePersonalityTests : IDisposable
{
    private readonly string _root;
    private readonly ServiceProvider _services;

    public PracticePersonalityTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "fda-tests", Guid.NewGuid().ToString("N"));
        var collection = new ServiceCollection();
        collection.AddFantasyDraftData(_root);
        collection.AddSingleton<IFantasyDataProvider, FantasyDraftAssistant.Providers.FantasyData.SeedFantasyDataProvider>();
        _services = collection.BuildServiceProvider();
        _services.GetRequiredService<MigrationRunner>().Apply();
    }

    public void Dispose()
    {
        _services.Dispose();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Saved_team_personality_round_trips_and_reaches_seat_policies()
    {
        var seed = _services.GetRequiredService<IFantasyDataProvider>();
        await seed.RefreshAsync(new Core.Results.FantasyDataRefreshRequest(), CancellationToken.None);
        var leagues = _services.GetRequiredService<ILeagueService>();
        var league = await leagues.CreateLeagueAsync(new CreateLeagueRequest
        {
            Name = "Personality League",
            Season = 2026,
            TeamCount = 4,
            DraftType = DraftType.Snake,
            RoundCount = 4,
            UserTeamName = "My Team"
        });
        var teams = await leagues.GetTeamsAsync(league.LeagueId);
        var cpuSeat = teams.First(t => t.DraftPosition == 2);

        await leagues.SaveTeamsAsync(new SaveTeamsRequest
        {
            LeagueId = league.LeagueId,
            Teams = teams.Select(t => new TeamDraftPosition
            {
                TeamId = t.TeamId,
                Name = t.Name,
                OwnerName = t.OwnerName,
                DisplayLabel = t.DisplayLabel,
                PortraitNotes = t.PortraitNotes,
                DraftPosition = t.DraftPosition,
                ExternalTeamId = t.ExternalTeamId,
                PracticePersonality = t.TeamId.Equals(cpuSeat.TeamId) ? MockPersonality.ZeroRb : null
            }).ToList()
        });

        var reloaded = await leagues.GetTeamsAsync(league.LeagueId);
        Assert.Equal(MockPersonality.ZeroRb, reloaded.First(t => t.TeamId.Equals(cpuSeat.TeamId)).PracticePersonality);
        Assert.Null(reloaded.First(t => t.DraftPosition == 3).PracticePersonality);

        var draft = await leagues.CreateDraftAsync(new CreateDraftRequest
        {
            LeagueId = league.LeagueId,
            Name = "Personality Draft"
        });
        var start = await _services.GetRequiredService<IDraftCommandService>()
            .StartDraftAsync(new Core.Commands.StartDraftCommand(draft.DraftId));
        Assert.True(start.Succeeded, start.Error);

        var mock = _services.GetRequiredService<IMockDraftService>();
        var state = await _services.GetRequiredService<IDraftStateService>().GetWorkingStateAsync(draft.DraftId);
        Assert.NotNull(state);
        await mock.SeedPoliciesAsync(draft.DraftId, state.ActiveBranch.BranchId);

        var policies = await mock.GetPoliciesAsync(draft.DraftId, state.ActiveBranch.BranchId);
        var policy = policies.Single(p => p.TeamId.Equals(cpuSeat.TeamId));
        Assert.Equal(MockPersonality.ZeroRb, policy.Personality);
        Assert.True(policy.IsCpu);
    }

    [Fact]
    public async Task Non_sleeper_refresh_does_not_reset_injury_status()
    {
        var writer = _services.GetRequiredService<IFantasyDataWriter>();
        var playerId = Core.Ids.PlayerId.FromName("Bijan Robinson", "RB");
        var now = DateTimeOffset.UtcNow;

        Player PlayerWith(PlayerStatus status) => new()
        {
            PlayerId = playerId,
            Name = "Bijan Robinson",
            NflTeam = "ATL",
            PrimaryPosition = PlayerPosition.RB,
            EligiblePositions = [PlayerPosition.RB],
            Status = status,
            StatusUpdatedAt = now
        };

        // Sleeper flags the injury.
        await writer.WriteAsync("sleeper", [PlayerWith(PlayerStatus.Out)], [], [], [], [], CancellationToken.None);

        // A FantasyPros sheet refresh knows nothing about injuries and writes Active.
        await writer.WriteAsync("fantasypros", [PlayerWith(PlayerStatus.Active)], [], [], [], [], CancellationToken.None);
        var players = await _services.GetRequiredService<IDraftStateService>().GetPlayersAsync();
        Assert.Equal(PlayerStatus.Out, players.Single(p => p.PlayerId.Equals(playerId)).Status);

        // A non-Sleeper provider may still flag a new injury it knows about.
        await writer.WriteAsync("fantasypros", [PlayerWith(PlayerStatus.Questionable)], [], [], [], [], CancellationToken.None);
        players = await _services.GetRequiredService<IDraftStateService>().GetPlayersAsync();
        Assert.Equal(PlayerStatus.Questionable, players.Single(p => p.PlayerId.Equals(playerId)).Status);

        // Sleeper is authoritative both ways: recovery back to Active sticks.
        await writer.WriteAsync("sleeper", [PlayerWith(PlayerStatus.Active)], [], [], [], [], CancellationToken.None);
        players = await _services.GetRequiredService<IDraftStateService>().GetPlayersAsync();
        Assert.Equal(PlayerStatus.Active, players.Single(p => p.PlayerId.Equals(playerId)).Status);
    }
}
