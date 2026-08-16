using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FantasyDraftAssistant.App.ViewModels;

namespace FantasyDraftAssistant.App.Views;

public partial class LeagueSetupView : UserControl
{
    private TeamRow? _dragRow;
    private Point _dragStart;

    public LeagueSetupView()
    {
        InitializeComponent();
        Focusable = true;
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.Source is TextBox box && box.Classes.Contains("seat"))
        {
            CommitSeat(box);
            e.Handled = true;
            return;
        }

        if (e.Key is not (Key.PageDown or Key.PageUp))
            return;

        var page = Math.Max(PageScroll.Viewport.Height * 0.9, 120);
        var max = Math.Max(0, PageScroll.Extent.Height - PageScroll.Viewport.Height);
        var delta = e.Key == Key.PageDown ? page : -page;
        var y = Math.Clamp(PageScroll.Offset.Y + delta, 0, max);
        PageScroll.Offset = PageScroll.Offset.WithY(y);
        e.Handled = true;
    }

    private void OnSeatCommit(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox box)
            CommitSeat(box);
    }

    private void CommitSeat(TextBox box)
    {
        if (box.DataContext is TeamRow row && DataContext is LeagueSetupViewModel vm)
            vm.ApplySeat(row);
    }

    private void OnSeatGripPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not LeagueSetupViewModel vm || !vm.CanReorderTeams)
            return;
        if (sender is not Control grip || grip.DataContext is not TeamRow row)
            return;
        if (!e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed)
            return;

        _dragRow = row;
        _dragStart = e.GetPosition(TeamList);
        e.Pointer.Capture(grip);
        grip.PointerMoved += OnSeatGripMoved;
        grip.PointerReleased += OnSeatGripReleased;
        grip.PointerCaptureLost += OnSeatGripCaptureLost;
        e.Handled = true;
    }

    private void OnSeatGripMoved(object? sender, PointerEventArgs e)
    {
        if (_dragRow is null || DataContext is not LeagueSetupViewModel vm)
            return;

        var position = e.GetPosition(TeamList);
        if (Math.Abs(position.Y - _dragStart.Y) < 6)
            return;

        var index = IndexFromY(position.Y);
        if (index >= 0)
            vm.MoveTeamToIndex(_dragRow, index);
    }

    private void OnSeatGripReleased(object? sender, PointerReleasedEventArgs e) => EndDrag(sender as Control);

    private void OnSeatGripCaptureLost(object? sender, PointerCaptureLostEventArgs e) => EndDrag(sender as Control);

    private void EndDrag(Control? grip)
    {
        if (grip is not null)
        {
            grip.PointerMoved -= OnSeatGripMoved;
            grip.PointerReleased -= OnSeatGripReleased;
            grip.PointerCaptureLost -= OnSeatGripCaptureLost;
        }

        _dragRow = null;
    }

    private int IndexFromY(double y)
    {
        var count = TeamList.ItemCount;
        if (count == 0)
            return -1;

        for (var i = 0; i < count; i++)
        {
            if (TeamList.ContainerFromIndex(i) is not Control container)
                continue;
            var origin = container.TranslatePoint(new Point(0, 0), TeamList);
            if (origin is null)
                continue;
            if (y < origin.Value.Y + container.Bounds.Height / 2)
                return i;
        }

        return count - 1;
    }
}
