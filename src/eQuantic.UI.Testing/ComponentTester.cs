using System.Globalization;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;

namespace eQuantic.UI.Testing;

/// <summary>
/// Draws a component the way a browser is given it, with no browser, so an app's tests can drive it
/// and read what it shows: Flutter's <c>WidgetTester</c>, for any .NET test framework.
/// <code>
/// using var tester = new ComponentTester(culture: new CultureInfo("pt-BR"));
/// tester.Pump(new SignupForm());
/// tester.EnterText("Email", "ana@example.com");
/// tester.Tap("Sign up");
/// tester.Pump();
/// Assert.Equal(SemanticRole.StaticText, tester.Find("Welcome, ana@example.com").Role);
/// </code>
/// <para>
/// A FRAME is drawn through the vocabulary's own <see cref="BuildOwner"/>, a live one, as Photon
/// draws: a stateful instance is kept by its position from one frame to the next, <c>OnMount</c> runs
/// when the frame that first built it ends, and a frame builds only what a <c>SetState</c> marked.
/// The frame is lowered by the web's host of that owner, the one the server draws a page through
/// (<see cref="MarkupHost"/>), so <see cref="Html"/> is the markup a browser receives, and
/// <see cref="Semantics"/> is what its screen reader would announce from it.
/// </para>
/// <para>
/// A control is found by the NAME a screen reader announces, the way a person who cannot see the
/// page finds it, and never by a class or a position in the markup: a test that names it so keeps
/// passing while the layout changes, and fails when the label does.
/// </para>
/// </summary>
public sealed class ComponentTester : IDisposable
{
    /// <summary>The rounds a page's data may take to settle: the server's bound
    /// (<c>ServerRenderingService</c>). The server draws the defaults past it; a test fails.</summary>
    private const int MaxPrefetchRounds = 5;

    private readonly MarkupHost _host;
    private readonly WindowSizeClass _sizeClass;
    private VisualNode? _root;
    private HtmlNode? _frame;
    private HtmlNode? _paintServers;
    private AnnouncedTree? _announced;
    private string? _html;
    private bool _disposed;

    /// <summary>A tester for one component and the frames it draws.</summary>
    /// <param name="theme">The theme every component builds with; the SDK's when null, the one
    /// <c>AddUI</c> serves without <c>UseTheme</c>.</param>
    /// <param name="culture">The culture a component formats with and reads its own labels in; the
    /// invariant one when null, so a test reads the same on every machine.</param>
    /// <param name="width">The viewport's width in dp, which decides the size class and which arm of
    /// an adaptive node shows; a desktop's by default.</param>
    /// <param name="density">The density the target asks for.</param>
    /// <param name="services">What a page's prefetch resolves its dependencies from.</param>
    public ComponentTester(IAppTheme? theme = null, CultureInfo? culture = null, float width = 1280f,
        Density density = Density.Comfortable, IServiceProvider? services = null)
    {
        if (!(width > 0)) throw new ArgumentOutOfRangeException(nameof(width), width, "A viewport is wider than nothing.");

        Theme = theme ?? PhotonTheme.Instance;
        Culture = culture ?? CultureInfo.InvariantCulture;
        Width = width;
        Density = density;
        Services = services ?? NoServices.Instance;
        _sizeClass = WindowSizeClasses.FromWidth(width);
        // No font to measure with, as on the server: a component that asks is answered 0, and the
        // markup is the markup the server writes for it.
        _host = new MarkupHost(Theme, 1f, density);
    }

    /// <summary>The theme every component builds with.</summary>
    public IAppTheme Theme { get; }

    /// <summary>The culture every build, lowering and handler the tester runs sees.</summary>
    public CultureInfo Culture { get; }

    /// <summary>The viewport's width in dp.</summary>
    public float Width { get; }

    /// <summary>The density the target asks for.</summary>
    public Density Density { get; }

    /// <summary>What a page's prefetch resolves its dependencies from.</summary>
    public IServiceProvider Services { get; }

    /// <summary>The markup of the current frame, as the web realizer lowered it, followed by the paint
    /// servers its drawings use, which the server declares once for the document beside the page.</summary>
    public string Html => _html ??= HtmlRenderer.RenderNode(Frame)
        + (_paintServers is { } servers ? HtmlRenderer.RenderNode(servers) : "");

    /// <summary>The current frame's markup as a tree, for a test that walks it.</summary>
    public HtmlNode Root => Frame;

    /// <summary>What a screen reader announces in the current frame, in reading order.</summary>
    public IReadOnlyList<Announcement> Semantics => Announced.Announcements;

