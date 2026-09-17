namespace eQuantic.UI.Primitives;

/// <summary>What the web twin ANNOUNCES an <see cref="Adjustable"/> as. Member names are the ARIA
/// tokens themselves (they cross to the client as camelCase strings — "tablist", not "tabList").</summary>
public enum AdjustableRole
{
    /// <summary>role="slider" — a continuous value the arrows nudge.</summary>
    Slider,

    /// <summary>role="tablist" — one tab selected out of a visible set.</summary>
    Tablist,

    /// <summary>role="radiogroup" — one choice out of a visible set.</summary>
    Radiogroup,
}
