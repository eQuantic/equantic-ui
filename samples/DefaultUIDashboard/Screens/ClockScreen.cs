using eQuantic.UI.Core;
using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// The screen that changes with NOBODY touching it. Every other page here reacts to a tap, a key or
/// a route; this one reacts to time, which until <see cref="IClock"/> the framework could not say.
/// <para>
/// It is also the proof that the lifecycle survives the crossing: the subscription starts in
/// <c>OnMount</c> — which runs after the server-rendered markup HYDRATES — and ends in
/// <c>OnUnmount</c>, so navigating away leaves nothing ticking behind. The first paint is what the
/// server sent (step 0, and the dot on the left), because a server renders one frame and there is
/// no later for a timer to arrive in.
/// </para>
/// </summary>
[Page("/clock", Title = "Time — eQuantic Console")]
public sealed class ClockScreen : StatefulComponent
{
    private static readonly string[] Steps = ["one", "two", "three", "four"];

    private readonly IClock _clock;
    private IDisposable? _tick;
    private int _step;
    private int _seconds;

    public ClockScreen(IClock clock)
    {
        _clock = clock;
    }

    protected override void OnMount()
    {
        // Two subscriptions on purpose: one slow enough to watch, one that proves a second timer is
        // not the first one in disguise.
        _tick = _clock.Every(TimeSpan.FromSeconds(1), () => SetState(() =>
        {
            _seconds++;
            if (_seconds % 2 == 0) _step = (_step + 1) % Steps.Length;
        }));
    }

    protected override void OnUnmount() => _tick?.Dispose();

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var primary = theme.Colors(Variant.Primary);

        // The dot walks the row: the same state, drawn as position instead of as text, so the page
        // is legible from across the room and in a screenshot taken two seconds apart.
        var track = Row(gap: Space.S3, children: [.. Steps.Select((label, index) =>
            Column(gap: Space.S2, cross: CrossAlign.Center, children: [
                Box(new BoxStyle
                {
                    Width = 44,
                    Height = 44,
                    Background = index == _step ? primary.Subtle : theme.SurfaceSubtle,
                    CornerRadius = new CornerRadii(Radius.Full),
                }),
                Text(label, TypeRole.Caption,
                    index == _step ? primary.OnSubtle : theme.TextMuted),
            ]))]);

        // Standalone like the other demo screens: the frame belongs to the payments route.
        return Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
            Padding = EdgeInsets.All(Space.S6),
        },
        Column(gap: Space.S4, children: [
            Text("A screen that advances itself", TypeRole.Heading, theme.TextPrimary),
            Text("No tap, no key, no route change. A component subscribes to time in OnMount "
                + "and lets go in OnUnmount, and the same C# does it on a phone.",
                TypeRole.BodyM, theme.TextSecondary),
            track,
            Text($"{_seconds}s since this page hydrated", TypeRole.BodyM, theme.TextMuted),
        ]));
    }
}
