using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FantasyDraftAssistant.App.ViewModels;

namespace FantasyDraftAssistant.App.Views;

public partial class PortraitPromptView : UserControl
{
    public PortraitPromptView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        PromptBox.Focus();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;
        if (DataContext is PortraitPromptViewModel { IsGenerating: false } vm)
        {
            vm.CloseCommand.Execute(null);
            e.Handled = true;
        }
    }
}
