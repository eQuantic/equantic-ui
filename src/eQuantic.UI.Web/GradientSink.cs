using System.Threading;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// Every gradient the page paints with, declared ONCE for the document — the paint-server twin of
/// <see cref="StyleSink"/>, armed by the SSR pipeline around a render.
///
/// <para>
/// The id stays what it was: the hash of the run itself, so the server and the client twin arrive
/// at the same string without agreeing on a counter. What moves is WHERE the definition lives. A
/// <c>&lt;defs&gt;</c> inside each drawing's own <c>&lt;svg&gt;</c> deduplicates within that drawing
/// and nowhere else, and an AdaptiveNode puts every arm in the document: the same artwork appeared
/// N times with N identical defs, and <c>url(#id)</c> binds to the FIRST in document order — which
/// is the arm the media query hides. A hidden ancestor makes a paint server unusable, so the shape
/// rendered with no fill at all, on the layout where it was supposed to be visible.
/// </para>
/// <para>
/// Without an ambient sink the realizer keeps writing the defs inline. A drawing rendered on its
/// own, a unit test, a component realized outside a page — none of them have a document to put a
/// container in, and all of them have to keep working.
/// </para>
/// </summary>
public sealed class GradientSink
{
    private static readonly AsyncLocal<GradientSink?> _ambient = new();

    /// <summary>The render-scoped sink, or null outside an SSR render.</summary>
    public static GradientSink? Ambient
    {
        get => _ambient.Value;
        set => _ambient.Value = value;
    }

    /// <summary>The id the container carries, and the one the client twin looks for before it
    /// creates a second one — two containers means two defs with one id, which is this bug again
    /// through the other door.</summary>
    public const string ContainerId = "eq-vectors";

    private readonly Dictionary<string, VectorPaint> _runs = new(StringComparer.Ordinal);

    /// <summary>Content-addressed, so the same run from two drawings is one entry.</summary>
    public void Add(string id, VectorPaint paint) => _runs.TryAdd(id, paint);

    public bool IsEmpty => _runs.Count == 0;

    /// <summary>
    /// The document's own paint servers, as an element for the BODY — never the head, where an svg
    /// is not allowed, and never inside <c>#app</c>, whose children the reconciler owns.
    /// <para>
    /// Zero-sized and out of flow rather than <c>display:none</c>: a paint server inside a
    /// display:none subtree is not rendered, and referencing it paints nothing. That is exactly the
    /// failure this replaces, so the container must not reintroduce it — <c>aria-hidden</c> keeps it
    /// out of the accessibility tree without taking it out of the render tree.
    /// </para>
    /// </summary>
    public Core.HtmlElement Container() => WebRealizer.GradientContainer(_runs);
}
