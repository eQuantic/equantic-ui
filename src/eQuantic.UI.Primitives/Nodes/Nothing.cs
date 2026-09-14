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
/// </summary>
public readonly struct Nothing
{
    /// <summary>The one value there is.</summary>
    public static readonly Nothing Value = default;
}
