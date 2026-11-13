using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// An element tree whose frames the web realizer lowers to markup, kept for as long as the tree is
/// drawn: the web's host of a <see cref="BuildOwner"/> (docs/ELEMENT-TREE-PLAN.md 3.6), as Photon's
/// host is the native one. The server keeps a STRUCTURAL one for a request, so a page's prefetch rounds
/// are frames of one tree and the instance that loaded is the instance drawn. A component test keeps a
/// LIVE one, which runs the lifecycle hooks a browser runs.
/// <para>
/// A frame is <see cref="Build"/>, <see cref="Lower"/> and <see cref="EndFrame"/>. A component is
/// lowered from what its position built, by the instance the position keeps; a layer (an overlay's
/// child, an anchored panel) is shown to the owner before it lowers, so what it holds builds in the
/// slot that keeps it, and a layer not lowered leaves the tree when the frame ends. Every arm of an
/// adaptive node is a position, as the web draws them all behind their gates.
/// </para>
/// <para>
/// Its components build through <see cref="ComponentBoundary"/> with a measurer that has no font, as
/// every web lowering does, and the host remembers which of them asked it: hydration's unmeasured mark
/// is per component, and in a frame the owner built every component before any lowered.
/// </para>
/// <para>
/// <em>How does Flutter solve it?</em> The binding keeps the <c>BuildOwner</c> and the root element,
/// and a frame is <c>buildScope</c> then the pipeline's layout and paint, then
/// <c>finalizeTree</c>, which unmounts what left: the same three steps.
/// </para>
/// </summary>
public sealed class MarkupHost
{
    private readonly FontlessMeasurer _measurer = new();
    private readonly ComponentContext _context;

    /// <summary>A live host: its owner calls the lifecycle hooks, and a <c>SetState</c> marks a
    /// position for the next frame.</summary>
    /// <param name="theme">The theme every component of the tree builds and lowers with.</param>
    /// <param name="typeScale">The text scale the tree builds and lowers at.</param>
    /// <param name="density">The density the tree builds and lowers at.</param>
    public MarkupHost(IAppTheme theme, float typeScale = 1f, Density density = Density.Comfortable)
        : this(theme, typeScale, density, live: true)
    {
    }

    private MarkupHost(IAppTheme theme, float typeScale, Density density, bool live)
    {
        ArgumentNullException.ThrowIfNull(theme);
        Theme = theme;
        _context = new ComponentContext(theme, typeScale, density, _measurer.Measure);
        // The web draws every arm of an adaptive node behind its own media rule, so each is a position.
        Owner = live
            ? new BuildOwner(BuildAt, holdsEveryArm: true)
            : BuildOwner.Structural(BuildAt, holdsEveryArm: true);
    }

    /// <summary>A structural host: a tree built for a request and never drawn again, whose owner calls
    /// no lifecycle hook. What the server keeps for a page.</summary>
    /// <param name="theme">The theme every component of the tree builds and lowers with.</param>
    /// <param name="typeScale">The text scale the tree builds and lowers at.</param>
    /// <param name="density">The density the tree builds and lowers at.</param>
    public static MarkupHost Structural(IAppTheme theme, float typeScale = 1f, Density density = Density.Comfortable) =>
        new(theme, typeScale, density, live: false);

    /// <summary>
    /// The host of a write-once PAGE drawn for a request: structural, with the page component at its
    /// root. The browser builds a page's own component by itself and only what it builds through an
    /// owner, so a position's server data key is measured below the page (<see cref="ElementFrame.KeyOf"/>).
    /// </summary>
    /// <param name="theme">The theme every component of the tree builds and lowers with.</param>
    /// <param name="typeScale">The text scale the tree builds and lowers at.</param>
    /// <param name="density">The density the tree builds and lowers at.</param>
    public static MarkupHost ForPage(IAppTheme theme, float typeScale = 1f, Density density = Density.Comfortable) =>
        new(theme, typeScale, density, live: false) { KeyDepth = 1 };

