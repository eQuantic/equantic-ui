namespace eQuantic.UI.Primitives;

/// <summary>
/// POINTER-PRESENCE callback (spec S5's programmable side): fires <see cref="OnChanged"/> with
/// <c>true</c> when the pointer enters the child's bounds and <c>false</c> when it leaves — the
/// primitive under hover-intent menus (open on hover, grace-period close), hover prefetch and rich
/// hover cards. Layout-transparent; never fires on touch (there is no hover to track). Distinct
/// from <see cref="BoxStyle.Hover"/> (declarative style diff) and from <see cref="Pressable"/>
/// (press semantics) — presence COMPOSES with both by nesting. Web attaches
/// mouseenter/mouseleave; native rides the host's hover pipeline (the same one Style.Hover uses).
/// </summary>
public sealed class Hoverable : SingleChildNode
{
    public override string NodeKind => "hoverable";

    public Hoverable(VisualNode child, Action<bool> onChanged)
        : base(child)
    {
        OnChanged = onChanged;
    }


    /// <summary><c>true</c> = pointer entered, <c>false</c> = pointer left.</summary>
    public Action<bool> OnChanged { get; init; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