    /// <summary>Whether a <c>SetState</c> asked for a frame that has not been drawn: Flutter's
    /// <c>hasScheduledFrame</c>. A handler that changed what it shows without asking is the bug this
    /// says out loud.</summary>
    public bool HasScheduledFrame => _host.Owner.HasDirty;

    /// <summary>Mounts <paramref name="root"/> and draws its first frame. A second call replaces the
    /// root: a position whose node is the same kind keeps its instance, as a rebuild does.</summary>
    public void Pump(VisualNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        ThrowIfDisposed();
        _root = root;
        Draw(rebuildAll: false);
    }

    /// <summary>Draws the next frame: builds what a <c>SetState</c> marked since the last one.</summary>
    public void Pump()
    {
        ThrowIfDisposed();
        if (_root is null)
            throw new InvalidOperationException("Nothing is mounted yet: pump a component first, with Pump(root) or PumpPageAsync(page).");
        Draw(rebuildAll: false);
    }

    /// <summary>
    /// Mounts <paramref name="page"/>, loads its server data, and draws its first frame. Every
    /// <see cref="IServerPrefetch"/> component the tree reaches is asked, one at a time and with
    /// <see cref="Services"/>; then the tree builds again, and any component it reaches for the first
    /// time is asked too, until a round reaches none. The instance that loaded is the one drawn.
    /// </summary>
    /// <exception cref="InvalidOperationException">The page still asks for data after the rounds the
    /// server allows; the server would have drawn the defaults.</exception>
    public async Task PumpPageAsync(VisualNode page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ThrowIfDisposed();
        _root = page;

        // The rounds the server runs (ServerDataRounds): which components are asked after each frame,
        // and only the positions that loaded build again, with the instance that loaded.
        var data = new ServerDataRounds(_host);
        var settled = await data.RunAsync(() => Discover(page), async component =>
        {
            using (new CultureScope(Culture))
                await component.PrefetchAsync(Services, cancellationToken).ConfigureAwait(false);
        }, MaxPrefetchRounds).ConfigureAwait(false);

        if (!settled)
            throw new InvalidOperationException(
                $"The page still asks for data after {MaxPrefetchRounds} rounds ({string.Join(", ", data.Pending.Select(component => component.GetType().Name))}): "
                + "the server would draw it with the defaults. Something each round loads builds a component that loads again.");

        Draw(rebuildAll: false);
    }

    /// <summary>Taps the control a screen reader announces as <paramref name="name"/>: its click
    /// handler runs, or the nearest one around it, as a click bubbles. A tap names a CONTROL, so a
    /// run of text or a picture of the same name is not a candidate. Draws nothing; call
    /// <see cref="Pump()"/> to see what it changed.</summary>
    public void Tap(string name) => TapOne(Locate(name, "control",
        (announcement, _) => announcement.Role is not (SemanticRole.StaticText or SemanticRole.Image)));

    /// <summary>Taps the control announced with <paramref name="role"/> and <paramref name="name"/>.</summary>
    public void Tap(SemanticRole role, string name) => TapOne(Locate(name, role.ToString(),
        (announcement, _) => announcement.Role == role));

    /// <summary>
    /// Types <paramref name="text"/> into the text field a screen reader announces as
    /// <paramref name="name"/>, replacing what it held: its change callback runs with the whole text,
    /// as the browser's input handler calls it. Draws nothing; call <see cref="Pump()"/> to see it.
    /// </summary>
    public void EnterText(string name, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var (announcement, _) = Locate(name, "text field",
            (stop, _) => stop.Role is SemanticRole.TextField or SemanticRole.CodeField);
        if (announcement.Disabled) throw new InvalidOperationException($"{announcement} takes no text.");

        // The server's lowering carries no input handler (the runtime binds one), so the field is
        // found where the runtime's handler finds it: the text entry the tree holds under that label.
        var entries = new List<TextEntry>();
        Collect(_host.Owner.Root!, entries, name);
        if (entries.Count != 1)
            throw new InvalidOperationException(entries.Count == 0
                ? $"No text entry is labelled \"{name}\", though {announcement} is announced: a field is typed into "
                  + "through its TextEntry's Label, and a code surface or a sheet takes typing its own way."
                : $"{entries.Count} text entries are labelled \"{name}\".");
        var changed = entries[0].OnChanged
                      ?? throw new InvalidOperationException($"{announcement} has nothing listening for its text.");

        using var culture = new CultureScope(Culture);
        changed(text);
    }

    /// <summary>The one stop announced as <paramref name="name"/>.</summary>
    public Announcement Find(string name) => Locate(name, "stop", static (_, _) => true).Announcement;