    /// <summary>
    /// A tree drawn ONCE inside a render (#1404): an escape-hatch page's bridge, through a structural
    /// host of its own, so its components have positions and each is named by where it stands, as the
    /// browser names it (#1477), and what an earlier round loaded is put back on the fresh instance at
    /// that position before it builds. Its atomic rules, density, expansion scope, gradients, faces and
    /// link policy are the render's.
    /// </summary>
    internal static HtmlElement DrawOnce(VisualNode node, IAppTheme theme, float typeScale, RenderChannels channels)
    {
        var host = new MarkupHost(theme, typeScale, channels.Density, live: false)
        {
            Tree = channels.NextTree(),
            PutBackFrom = channels.Expansion,
        };
        host.Build(node);
        var lowered = host.Lower(channels);
        host.EndFrame();
        return lowered;
    }

    /// <summary>Which tree of its render this host draws, in the order the render draws them: what its
    /// components' server data keys start with.</summary>
    internal int Tree { get; init; }

    /// <summary>The depth its components' server data keys are measured from (<see cref="ForPage"/>).</summary>
    internal int KeyDepth { get; private init; }

    /// <summary>What earlier rounds loaded, put back onto each fresh instance before it builds: set for
    /// a tree drawn anew every round, an escape-hatch page's bridge.</summary>
    internal ComponentExpansionScope? PutBackFrom { get; init; }

    /// <summary>A component's build, through the measurer, after what an earlier round loaded for its
    /// position is back on it.</summary>
    private VisualNode BuildAt(UiComponent component, ComponentContext context, int depth)
    {
        if (PutBackFrom is { } scope && Owner.Building is { } position)
            scope.PutBack(component, ElementFrame.KeyAt(position, Tree, KeyDepth));
        return _measurer.Build(component, context, depth);
    }

    /// <summary>The theme the tree builds and lowers with.</summary>
    public IAppTheme Theme { get; }

    /// <summary>The owner of the tree: what a host marks a position through
    /// (<see cref="ComponentElement.MarkNeedsBuild"/>) and walks it from (<see cref="BuildOwner.Root"/>).</summary>
    public BuildOwner Owner { get; }

    /// <summary>
    /// The build phase of a frame: the first builds <paramref name="root"/>, a later one builds what
    /// was marked since, or every component with <paramref name="rebuildAll"/>.
    /// </summary>
    /// <param name="root">The tree's root node.</param>
    /// <param name="rebuildAll">Whether every component builds, not only the marked ones.</param>
    /// <returns>The root's position.</returns>
    /// <remarks>No window size class is asked for: the owner holds every arm of an adaptive node, as the
    /// web draws them all behind their gates, so no arm is resolved. The one handed on is never read.</remarks>
    public Element Build(VisualNode root, bool rebuildAll = false) =>
        Owner.Build(root, _context, WindowSizeClass.Expanded, rebuildAll);

    /// <summary>
    /// Lowers the frame <see cref="Build"/> just built. With a render's <paramref name="channels"/>, every
    /// element's style becomes atomic classes collected into its style sink, as <see cref="WebRealizer.Lower(VisualNode, IAppTheme, float, StyleSink?, Density)"/>
    /// does, and the expansion scope, the gradients, the faces and the link policy are the render's (#1404).
    /// </summary>
    /// <exception cref="InvalidOperationException">Nothing was built yet.</exception>
    public HtmlElement Lower(RenderChannels? channels = null)
    {
        var root = Owner.Root ?? throw new InvalidOperationException("Nothing was built yet: call Build before Lower.");
        var lowered = new WebLoweringVisitor(_context, _measurer, new ElementFrame(root, Tree, KeyDepth), channels)
                          .Lower(root.Configuration, horizontalAxis: null)
                      ?? new RealizedElement("span");
        if (channels is not null)
            StyleAtomizer.AtomizeTree(lowered, ThemeVarMap.For(Theme), channels.Styles);
        return lowered;
    }

    /// <summary>Ends the frame: the layers it did not lower leave the tree, and a live owner runs every
    /// <c>OnUnmount</c> before any <c>OnMount</c>.</summary>
    public void EndFrame() => Owner.EndFrame();
}
