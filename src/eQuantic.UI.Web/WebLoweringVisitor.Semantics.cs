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
        var host = SemanticsHost(merge.Child);
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
        var host = SemanticsHost(exclude.Child);
        if (exclude.Excluding) host.RawAttributes!["aria-hidden"] = "true";
        if (Lower(exclude.Child, null, LoweringPath.Child) is { } child) host.Children.Add(child);
        return host;
    }

    /// <summary>The host both write: layout-transparent, by the role-bearing wrappers' one rule.</summary>
    private RealizedElement SemanticsHost(VisualNode child)
    {
        var fills = Fills(child);
        var cap = CapsAt(child);
        return new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                Width = fills.Width ? "100%" : "fit-content",
                MaxWidth = Size(cap),
                Height = fills.Height ? "100%" : null,
            },
            RawAttributes = new Dictionary<string, string>(),
        };
    }
}
