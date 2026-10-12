using Android.Views;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Shell.Android;

/// <summary>
/// What an Android key press MEANS to the host, in the DOM's spelling: the vocabulary every shell
/// speaks so a chord like <c>⌘K</c> / <c>Ctrl+K</c> is authored once (the twins are
/// <c>WindowsKeys</c> and <c>MacKeys</c>). A phone's on-screen keyboard sends few keys (Enter,
/// Backspace, the keys it types into a code surface); a keyboard attached to a phone or a tablet
/// sends all of them.
/// </summary>
internal static class AndroidKeys
{
    /// <summary>
    /// The key's DOM name from its KEY CODE, never from the character it typed: a layout decides
    /// what a key types, not which key is Enter. Every other key is what it types with no modifier
    /// held, lower-cased, which is the DOM's rule too and what a chord is written against; the text
    /// a press INSERTS is <see cref="TypedText"/>.
    /// </summary>
    public static string NameOf(KeyEvent e) => e.KeyCode switch
    {
        Keycode.Tab => "Tab",
        // The d-pad's centre is a TV remote's and an old phone's OK: it runs what Enter runs.
        Keycode.Enter or Keycode.NumpadEnter or Keycode.DpadCenter => "Enter",
        Keycode.Del => "Backspace",
        Keycode.ForwardDel => "Delete",
        Keycode.Escape => "Escape",
        Keycode.Space => " ",
        Keycode.DpadLeft => "ArrowLeft",
        Keycode.DpadRight => "ArrowRight",
        Keycode.DpadUp => "ArrowUp",
        Keycode.DpadDown => "ArrowDown",
        Keycode.MoveHome => "Home",
        Keycode.MoveEnd => "End",
        Keycode.PageUp => "PageUp",
        Keycode.PageDown => "PageDown",
        Keycode.Insert => "Insert",
        >= Keycode.F1 and <= Keycode.F12 => "F" + (e.KeyCode - Keycode.F1 + 1),
        _ => Character(e.GetUnicodeChar(MetaKeyStates.None)).ToLowerInvariant(),
    };

    /// <summary>
    /// The modifiers the key was pressed with. <see cref="KeyModifiers.Command"/> is CTRL here, as
    /// on Windows and on a Chromebook, and Alt is Alt. Meta is nobody's: Android's own shortcuts sit
    /// on it.
    /// </summary>
    public static KeyModifiers Modifiers(KeyEvent e)
    {
        var modifiers = KeyModifiers.None;
        if (e.IsShiftPressed) modifiers |= KeyModifiers.Shift;
        if (e.IsCtrlPressed) modifiers |= KeyModifiers.Command;
        if (e.IsAltPressed) modifiers |= KeyModifiers.Alt;
        return modifiers;
    }

    /// <summary>
    /// The text the press INSERTS, with Shift and the layout applied, or empty. A Ctrl chord types
    /// nothing: Ctrl+A selects the field, it does not add an "a" to it.
    /// </summary>
    public static string TypedText(KeyEvent e, KeyModifiers modifiers) =>
        (modifiers & (KeyModifiers.Command | KeyModifiers.Control)) != 0
            ? ""
            : Character(e.GetUnicodeChar(e.MetaState));

    /// <summary>
    /// The accent a DEAD key holds for the next key, or zero for any other press. On a layout with
    /// dead keys (Portuguese, US International), ´ types nothing by itself and makes the next letter
    /// á; the key map marks such a press with <see cref="KeyCharacterMap.CombiningAccent"/>, and what
    /// is under the mask is the accent written on its own. A Ctrl chord holds nothing, as it types
    /// nothing.
    /// </summary>
    public static int DeadAccent(KeyEvent e, KeyModifiers modifiers)
    {
        if ((modifiers & (KeyModifiers.Command | KeyModifiers.Control)) != 0) return 0;
        var unicode = e.GetUnicodeChar(e.MetaState);
        return (unicode & KeyCharacterMap.CombiningAccent) != 0
            ? unicode & KeyCharacterMap.CombiningAccentMask
            : 0;
    }

    /// <summary>The accent a dead key held, written on its own.</summary>
    public static string Accent(int accent) => Character(accent);

    /// <summary>
    /// What a held accent and the text typed after it write: the one character the two make where
    /// there is one (´ then a is á; the accent again, or a space, is the accent on its own), and
    /// otherwise the accent followed by the text, as Android's own fields write it.
    /// </summary>
    public static string Composed(int accent, string typed)
    {
        if (typed.Length == 0) return Accent(accent);
        var composed = KeyCharacterMap.GetDeadChar(accent, char.ConvertToUtf32(typed, 0));
        if (composed <= 0) return Accent(accent) + typed;
        return Character(composed) + typed[(char.IsSurrogatePair(typed, 0) ? 2 : 1)..];
    }

    /// <summary>
    /// The character a key map answered, or empty: zero is no character, a dead key's accent sets
    /// the sign bit (<see cref="DeadAccent"/> reads it), and a control character is a key handled
    /// by name.
    /// </summary>
    private static string Character(int unicode)
    {
        if (unicode <= 0 || unicode > 0x10FFFF || unicode is >= 0xD800 and <= 0xDFFF) return "";
        var text = char.ConvertFromUtf32(unicode);
        return char.IsControl(text[0]) ? "" : text;
    }
}
