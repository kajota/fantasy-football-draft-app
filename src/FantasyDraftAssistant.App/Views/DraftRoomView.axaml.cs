using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FantasyDraftAssistant.App.ViewModels;

namespace FantasyDraftAssistant.App.Views;

public partial class DraftRoomView : UserControl
{
    private DraftRoomViewModel? _vm;

    public DraftRoomView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnDraftRoomPropertyChanged;
        base.OnDataContextChanged(e);
        _vm = DataContext as DraftRoomViewModel;
        if (_vm is not null)
            _vm.PropertyChanged += OnDraftRoomPropertyChanged;
        ScrollBoardToCurrent();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_vm is null && DataContext is DraftRoomViewModel vm)
        {
            _vm = vm;
            _vm.PropertyChanged += OnDraftRoomPropertyChanged;
        }

        ScrollBoardToCurrent();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnDraftRoomPropertyChanged;
        _vm = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnDraftRoomPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DraftRoomViewModel.CurrentBoardRow)
            or nameof(DraftRoomViewModel.StateVersion))
        {
            ScrollBoardToCurrent();
        }
    }

    private void ScrollBoardToCurrent()
    {
        if (_vm?.CurrentBoardRow is not { } row)
            return;
        Dispatcher.UIThread.Post(() =>
        {
            if (BoardList.ItemCount > 0)
            {
                BoardList.SelectedItem = row;
                BoardList.ScrollIntoView(row);
            }

            if (BoardTabList.ItemCount > 0)
            {
                BoardTabList.SelectedItem = row;
                BoardTabList.ScrollIntoView(row);
            }
        }, DispatcherPriority.Background);
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not DraftRoomViewModel vm)
            return;

        if (e.Source is TextBox)
        {
            if (e.Key == Key.Enter
                && e.KeyModifiers == KeyModifiers.None
                && (ReferenceEquals(e.Source, AiPromptBox) || ReferenceEquals(e.Source, AiPromptBoxTab)))
            {
                await vm.AskAiCommand.ExecuteAsync(null);
                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.Oem2 || (e.Key == Key.D7 && e.KeyModifiers == KeyModifiers.Shift))
        {
            SearchBox.Focus();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            vm.Search = "";
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            await vm.DraftSelectedCommand.ExecuteAsync(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.D && e.KeyModifiers == KeyModifiers.Control)
        {
            await vm.DraftQueueCommand.ExecuteAsync(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Z && e.KeyModifiers == KeyModifiers.Control)
        {
            await vm.RollbackLastCommand.ExecuteAsync(null);
            e.Handled = true;
            return;
        }

        if ((e.Key == Key.Y && e.KeyModifiers == KeyModifiers.Control) ||
            (e.Key == Key.Z && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift)))
        {
            await vm.RedoCommand.ExecuteAsync(null);
            e.Handled = true;
        }
    }
}
