using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Data.Database;
using FantasyDraftAssistant.Data.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FantasyDraftAssistant.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFantasyDraftData(this IServiceCollection services, string? dataRoot = null)
    {
        services.AddSingleton(new AppPaths(AppDataRoot.ResolveFromEnvironment(dataRoot)));
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<MigrationRunner>();
        services.AddSingleton<IDraftChangeNotifier, DraftChangeNotifier>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<ILeagueService, LeagueService>();
        services.AddSingleton<IDraftCommandService, DraftCommandService>();
        services.AddSingleton<IDraftStateService, DraftStateService>();
        services.AddSingleton<IMockDraftService, MockDraftService>();
        services.AddSingleton<IFantasyDataWriter, FantasyDataWriter>();
        services.AddSingleton<IAnalyticsService, AnalyticsService>();
        services.AddSingleton<IDraftQueryService, DraftQueryService>();
        services.AddSingleton<IReadinessService, ReadinessService>();
        services.AddSingleton<IAiConfigStore, AiConfigStore>();
        services.AddSingleton<IAiUsageService, AiUsageService>();
        services.AddSingleton<IAiResponseStore, AiResponseStore>();
        services.AddSingleton<ITeamPortraitStore, TeamPortraitStore>();
        services.AddSingleton<IAppSettingsStore, AppSettingsStore>();
        services.AddSingleton<IBoardPublisher, NoOpBoardPublisher>();
        services.AddSingleton<FileCredentialStore>();
        services.AddSingleton<ICredentialStore>(sp =>
        {
            var fallback = sp.GetRequiredService<FileCredentialStore>();
            return OperatingSystem.IsLinux()
                ? new LinuxSecretToolCredentialStore(fallback)
                : fallback;
        });
        return services;
    }
}
