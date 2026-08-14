using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FantasyDraftAssistant.Core.Ai;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.App.ViewModels;

public partial class AiProviderEditor : ObservableObject
{
    public AiProviderEditor(AiProviderDescriptor descriptor)
    {
        ProviderKey = descriptor.ProviderKey;
        Title = $"{descriptor.ProductName} ({descriptor.CompanyName})";
        Help = descriptor.CredentialHelp;
        DefaultModel = descriptor.DefaultModel;
        Model = descriptor.DefaultModel;
    }

    public string ProviderKey { get; }
    public string Title { get; }
    public string Help { get; }
    public string DefaultModel { get; }
    public IReadOnlyList<string> Roles => AiProviderCatalog.Roles;

    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private string _apiKey = "";
    [ObservableProperty] private bool _keySaved;
    [ObservableProperty] private string _model = "";
    [ObservableProperty] private string _role = "Fast Advisor";
    [ObservableProperty] private string? _perDraftLimit;
    [ObservableProperty] private string _status = "";
}

public partial class AiSettingsViewModel(
    IAiConfigStore configs,
    ICredentialStore credentials,
    IAiProviderRegistry registry) : PageViewModel
{
    public ObservableCollection<AiProviderEditor> Providers { get; } = [];

    [ObservableProperty] private string _storeDescription = "";

    public override async Task OnNavigatedToAsync()
    {
        Title = "AI Providers";
        StoreDescription = credentials.Description;
        Providers.Clear();
        var saved = await configs.ListAsync();
        foreach (var descriptor in AiProviderCatalog.All)
        {
            var editor = new AiProviderEditor(descriptor);
            var config = saved.FirstOrDefault(c => c.ProviderKey == descriptor.ProviderKey);
            if (config is not null)
            {
                editor.Enabled = config.Enabled;
                editor.Model = config.Model;
                editor.Role = string.IsNullOrWhiteSpace(config.Role) ? "Fast Advisor" : config.Role;
                editor.PerDraftLimit = config.PerDraftSpendLimit?.ToString();
            }

            var secret = await credentials.GetSecretAsync("ai", descriptor.ProviderKey);
            editor.KeySaved = !string.IsNullOrWhiteSpace(secret);
            editor.ApiKey = "";
            Providers.Add(editor);
        }
    }

    [RelayCommand]
    private async Task SaveAsync(AiProviderEditor? editor)
    {
        if (editor is null)
            return;
        if (!string.IsNullOrWhiteSpace(editor.ApiKey))
        {
            await credentials.SaveSecretAsync("ai", editor.ProviderKey, editor.ApiKey.Trim());
            editor.KeySaved = true;
            editor.ApiKey = "";
        }

        decimal? limit = decimal.TryParse(editor.PerDraftLimit, out var value) ? value : null;
        await configs.SaveAsync(new AiProviderConfig
        {
            ProviderKey = editor.ProviderKey,
            Enabled = editor.Enabled,
            Model = string.IsNullOrWhiteSpace(editor.Model) ? editor.DefaultModel : editor.Model.Trim(),
            Role = editor.Role,
            PerDraftSpendLimit = limit
        });
        editor.Status = $"{editor.Title} saved. API keys are not stored in the draft database.";
    }

    [RelayCommand]
    private async Task TestAsync(AiProviderEditor? editor)
    {
        if (editor is null)
            return;
        if (!string.IsNullOrWhiteSpace(editor.ApiKey))
            await credentials.SaveSecretAsync("ai", editor.ProviderKey, editor.ApiKey.Trim());

        var adapter = registry.Get(editor.ProviderKey);
        if (adapter is null)
        {
            editor.Status = "No adapter is registered for this provider.";
            return;
        }

        editor.Status = "Testing connection…";
        var result = await adapter.TestConnectionAsync(new AiProviderConfig
        {
            ProviderKey = editor.ProviderKey,
            Enabled = true,
            Model = editor.Model
        });
        editor.Status = result.Succeeded
            ? $"{result.Message} Model: {result.Model ?? editor.Model}."
            : result.Message ?? "Connection failed.";
    }
}
