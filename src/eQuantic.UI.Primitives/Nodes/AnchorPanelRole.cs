namespace eQuantic.UI.Primitives;

/// <summary>What an <see cref="Anchored"/> panel IS to assistive tech. Grows with the composites
/// that need it, never speculatively — the same rule <see cref="PressableRole"/> follows.</summary>
public enum AnchorPanelRole : byte
{
    /// <summary>A plain floating panel — today's default everywhere.</summary>
    None = 0,

    /// <summary>A menu of commands (<c>role="menu"</c>); its rows are MenuItems.</summary>
    Menu = 1,

    /// <summary>A list of choices (<c>role="listbox"</c>); its rows are Options, and the ANCHOR
    /// becomes the combobox.</summary>
    Listbox = 2,

    /// <summary>
    /// A panel that holds a COMPOSITE rather than a list of rows — the pointer tier's calendar
    /// popover (design system C15). It is a dialog because what is inside owns its own keyboard
    /// (a <see cref="Navigable"/> grid), so the anchor cannot keep the focus and drive it the way
    /// a combobox drives a listbox: focus MOVES into the panel, and Esc brings it back.
    /// </summary>
    Dialog = 3,
}