    /// <summary>The one stop announced with <paramref name="role"/> and <paramref name="name"/>.</summary>
    public Announcement Find(SemanticRole role, string name) =>
        Locate(name, role.ToString(), (announcement, _) => announcement.Role == role).Announcement;

    /// <summary>Takes the tree down, so every <c>OnUnmount</c> runs: what a test's end is to the
    /// components it drew.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        using var culture = new CultureScope(Culture);
        _host.Owner.UnmountAll();
    }

    private HtmlNode Frame => _frame ?? throw NothingDrawn();

    private AnnouncedTree Announced => _announced ?? throw NothingDrawn();

    private static InvalidOperationException NothingDrawn() =>
        new("Nothing is drawn yet: pump a component first, with Pump(root) or PumpPageAsync(page).");

    private void Draw(bool rebuildAll)
    {
        using var culture = new CultureScope(Culture);
        _host.Build(_root!, _sizeClass, rebuildAll);
        // Inside a render of its own, as the server lowers: the style sink is what knows which of a
        // node's classes hide it, an adaptive arm's gate among them.
        var channels = new RenderChannels(density: Density);
        _frame = _host.Lower(channels).Render();
        _host.EndFrame();
        _paintServers = channels.Gradients.IsEmpty ? null : channels.Gradients.Container().Render();
        _announced = AnnouncedTree.Read(_frame, channels.Styles, Width);
        _html = null;
    }

    /// <summary>
    /// A discovery round: the tree built and lowered as the frame will be, with the server's expansion
    /// scope naming every component it reaches, by the instance its position keeps. Answers the scope,
    /// for <see cref="ServerDataRounds.Next"/> to read.
    /// </summary>
    private ComponentExpansionScope Discover(VisualNode page)
    {
        using var culture = new CultureScope(Culture);
        var scope = new ComponentExpansionScope();
        _host.Build(page, _sizeClass);
        _host.Lower(new RenderChannels(scope, Density));
        return scope;
    }

    private (Announcement Announcement, HtmlNode Node) Locate(string name, string what,
        Func<Announcement, HtmlNode, bool> fits)
    {
        ArgumentNullException.ThrowIfNull(name);
        ThrowIfDisposed();
        var tree = Announced;
        var matches = Enumerable.Range(0, tree.Announcements.Count)
            .Where(index => tree.Announcements[index].Name == name && fits(tree.Announcements[index], tree.NodeAt(index)))
            .ToList();
        if (matches.Count == 1) return (tree.Announcements[matches[0]], tree.NodeAt(matches[0]));

        // The failure lists what IS announced: the test's author learns the name the reader hears,
        // which is the one to write.
        var heard = tree.Announcements.Count == 0
            ? " Nothing is announced."
            : " It announces:\n  " + string.Join("\n  ", tree.Announcements);
        throw new InvalidOperationException((matches.Count == 0
            ? $"No {what} in the frame is announced as \"{name}\"."
            : $"{matches.Count} of the frame's stops are announced as \"{name}\"; name the role to tell them apart.") + heard);
    }

    private void TapOne((Announcement Announcement, HtmlNode Node) stop)
    {
        if (stop.Announcement.Disabled) throw new InvalidOperationException($"{stop.Announcement} is disabled: it cannot be tapped.");
        var handler = ClickOf(stop.Node)
                      ?? throw new InvalidOperationException($"Nothing handles a tap on {stop.Announcement}.");

        using var culture = new CultureScope(Culture);
        if (handler is Action action) action();
        else if (handler.Method.GetParameters().Length == 0) handler.DynamicInvoke();
        else throw new NotSupportedException($"A tap handler of type {handler.GetType()} takes arguments a tap has none of.");
    }

    /// <summary>The click handler a tap on <paramref name="node"/> runs: its own, or the nearest one
    /// around it, as a click bubbles.</summary>
    private Delegate? ClickOf(HtmlNode node)
    {
        for (var at = node; at is not null; at = Announced.ParentOf(at))
        {
            if (at.Events.TryGetValue("click", out var handler) || at.Events.TryGetValue("onclick", out handler))
                return handler;
        }

        return null;
    }

    private static void Collect(Element element, List<TextEntry> entries, string label)
    {
        if (element.Configuration is TextEntry entry && entry.Label == label) entries.Add(entry);
        element.VisitChildren(child => Collect(child, entries, label));
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <summary>A provider that resolves nothing, for a tester given no services.</summary>
    private sealed class NoServices : IServiceProvider
    {
        public static readonly NoServices Instance = new();

        public object? GetService(Type serviceType) => null;
    }
}
