namespace eQuantic.UI.Primitives;

/// <summary>
/// The unit type: a pass that carries nothing down, or hands nothing back.
///
/// <para>
/// <c>IVisualNodeVisitor</c> is generic in both directions so one interface can serve a lowering,
/// a measurement and a semantics walk. Most passes want only one of them, and C# has no
/// <c>void</c> a type argument can hold — so this is it, for the same reason Rust and F# have one.
/// A semantics walk is <c>IVisualNodeVisitor&lt;string, Nothing&gt;</c>: a path goes down, the list
/// it appends to is the visitor's own, and nothing comes back.
/// </para>
///
/// <para>A struct with no fields, so it costs nothing to pass and nothing to return.</para>
///
/// <para>
/// HOST ONLY. A component BUILDS a tree; VISITING one is what a realizer, a layout pass or a
/// semantics walk does, and all of those live above the page — so nothing a page can write names
/// this. It carries the fence rather than an entry on the runtime's exception list, for the reason
/// #135 gave: a list entry is a note saying "no page does this" where the attribute is the BUILD
/// saying no, and the list cannot enforce the absence it describes.
/// </para>
/// </summary>
[ServerOnly]
public readonly struct Nothing
{
    /// <summary>The one value there is.</summary>
    public static readonly Nothing Value = default;
}
