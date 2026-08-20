using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using FantasyDraftAssistant.App.ViewModels;

namespace FantasyDraftAssistant.App.Controls;

/// <summary>
/// Renders an advisor answer as normal selectable prose, with recognised player names
/// drawn as links. Clicking a name runs <see cref="MentionCommand"/> with that segment.
///
/// Names are ordinary <see cref="Run"/>s rather than embedded buttons so the text wraps
/// and selects like any other paragraph; clicks are resolved by hit-testing the layout.
/// </summary>
public class AiResponseText : SelectableTextBlock
{
    public static readonly StyledProperty<IReadOnlyList<AiResponseSegment>?> SegmentsProperty =
        AvaloniaProperty.Register<AiResponseText, IReadOnlyList<AiResponseSegment>?>(nameof(Segments));

    public static readonly StyledProperty<ICommand?> MentionCommandProperty =
        AvaloniaProperty.Register<AiResponseText, ICommand?>(nameof(MentionCommand));

    private readonly List<(int Start, int End, AiResponseSegment Segment)> _hitRanges = [];
    private int _pressedAt = -1;

    public AiResponseText()
    {
        // A null background makes the control invisible to hit-testing, which would swallow
        // every click on a player name.
        Background ??= Brushes.Transparent;
    }

    public IReadOnlyList<AiResponseSegment>? Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public ICommand? MentionCommand
    {
        get => GetValue(MentionCommandProperty);
        set => SetValue(MentionCommandProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SegmentsProperty)
        {
            DetachCollection(change.OldValue as IReadOnlyList<AiResponseSegment>);
            AttachCollection(change.NewValue as IReadOnlyList<AiResponseSegment>);
            Rebuild();
        }
    }

    private void AttachCollection(IReadOnlyList<AiResponseSegment>? segments)
    {
        if (segments is INotifyCollectionChanged notify)
            notify.CollectionChanged += OnSegmentsChanged;
    }

    private void DetachCollection(IReadOnlyList<AiResponseSegment>? segments)
    {
        if (segments is INotifyCollectionChanged notify)
            notify.CollectionChanged -= OnSegmentsChanged;
    }

    private void OnSegmentsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        _hitRanges.Clear();
        var inlines = new InlineCollection();
        var cursor = 0;

        foreach (var segment in Segments ?? [])
        {
            if (segment.Text.Length == 0)
                continue;

            var run = new Run(segment.Text);
            if (segment.IsPlayer)
            {
                run.Foreground = segment.IsAvailable ? Accent : Muted;
                run.TextDecorations = Underline;
                run.FontWeight = FontWeight.SemiBold;
                _hitRanges.Add((cursor, cursor + segment.Text.Length, segment));
            }

            inlines.Add(run);
            cursor += segment.Text.Length;
        }

        Inlines = inlines;
    }

    // The base class resolves a pointer position to a character index for its own selection,
    // and does it correctly for padding and wrapping. Read that back rather than hit-testing
    // the layout again here — a second hit test disagreed with it and reported every click as
    // outside the text.
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _pressedAt = SelectionStart;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        var released = SelectionStart;
        var wasClick = SelectionStart == SelectionEnd && released == _pressedAt;
        _pressedAt = -1;
        if (!wasClick)
            return;

        if (SegmentAt(released) is { } segment && MentionCommand is { } command && command.CanExecute(segment))
            command.Execute(segment);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_hitRanges.Count == 0)
            return;

        var point = e.GetPosition(this);
        var hit = TextLayout.HitTestPoint(new Point(point.X - Padding.Left, point.Y - Padding.Top));
        Cursor = new Cursor(SegmentAt(hit.TextPosition) is not null
            ? StandardCursorType.Hand
            : StandardCursorType.Ibeam);
    }

    private AiResponseSegment? SegmentAt(int index)
    {
        foreach (var (start, end, segment) in _hitRanges)
        {
            if (index >= start && index < end)
                return segment;
        }

        return null;
    }

    private static IBrush Accent { get; } = new SolidColorBrush(Color.Parse("#E8A317"));
    private static IBrush Muted { get; } = new SolidColorBrush(Color.Parse("#8B97A8"));

    private static TextDecorationCollection Underline { get; } =
        [new TextDecoration { Location = TextDecorationLocation.Underline }];
}
