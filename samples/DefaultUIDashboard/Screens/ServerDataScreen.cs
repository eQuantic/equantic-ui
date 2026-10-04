using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// Values only the server can know, drawn by the browser as well. A prefetch fills two
/// auto-properties, and a child prefetches into the primary-constructor parameter it captured: the two
/// shapes whose values drew on the server and vanished on hydration (#509, #510).
/// <para>
/// The page source holds the server's values, and the first build after hydration has to show the
/// same ones; so does arriving here by a link from another screen, which loads them without a
/// reload. A value that falls back to its default a moment after the page appears is the defect this
/// screen exists to show.
/// </para>
/// </summary>
[Page("/server-data", Title = "Server data — eQuantic Console")]
public sealed class ServerDataScreen : StatelessComponent, IServerPrefetch
{
    /// <summary>The server's process, which the browser has no way to know.</summary>
    public int ProcessId { get; private set; }

    /// <summary>The runtime the server runs on.</summary>
    public string Runtime { get; private set; } = "";

    [ServerOnly]
    public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ProcessId = Environment.ProcessId;
        Runtime = $".NET {Environment.Version}";
        return Task.CompletedTask;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        // A stateless page owns no drawer state, so the frame gets a closed one, as the ticker's does.
        return ConsoleShell.Frame(theme, "/server-data", "Server data",
            Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Height = SizeValue.Fill,
                Background = theme.Background,
                Padding = EdgeInsets.All(Space.S6),
            },
            Column(gap: Space.S4, children: [
                Text("Values only the server knows", TypeRole.Heading, theme.TextPrimary),
                Text("They are in the page source. They must still be here once the page hydrates, and "
                    + "after arriving from another screen by a link.", TypeRole.BodyM, theme.TextSecondary),
                Text($"Server process {ProcessId}", TypeRole.BodyL, theme.TextPrimary),
                Text($"Running on {Runtime}", TypeRole.BodyL, theme.TextPrimary),
                new ServerStamp("Rendered"),
            ])),
            navOpen: false, onToggleNav: () => { });
    }
}
