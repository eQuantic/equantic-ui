namespace eQuantic.UI.Primitives;

/// <summary>What the web twin ANNOUNCES an <see cref="Adjustable"/> as. Member names are the ARIA
/// tokens themselves (they cross to the client as camelCase strings — "tablist", not "tabList").
/// The native semantics walk reads it too, and gives each member a <see cref="SemanticRole"/> of its
/// own: <see cref="SemanticRole.Slider"/>, <see cref="SemanticRole.TabBar"/>,
/// <see cref="SemanticRole.RadioGroup"/> and <see cref="SemanticRole.Splitter"/>. Append-only, as every
/// vocabulary enum is.</summary>
public enum AdjustableRole
{
    /// <summary>role="slider" — a continuous value the arrows nudge.</summary>
    Slider,

    /// <summary>role="tablist" — one tab selected out of a visible set.</summary>
    Tablist,

    /// <summary>role="radiogroup" — one choice out of a visible set.</summary>
    Radiogroup,

    /// <summary>role="separator", focusable — the boundary between two things, which the arrows move: a
    /// table's column edge, a split view's divider (#1538). Like a slider it holds a value, the size it
    /// leaves the thing before it, and is a group without one; its orientation is the line it draws,
    /// across the <see cref="Adjustable.Axis"/> it moves on.</summary>
    Separator,
}
