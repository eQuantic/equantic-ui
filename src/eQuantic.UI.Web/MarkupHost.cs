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
/// slot that keeps it, and a layer not lowered leaves the tree when the frame ends. Of an adaptive node,
/// the arm its element holds lowers from its positions and the others as a tree drawn once does.
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
        Owner = live ? new BuildOwner(_measurer.Build) : BuildOwner.Structural(_measurer.Build);
    }

    /// <summary>A structural host: a tree built for a request and never drawn again, whose owner calls
    /// no lifecycle hook. What the server keeps for a page.</summary>
    /// <param name="theme">The theme every component of the tree builds and lowers with.</param>
    /// <param name="typeScale">The text scale the tree builds and lowers at.</param>
    /// <param name="density">The density the tree builds and lowers at.</param>
    public static MarkupHost Structural(IAppTheme theme, float typeScale = 1f, Density density = Density.Comfortable) =>
        new(theme, typeScale, density, live: false);

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
    /// <param name="sizeClass">The window size class an adaptive node's element resolves its arm for.
    /// The markup is the same for every one, since every arm lowers behind its own gate; it decides
    /// which arm keeps its instances from one frame to the next.</param>
    /// <param name="rebuildAll">Whether every component builds, not only the marked ones.</param>
    /// <returns>The root's position.</returns>
    public Element Build(VisualNode root, WindowSizeClass sizeClass, bool rebuildAll = false) =>
        Owner.Build(root, _context, sizeClass, rebuildAll);

    /// <summary>
    /// Lowers the frame <see cref="Build"/> just built. With <paramref name="styles"/>, every element's
    /// style becomes atomic classes collected into the sink, as <see cref="WebRealizer.Lower"/> does.
    /// </summary>
    /// <exception cref="InvalidOperationException">Nothing was built yet.</exception>
    public HtmlElement Lower(StyleSink? styles = null)
    {
        var root = Owner.Root ?? throw new InvalidOperationException("Nothing was built yet: call Build before Lower.");
        var lowered = new WebLoweringVisitor(_context, _measurer, new ElementFrame(root))
                          .Lower(root.Configuration, horizontalAxis: null)
                      ?? new RealizedElement("span");
        if (styles != null)
            StyleAtomizer.AtomizeTree(lowered, ThemeVarMap.For(Theme), styles);
        return lowered;
    }

    /// <summary>Ends the frame: the layers it did not lower leave the tree, and a live owner runs every
    /// <c>OnUnmount</c> before any <c>OnMount</c>.</summary>
    public void EndFrame() => Owner.EndFrame();
}
