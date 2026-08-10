using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// Pull a list past its top to refresh it (spec B22). The indicator is REVEALED by the pull rather
/// than pushed by it: the content slides down over a spinner that was always there, which is why
/// the gesture reads as uncovering something instead of stretching it.
/// <para>
/// CONTROLLED: <see cref="Refreshing"/> is the caller's state. While it is true the content stays
/// held down at the indicator's height — the work is visibly in flight — and the caller lowers it
/// when the fetch returns. <see cref="OnRefresh"/> fires once, on the release that passed the
/// threshold; a pull that falls short springs back and asks for nothing.
/// </para>
/// </summary>
public sealed class PullToRefresh : StatelessComponent
{
    /// <summary>How far the content must travel before the release counts as a request.</summary>
    public const float Threshold = 64;

    public PullToRefresh(VisualNode child, Action? onRefresh = null)
    {
        Child = child;
        OnRefresh = onRefresh;
    }

    public VisualNode Child { get; init; }
    public Action? OnRefresh { get; init; }

    public bool Refreshing { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        var indicator = new Row(gap: 0)
        {
            Width = SizeValue.Fill,
            Height = Threshold,
            Main = MainAlign.Center,
            Cross = CrossAlign.Center,
        };
        indicator.Add(new Spinner(IconSize.Dense, theme.Colors(Variant.Primary).Base));

        var content = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Background = theme.Background,
        }, Child);

        var stack = new Stack();
        stack.Add(new Positioned(indicator, top: 0, start: 0, end: 0));
        stack.Add(new Draggable(content, OnReleased)
        {
            Axis = DragAxis.Vertical,
            Min = 0,
            Max = Threshold,
            // Held down while the work is in flight — the spinner stays uncovered until it lands.
            // IN THE FLOW nothing can be pulled, so the resting state is the one that SHOWS what
            // this component is: the indicator, uncovered (see InFlow).
            RestOffset = Refreshing || context.InFlow ? Threshold : 0,
        });

        return new Box(new BoxStyle { Width = SizeValue.Fill, Clip = true }, stack);
    }

    private void OnReleased(float offset)
    {
        if (offset >= Threshold) OnRefresh?.Invoke();
    }
}
