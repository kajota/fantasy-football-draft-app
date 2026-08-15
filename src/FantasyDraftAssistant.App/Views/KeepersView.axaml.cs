using Avalonia.Controls;
using Avalonia.Input;
using FantasyDraftAssistant.App.ViewModels;

namespace FantasyDraftAssistant.App.Views;

public partial class KeepersView : UserControl
{
    public KeepersView() => InitializeComponent();

    private void OnPlayerDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is KeepersViewModel vm)
            vm.ConfirmPickCommand.Execute(null);
    }
}
