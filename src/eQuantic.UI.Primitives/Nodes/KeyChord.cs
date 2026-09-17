namespace eQuantic.UI.Primitives;

/// <summary>
/// A key plus its modifiers. <see cref="Key"/> uses the DOM <c>KeyboardEvent.key</c> names
/// (<c>"k"</c>, <c>"Escape"</c>, <c>"ArrowDown"</c>, <c>"Enter"</c>) — one vocabulary both targets
/// map from, matched case-insensitively so Shift-typing never breaks a binding.
/// </summary>
public readonly record struct KeyChord(string Key, KeyModifiers Modifiers = KeyModifiers.None)
{
    /// <summary>⌘K / Ctrl+K — the platform command chord.</summary>
    public static KeyChord Command(string key) => new(key, KeyModifiers.Command);

    public static readonly KeyChord Escape = new("Escape");
    public static readonly KeyChord Enter = new("Enter");
    public static readonly KeyChord ArrowUp = new("ArrowUp");
    public static readonly KeyChord ArrowDown = new("ArrowDown");
}
