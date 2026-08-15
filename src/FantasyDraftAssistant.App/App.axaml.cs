using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FantasyDraftAssistant.App.ViewModels;
using FantasyDraftAssistant.App.Views;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Data;
using FantasyDraftAssistant.Data.Database;
using FantasyDraftAssistant.Providers.AI;
using FantasyDraftAssistant.Providers.FantasyData;
using FantasyDraftAssistant.Providers.Yahoo;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace FantasyDraftAssistant.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddFantasyDraftData();
        services.AddHttpClient("sleeper", client =>
        {
            client.BaseAddress = new Uri("https://api.sleeper.app/v1/");
            client.Timeout = TimeSpan.FromMinutes(2);
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "FantasyDraftAssistant/0.1 (local draft assistant)");
        });
        services.AddHttpClient("yahoo-oauth", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "FantasyDraftAssistant/0.1 (local draft assistant)");
        });
        services.AddHttpClient("yahoo-fantasy", client =>
        {
            client.BaseAddress = new Uri(YahooFantasyClient.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(45);
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "FantasyDraftAssistant/0.1 (local draft assistant)");
        });
        services.AddSingleton<IFantasyDataProvider, SeedFantasyDataProvider>();
        services.AddHttpClient("fantasypros", client =>
        {
            client.BaseAddress = new Uri("https://api.fantasypros.com/public/v2/json/");
            client.Timeout = TimeSpan.FromMinutes(2);
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "FantasyDraftAssistant/0.1 (local draft assistant)");
        });
        services.AddSingleton<IFantasyDataProvider>(sp => new SleeperFantasyDataProvider(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("sleeper"),
            sp.GetRequiredService<IFantasyDataWriter>()));
        services.AddSingleton<IFantasyDataProvider>(sp => new FantasyProsFantasyDataProvider(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("fantasypros"),
            sp.GetRequiredService<IFantasyDataWriter>(),
            sp.GetRequiredService<ICredentialStore>()));
        services.AddSingleton<IFantasyDataProviderRegistry, FantasyDataProviderRegistry>();
        services.AddSingleton<IAiProviderAdapter, OpenAiProviderAdapter>();
        services.AddSingleton<IAiProviderAdapter, AnthropicProviderAdapter>();
        services.AddSingleton<IAiProviderAdapter, XaiProviderAdapter>();
        services.AddSingleton<IAiProviderRegistry, AiProviderRegistry>();
        services.AddHttpClient("imagine", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2);
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "FantasyDraftAssistant/0.1 (local draft assistant)");
        });
        services.AddSingleton<ITeamPortraitGenerator>(sp => new TeamPortraitGenerator(
            sp.GetRequiredService<ICredentialStore>(),
            sp.GetRequiredService<ITeamPortraitStore>(),
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("imagine")));
        services.AddSingleton(sp => new YahooOAuthClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("yahoo-oauth")));
        services.AddSingleton<YahooAuthService>();
        services.AddSingleton<IYahooAuthService>(sp => sp.GetRequiredService<YahooAuthService>());
        services.AddSingleton(sp => new YahooFantasyClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("yahoo-fantasy")));
        services.AddSingleton<IYahooLeagueImporter, YahooLeagueImporter>();
        services.AddSingleton<SessionState>();
        services.AddSingleton<Navigator>();
        services.AddSingleton<ShellViewModel>();
        services.AddTransient<LeaguesViewModel>();
        services.AddTransient<LeagueSetupViewModel>();
        services.AddTransient<DraftOrderViewModel>();
        services.AddTransient<KeepersViewModel>();
        services.AddTransient<YahooViewModel>();
        services.AddTransient<DataSourcesViewModel>();
        services.AddTransient<AiSettingsViewModel>();
        services.AddTransient<ReadinessViewModel>();
        services.AddTransient<DraftRoomViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<BranchesViewModel>();
        services.AddTransient<RecapViewModel>();
        Services = services.BuildServiceProvider();

        Services.GetRequiredService<MigrationRunner>().Apply();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<ShellViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
