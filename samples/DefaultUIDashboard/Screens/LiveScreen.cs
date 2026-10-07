using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// A screen the SERVER moves. The clock screen changes with the time the browser keeps; this one
/// changes with what a service on the server publishes, to a topic the page subscribes to in
/// <c>OnMount</c> and lets go of in <c>OnUnmount</c> (#291).
/// <para>
/// One stream carries every topic the page holds, opened by the first subscription. Stop the server
/// and start it again: the state reads reconnecting, then connected, with the id of the last quote
/// that arrived, and the quotes carry on.
/// </para>
/// </summary>
[Page("/live", Title = "Live — eQuantic Console")]
public sealed class LiveScreen : StatefulComponent
{
    /// <summary>The topic, declared once: this page subscribes to it, and the server's publisher
    /// (<see cref="LiveQuotePublisher"/>) writes to it.</summary>
    public static readonly ServerTopic<LiveQuote> Quotes = new("console:quotes");

    private readonly IServerEvents _events;
    private IDisposable? _quotes;
    private IDisposable? _connection;
    private LiveQuote? _last;
    private int _received;
    private ServerConnectionState _state = ServerConnectionState.Disconnected;
    private string? _lastEventId;
    private bool _refused;

    /// <summary>Whether the compact drawer is up — page state wherever the page is.</summary>
    private bool _navOpen;

    public LiveScreen(IServerEvents events)
    {
        _events = events;
    }

    protected override void OnMount()
    {
        _connection = _events.OnConnectionChanged(connection => SetState(() =>
        {
            _state = connection.State;
            _lastEventId = connection.LastEventId;
        }));
        _quotes = _events.Subscribe(Quotes,
            quote => SetState(() =>
            {
                _last = quote;
                _received++;
            }),
            _ => SetState(() => _refused = true));
    }

    protected override void OnUnmount()
    {
        _quotes?.Dispose();
        _connection?.Dispose();
    }

    public override VisualNode Build(ComponentContext context) =>
        ConsoleShell.Frame(context.Theme, "/live", "Live", Content(context),
            _navOpen, () => SetState(() => _navOpen = !_navOpen));

    private VisualNode Content(ComponentContext context)
    {
        var theme = context.Theme;
        var quote = _last is null
            ? "Waiting for the first quote"
            : $"{_last.Symbol} {_last.Price} (#{_last.Sequence})";
        var connection = _lastEventId is null
            ? $"{_received} received, {Describe(_state)}"
            : $"{_received} received, {Describe(_state)}, last event {_lastEventId}";

        return Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
            Padding = EdgeInsets.All(Space.S6),
        },
        Column(gap: Space.S4, children: [
            Text("A screen the server moves", TypeRole.Heading, theme.TextPrimary),
            Text("A service on the server publishes a quote every second to a typed topic. The page "
                + "subscribes in OnMount, over one stream for every topic it holds, and each quote "
                + "arrives as the record the server wrote.", TypeRole.BodyM, theme.TextSecondary),
            Text(quote, TypeRole.Title, theme.TextPrimary),
            Text(_refused ? "The server refused the topic." : connection, TypeRole.BodyM, theme.TextMuted),
        ]));
    }

    private static string Describe(ServerConnectionState state) => state switch
    {
        ServerConnectionState.Connecting => "connecting",
        ServerConnectionState.Connected => "connected",
        ServerConnectionState.Reconnecting => "reconnecting",
        _ => "not connected",
    };
}
