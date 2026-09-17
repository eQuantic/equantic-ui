namespace eQuantic.UI.Primitives;

/// <summary>Modifier keys of a <see cref="KeyChord"/>. <see cref="Command"/> is the PLATFORM's
/// command key — ⌘ on Apple, Ctrl elsewhere — so one authored chord is right everywhere.</summary>
[Flags]
public enum KeyModifiers : byte
{
    None = 0,
    Shift = 1,
    Alt = 2,
    /// <summary>⌘ on Apple, Ctrl on Windows/Linux (the "⌘K/Ctrl+K" idiom, authored once).</summary>
    Command = 4,
    /// <summary>Literally Control, on every platform (rare — prefer <see cref="Command"/>).</summary>
    Control = 8,
}
