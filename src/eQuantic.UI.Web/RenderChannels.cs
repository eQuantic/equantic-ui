using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// What one render of a page collects and reads for the web (#1404): the atomic style rules, the paint
/// servers, the faces its text names, the scope that names each component the render expands, the
/// density the browser reported, and the link policy that prefixes an in-app destination with the
/// request's culture. They were six <c>AsyncLocal</c>s armed around a render, which any code running in
/// the request's flow could read or overwrite. One render's are one object now, handed to the lowering
/// as a parameter (docs/ELEMENT-TREE-PLAN.md, 3.5).
/// <para>
/// ONE READER finds it in an ambient instead, and it is the declared exception: the escape hatch's
/// bridge (<see cref="VisualNodeComponent"/>), which an <see cref="HtmlElement"/>'s <c>Render()</c>
/// reaches from anywhere in a page drawn by hand, through a method that takes no parameter. Changing
/// that method would change the escape hatch's whole API, which crosses to the browser too. So a server
/// rendering such a page arms the render's channels around it (<see cref="BridgeWith"/>), and a bridge
/// reads them there (<see cref="Bridge"/>).
/// </para>
/// </summary>
public sealed class RenderChannels
{
    private static readonly AsyncLocal<RenderChannels?> Bridging = new();

    /// <summary>The channels of a render, with new sinks, the scope it expands components into, the
    /// density the browser reported, the link policy of the request and the error boundary of its tree.</summary>
    public RenderChannels(ComponentExpansionScope? expansion = null, Density density = Density.Comfortable,
        Func<string, string>? linkPolicy = null, ComponentBoundary? boundary = null)
    {
        Expansion = expansion;
        Density = density;
        LinkPolicy = linkPolicy;
        Boundary = boundary ?? new ComponentBoundary();
    }

    /// <summary>The error boundary of the tree the render draws (#1452): a write-once bridge inside an
    /// escape-hatch page expands through it, so what fails there is reported and counted with the page.</summary>
    public ComponentBoundary Boundary { get; }

    /// <summary>The atomic rules the render's markup references, which the server writes beside it.</summary>
    public StyleSink Styles { get; } = new();

    /// <summary>The paint servers the render's drawings use, declared once for the document.</summary>
    public GradientSink Gradients { get; } = new();

    /// <summary>The families with files the render's text names.</summary>
    public FaceSink Faces { get; } = new();

    /// <summary>The scope that names each component the render expands, and hands back what an earlier
    /// round loaded for it; null when the render loads nothing.</summary>
    public ComponentExpansionScope? Expansion { get; }

    /// <summary>The density the browser reported, which every bridge of the render is built at.</summary>
    public Density Density { get; }

    /// <summary>
    /// What an in-app destination becomes on the way into an href in this render.
    /// <para>
    /// It exists for one policy, the language prefix, and it is the render's rather than the link node's
    /// because the alternative is asking every author to remember it on every link. A site with two
    /// hundred hrefs has two hundred chances to forget, and forgetting is silent: the link works, it
    /// just leaves the language behind.
    /// </para>
    /// <para>
    /// It stays on the WEB rather than moving down with the route, because it is not neutral: the
    /// server's culture routes install it, and Photon's realizer reads <c>Link.Destination</c> raw.
    /// That is a fence rather than a gap today, since nothing arms a policy on that target, but the day
    /// one becomes neutral, the native realizer owes it too.
    /// </para>
    /// </summary>
    public Func<string, string>? LinkPolicy { get; }

    /// <summary>
    /// The destination an href carries: the policy's answer, or the destination untouched when the
    /// render has no policy. Only APP-INTERNAL rooted paths are offered to it: <c>//cdn</c>,
    /// <c>https://</c>, <c>#anchor</c> and <c>mailto:</c> belong to someone else. Null when the
    /// destination's scheme is one an href never carries (<see cref="LinkScheme"/>), and the link is
    /// written with no href at all (#1286).
    /// </summary>
    public string? ResolveDestination(string destination) =>
        Resolve(destination, LinkPolicy);

    /// <summary>The same answer, for a lowering with no render around it.</summary>
    internal static string? Resolve(string destination, Func<string, string>? policy) =>
        !LinkScheme.Allows(destination) ? null
        : policy is not null
          && destination.Length > 0
          && destination[0] == '/'
          && !destination.StartsWith("//", StringComparison.Ordinal)
            ? policy(destination)
            : destination;

    /// <summary>The channels of the escape-hatch page being rendered, for the bridges inside it; null
    /// outside such a render.</summary>
    internal static RenderChannels? Bridge => Bridging.Value;

    /// <summary>Arms <paramref name="channels"/> for the bridges of an escape-hatch page rendered while
    /// the returned scope lives, and puts back what was armed before when it is disposed.</summary>
    public static IDisposable BridgeWith(RenderChannels channels)
    {
        ArgumentNullException.ThrowIfNull(channels);
        var previous = Bridging.Value;
        Bridging.Value = channels;
        return new Restore(previous);
    }

    private sealed class Restore(RenderChannels? previous) : IDisposable
    {
        public void Dispose() => Bridging.Value = previous;
    }
}
