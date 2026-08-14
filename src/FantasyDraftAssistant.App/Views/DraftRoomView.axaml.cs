using Avalonia.Controls;
using Avalonia.Input;
using FantasyDraftAssistant.App.ViewModels;

namespace FantasyDraftAssistant.App.Views;

public partial class DraftRoomView : UserControl
{
    public DraftRoomView()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not DraftRoomViewModel vm)
            return;

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
