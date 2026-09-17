namespace eQuantic.UI.Primitives;

/// <summary>The composite-item roles a <see cref="Pressable"/> can take. Grows alongside the
/// composites that need it (tabs join with the Tabs semantics work), never speculatively.</summary>
public enum PressableRole : byte
{
    /// <summary>A standalone button — today's default everywhere.</summary>
    Button = 0,

    /// <summary>One choice of an exclusive set, inside an <see cref="AdjustableRole.Radiogroup"/>.</summary>
    Radio = 1,

    /// <summary>
    /// A two-or-three-state check. The STATE goes in <see cref="Pressable.Selected"/> (and
    /// <see cref="Pressable.Mixed"/>), never in the name: a control that announces "Checked,
    /// checkbox" twice over is noise, and one whose name CHANGES when its state does reads as a
    /// different control to assistive tech. Unlike a radio it keeps its own Tab stop — a checkbox
    /// is not one choice of a composite, it IS the control.
    /// </summary>
    Checkbox = 2,

    /// <summary>An on/off toggle that acts immediately — ARIA's <c>switch</c>. Same contract as
    /// <see cref="Checkbox"/> (state as attribute, own Tab stop), minus the mixed state, which the
    /// role does not admit.</summary>
    Switch = 3,

    /// <summary>One tab of a tablist. The radio's twin in every mechanical respect — roving
    /// tabindex under an <see cref="AdjustableRole.Tablist"/> wrapper, selection as an attribute —
    /// but the attribute is <c>aria-selected</c>: a tab is picked, not checked.</summary>
    Tab = 4,

    /// <summary>One row of a menu panel (<c>role="menuitem"</c>). Out of the Tab order — the
    /// keyboard lives on the TRIGGER while a menu is up, and the highlight travels through
    /// <see cref="Anchored.ActiveIndex"/> as <c>aria-activedescendant</c>.</summary>
    MenuItem = 5,

    /// <summary>One option of a listbox panel (<c>role="option"</c>): selection as
    /// <c>aria-selected</c> — an option is picked, like a tab — and out of the Tab order for the
    /// same reason a menu item is.</summary>
    Option = 6,

    /// <summary>
    /// One destination of a navigation — a bar's tab, a rail's stop, a drawer's row. The third
    /// spelling of "this one of the set is the active one", and the one the others cannot say: a
    /// destination is not PRESSED (it does not toggle), not CHECKED (nothing is being chosen), and
    /// not SELECTED (a tab switches a panel; a destination changes where you ARE). The web word is
    /// <c>aria-current="page"</c>, and both mobile bridges report it as the node's selected state,
    /// which is the closest thing each platform has.
    /// <para>
    /// It keeps its own Tab stop, unlike <see cref="Tab"/> and <see cref="Radio"/>: a navigation is
    /// a list of links, not a composite with one entry point, and a keyboard user reaching the bar
    /// expects to walk it.
    /// </para>
    /// </summary>
    Destination = 7,

    /// <summary>
    /// One cell of a two-dimensional composite — a calendar's day, a picker's hour (design system
    /// C15: <c>grid</c> / <c>gridcell</c> + selected). Selection is stated the way a tab's is
    /// (PICKED, not checked), and the cell leaves the Tab order because the enclosing
    /// <see cref="Navigable"/> is the composite's one stop: a grid of 42 days must not cost 42
    /// tab presses to walk past.
    /// <para>
    /// It differs from <see cref="Option"/> in the DIMENSION the keyboard moves in, which is the
    /// whole reason the role exists: an option list answers next/previous, a grid answers
    /// next/previous, row up/row down, page, and row bounds (see <see cref="NavigableMove"/>).
    /// </para>
    /// </summary>
    GridCell = 8,
}
