using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>The two words that change how many stops a subtree makes, rather than what one of them is.</summary>
internal sealed partial class WebLoweringVisitor
{
    /// <summary>
    /// A SUBTREE HEARD AS ONE STOP (TS twin: lowerMergeSemantics): a host, and the child lowered inside
    /// it, then read by <see cref="MergedReading"/>, which names the one stop by the parts it hides. Photon's
    /// semantics walk says the same stop. The host takes the child's fill and cap, as the
    /// <see cref="LowerGroup"/> host does: it is the group a reader outlines when nothing in it is a control.
    /// </summary>
    private HtmlElement LowerMergeSemantics(MergeSemantics merge)
    {
        var host = SemanticsHost(merge.Child, new Dictionary<string, string>(), null, takesPointer: false);
        var scope = LoweringPath.Scope(_path);
        if (Lower(merge.Child, null, LoweringPath.Child) is { } child)
        {
            host.Children.Add(child);
            MergedReading.Merge(host, child, $"eq-merge-{scope}");
        }
        return host;
    }

    /// <summary>
    /// A SUBTREE A READER PASSES OVER (TS twin: lowerExcludeSemantics): a host marked
    /// <c>aria-hidden</c> while it excludes, and a plain one when it does not, so the shape is the same
    /// either way and a change of <see cref="ExcludeSemantics.Excluding"/> writes one attribute.
    /// </summary>
    private HtmlElement LowerExcludeSemantics(ExcludeSemantics exclude)
    {
        var host = SemanticsHost(exclude.Child, new Dictionary<string, string>(), null, takesPointer: false);
        if (exclude.Excluding) host.RawAttributes!["aria-hidden"] = "true";
        if (Lower(exclude.Child, null, LoweringPath.Child) is { } child) host.Children.Add(child);
        return host;
    }

    /// <summary>
    /// THE ONE HOST of the six wrappers that put an element between a parent and its child to carry
    /// semantics (TS twin: semanticsHost): <c>Progress</c>, <c>Adjustable</c>, <c>LiveRegion</c>,
    /// <c>Group</c>, <c>MergeSemantics</c> and <c>ExcludeSemantics</c>. It is the web's stand-in for
    /// Flutter's <c>Semantics</c> proxy box, which takes exactly its child's size, and it writes the
    /// rule once: the box a reader outlines is the box it names, so the host takes the child's width
    /// contract (<c>fit-content</c>, or <c>100%</c> when the child fills), the child's cap as
    /// <c>max-width</c>, and <c>100%</c> height when the child fills down. A wrapper that took the
    /// width and dropped the rest is the half-contract that made the Link diverge, and that
    /// <c>RoleBearingHostBoundsTests</c> keeps from coming back (#239, #241); a seventh wrapper comes
    /// through here rather than writing the lines by hand.
    /// <para>
    /// What differs is the role's <paramref name="attributes"/> (none for the two that bear no role),
    /// the accessible <paramref name="label"/>, written right after them when it is not empty, and
    /// whether the host <paramref name="takesPointer"/> (<c>pointer-events: auto</c>), which the one wrapper
    /// that is a tab stop and operated says. The child is not lowered here, and what a wrapper adds after the host stays with it.
    /// </para>
    /// </summary>
    private RealizedElement SemanticsHost(
        VisualNode child, Dictionary<string, string> attributes, string? label, bool takesPointer)
    {
        var fills = Fills(child);
        var cap = CapsAt(child);
        var host = new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                Width = fills.Width ? "100%" : "fit-content",
                MaxWidth = Size(cap),
                PointerEvents = takesPointer ? "auto" : null,
                Height = fills.Height ? "100%" : null,
            },
            RawAttributes = attributes,
        };
        if (label is { Length: > 0 }) attributes["aria-label"] = label;
        return host;
    }
}
