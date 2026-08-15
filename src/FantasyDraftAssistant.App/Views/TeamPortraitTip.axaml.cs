using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;

namespace FantasyDraftAssistant.App.Views;

public partial class TeamPortraitTip : UserControl
{
    public static readonly StyledProperty<string?> TeamKeyProperty =
        AvaloniaProperty.Register<TeamPortraitTip, string?>(nameof(TeamKey));

    private Bitmap? _bitmap;

    public TeamPortraitTip()
    {
        InitializeComponent();
    }

    public string? TeamKey
    {
        get => GetValue(TeamKeyProperty);
        set => SetValue(TeamKeyProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TeamKeyProperty)
            Reload();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Store() is { } store)
            store.Changed += OnStoreChanged;
        Reload();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (Store() is { } store)
            store.Changed -= OnStoreChanged;
        ClearImage();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnStoreChanged(object? sender, TeamId teamId)
    {
        if (TeamKey is not null && teamId.ToString().Equals(TeamKey, StringComparison.OrdinalIgnoreCase))
            Reload();
    }

    private void Reload()
    {
        ClearImage();
        var store = Store();
        if (store is not null
            && Guid.TryParse(TeamKey, out var guid)
            && store.ExistingPath(new TeamId(guid)) is { } path)
        {
            _bitmap = new Bitmap(path);
            PortraitImage.Source = _bitmap;
            PortraitImage.IsVisible = true;
            EmptyLabel.IsVisible = false;
            return;
        }

        PortraitImage.IsVisible = false;
        EmptyLabel.IsVisible = true;
    }

    private void ClearImage()
    {
        PortraitImage.Source = null;
        _bitmap?.Dispose();
        _bitmap = null;
    }

    private static ITeamPortraitStore? Store() =>
        App.Services.GetService(typeof(ITeamPortraitStore)) as ITeamPortraitStore;
}
