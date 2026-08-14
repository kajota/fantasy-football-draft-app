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
using Microsoft.Extensions.DependencyInjection;

namespace FantasyDraftAssistant.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddFantasyDraftData();
        services.AddSingleton<IFantasyDataProvider, SeedFantasyDataProvider>();
        services.AddSingleton<IAiProviderAdapter, OpenAiProviderAdapter>();
        services.AddSingleton<IAiProviderAdapter, AnthropicProviderAdapter>();
        services.AddSingleton<IAiProviderAdapter, XaiProviderAdapter>();
        services.AddSingleton<IAiProviderRegistry, AiProviderRegistry>();
        services.AddSingleton<SessionState>();
        services.AddSingleton<Navigator>();
        services.AddSingleton<ShellViewModel>();
        services.AddTransient<LeaguesViewModel>();
        services.AddTransient<LeagueSetupViewModel>();
        services.AddTransient<DraftOrderViewModel>();
        services.AddTransient<KeepersViewModel>();
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
