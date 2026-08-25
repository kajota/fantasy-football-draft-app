using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FantasyDraftAssistant.Core.Ai;
using FantasyDraftAssistant.Core.Interfaces;

namespace FantasyDraftAssistant.App.ViewModels;

public partial class PortraitPromptViewModel : ObservableObject, IDisposable
{
    private readonly TeamRow _row;
    private readonly ITeamPortraitGenerator _generator;
    private readonly ITeamPortraitStore _store;

    public PortraitPromptViewModel(
        TeamRow row,
        ITeamPortraitGenerator generator,
        ITeamPortraitStore store,
        IReadOnlyList<PortraitAiOption> providers,
        PortraitAiOption? selectedProvider)
    {
        _row = row;
        _generator = generator;
        _store = store;
        Providers = providers;
        Heading = $"Image for {row.DisplayTeamName}";
        _loading = true;
        SelectedProvider = selectedProvider ?? providers.FirstOrDefault();
        NormalImage = row.NormalImage;
        SelectedArtStyle = row.SelectedArtStyle;
        Prompt = row.NewImagePrompt;
        HasPreviousPrompt = !string.IsNullOrWhiteSpace(store.LastPrompt(row.TeamId));
        _loading = false;
        _store.Changed += OnStoreChanged;
        LoadPortrait();
    }

    private bool _loading;

    public IReadOnlyList<PortraitAiOption> Providers { get; }
    public IReadOnlyList<PortraitArtStyle> ArtStyleOptions => TeamPortraitPrompt.ArtStyles;
    public bool CanChoosePortraitStyle => _row.CanChoosePortraitStyle;
    public string Heading { get; }

    [ObservableProperty] private PortraitAiOption? _selectedProvider;
    [ObservableProperty] private bool _normalImage;
    [ObservableProperty] private PortraitArtStyle _selectedArtStyle = TeamPortraitPrompt.RandomArtStyle;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGenerate))]
    private string _prompt = "";
    [ObservableProperty] private string _status = "Edit the prompt, copy it, or send it to Grok or ChatGPT.";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGenerate))]
    private bool _isGenerating;
    [ObservableProperty] private Bitmap? _portrait;
    [ObservableProperty] private bool _hasPortrait;
    [ObservableProperty] private bool _hasPreviousPrompt;

    public bool CanGenerate => !IsGenerating && !string.IsNullOrWhiteSpace(Prompt);

    public event EventHandler? RequestClose;

    [RelayCommand]
    private void Close()
    {
        if (IsGenerating)
            return;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task CopyAsync()
    {
        if (string.IsNullOrWhiteSpace(Prompt))
        {
            Status = "Nothing to copy.";
            return;
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(Prompt);
            Status = "Copied the prompt.";
            return;
        }

        Status = "Could not reach the clipboard.";
    }

    [RelayCommand]
    private void ResetPrompt()
    {
        Prompt = _row.NewImagePrompt;
        Status = "Restored the generated prompt for this team.";
    }

    partial void OnNormalImageChanged(bool value)
    {
        if (_loading)
            return;
        _row.NormalImage = value;
        Prompt = _row.NewImagePrompt;
        Status = value
            ? "Prompt is the hero treatment. Edit it if you want, then generate."
            : "Prompt is the roast. Edit it if you want, then generate.";
    }

    partial void OnSelectedArtStyleChanged(PortraitArtStyle value)
    {
        if (_loading)
            return;
        _row.SelectedArtStyle = value;
        Prompt = _row.NewImagePrompt;
        Status = $"Prompt uses {value.Title}.";
    }

    [RelayCommand]
    private void StartWithPreviousPrompt()
    {
        var previous = _store.LastPrompt(_row.TeamId);
        if (string.IsNullOrWhiteSpace(previous))
        {
            HasPreviousPrompt = false;
            Status = "No previous generate prompt for this team.";
            return;
        }

        Prompt = previous;
        Status = "Loaded the prompt that made this team's last generated image.";
    }

    [RelayCommand]
    private void NewVariation()
    {
        _row.RerollPreviewSpin();
        Prompt = _row.NewImagePrompt;
        Status = "New generated prompt. Outfit, setting, and random style changed.";
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (!CanGenerate)
            return;

        _row.IsGenerating = true;
        IsGenerating = true;
        _row.PortraitStatus = "Generating…";
        Status = $"Generating with {SelectedProvider?.Label ?? "the image AI"}…";
        try
        {
            var result = await _generator.GenerateAsync(
                _row.ToPortraitRequest(SelectedProvider?.ProviderKey, Prompt));
            _row.HasPortrait = result.Succeeded || _store.Exists(_row.TeamId);
            _row.PortraitStatus = result.Succeeded
                ? $"Ready · {SelectedProvider?.Label ?? "image"}"
                : result.Error ?? "Failed";
            Status = result.Succeeded
                ? $"Ready from {result.Provider}. Tweak the prompt and generate again, or switch AIs."
                : result.Error ?? "Failed.";
            if (result.Succeeded)
            {
                HasPreviousPrompt = true;
                LoadPortrait();
            }
        }
        finally
        {
            IsGenerating = false;
            _row.IsGenerating = false;
        }
    }

    public void Dispose()
    {
        _store.Changed -= OnStoreChanged;
        Portrait?.Dispose();
        Portrait = null;
    }

    private void OnStoreChanged(object? sender, Core.Ids.TeamId teamId)
    {
        if (!_row.TeamId.Equals(teamId))
            return;
        Dispatcher.UIThread.Post(LoadPortrait);
    }

    private void LoadPortrait()
    {
        Portrait?.Dispose();
        Portrait = null;
        HasPortrait = false;
        if (_store.ExistingPath(_row.TeamId) is not { } path)
            return;
        try
        {
            using var file = File.OpenRead(path);
            using var memory = new MemoryStream();
            file.CopyTo(memory);
            memory.Position = 0;
            Portrait = new Bitmap(memory);
            HasPortrait = true;
        }
        catch (Exception ex)
        {
            Status = $"Could not load the current image: {ex.Message}";
        }
    }
}
