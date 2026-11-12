namespace eQuantic.UI.Primitives;

/// <summary>
/// A theme for a subtree: the vocabulary's own inherited value (docs/ELEMENT-TREE-PLAN.md 3.4), after
/// Flutter's <c>Theme</c>. A component below it reads <see cref="ComponentContext.Theme"/> as this
/// node's <see cref="Value"/>, and builds again when a new one replaces it; a component with none above
/// it reads the theme of its frame, which the host hands (#1395).
/// <para>
/// Nothing in the SDK places one yet: a host's frame is the app's theme, so every slot, key and
/// server-data name stays where it was. A part of the screen that draws in another theme (an inverse
/// panel, a branded section) wraps itself in one (#1387).
/// </para>
/// </summary>
public sealed class Theme : InheritedComponent
{
    /// <summary>A subtree drawn in <paramref name="value"/>.</summary>
    /// <param name="value">The theme the subtree reads.</param>
    /// <param name="child">The subtree.</param>
    public Theme(IAppTheme value, VisualNode child) : base(child)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    /// <summary>The theme the subtree reads.</summary>
    public IAppTheme Value { get; }

    /// <summary>Its dependents build again when the theme is another one.</summary>
    protected internal override bool UpdateShouldNotify(InheritedComponent old) => !ReferenceEquals(((Theme)old).Value, Value);
}
