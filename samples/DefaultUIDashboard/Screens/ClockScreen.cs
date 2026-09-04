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

    /// <summary>Whether the compact drawer is up — page state wherever the page is.</summary>
    private bool _navOpen;

    /// <summary>
    /// The frame, then this screen inside it. Every route wraps itself: there is no separate
    /// layout, and none is needed — the reconciler matches the frame position for position across
    /// a navigation, so the sidebar is never rebuilt and only the middle changes.
    /// </summary>
    public override VisualNode Build(ComponentContext context) =>
        ConsoleShell.Frame(context.Theme, "/clock", "Time", Content(context),
            _navOpen, () => SetState(() => _navOpen = !_navOpen));

    private VisualNode Content(ComponentContext context)
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
            Gap(Space.S2),
            new Pulse(),
        ]));
    }
}

/// <summary>
/// The same reaction to time, from a component NOBODY hands anything to. The page above takes its
/// clock in a constructor because the router fills it; this one is composed as <c>new Pulse()</c> in
/// the middle of a tree, so it asks for the capability itself — in <c>OnMount</c>, which has no
/// context, which is exactly why <c>UiComponent.GetService</c> exists.
/// <para>
/// If it ever stops blinking while the row above keeps walking, the capability stopped being
/// reachable from the hook, and no unit test would have to be believed over the page.
/// </para>
/// </summary>
public sealed class Pulse : StatefulComponent
{
    private IDisposable? _tick;
    private bool _lit;

    protected override void OnMount() =>
        _tick = GetService<IClock>()?.Every(TimeSpan.FromMilliseconds(600),
            () => SetState(() => _lit = !_lit));

    protected override void OnUnmount() => _tick?.Dispose();

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var primary = theme.Colors(Variant.Primary);

        return Row(gap: Space.S2, cross: CrossAlign.Center, children: [
            Box(new BoxStyle
            {
                Width = 12,
                Height = 12,
                Background = _lit ? primary.Base : theme.SurfaceSubtle,
                CornerRadius = new CornerRadii(Radius.Full),
            }),
            Text("a nested section, on the clock it asked for itself",
                TypeRole.Caption, theme.TextMuted),
        ]);
    }
}
