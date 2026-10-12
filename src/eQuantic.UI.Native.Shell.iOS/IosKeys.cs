using eQuantic.UI.Primitives;
using UIKit;

namespace eQuantic.UI.Native.Shell.iOS;

/// <summary>
/// What a press of a keyboard attached to an iPhone or an iPad MEANS to the host, in the DOM's
/// spelling: the vocabulary every shell speaks (the twins are <c>MacKeys</c>, <c>WindowsKeys</c> and
/// <c>AndroidKeys</c>). The on-screen keyboard sends no presses; it types through the text input
/// protocol.
/// </summary>
internal static class IosKeys
{
    /// <summary>The DOM name of a key that types nothing, from its HID usage, or null for a key
    /// that types a character: a layout decides what a key types, not which key is Enter.</summary>
    public static string? NameOf(UIKey key) => key.KeyCode switch
    {
        UIKeyboardHidUsage.KeyboardTab => "Tab",
        UIKeyboardHidUsage.KeyboardReturnOrEnter or UIKeyboardHidUsage.KeypadEnter => "Enter",
        UIKeyboardHidUsage.KeyboardDeleteOrBackspace => "Backspace",
        UIKeyboardHidUsage.KeyboardDeleteForward => "Delete",
        UIKeyboardHidUsage.KeyboardEscape => "Escape",
        UIKeyboardHidUsage.KeyboardLeftArrow => "ArrowLeft",
        UIKeyboardHidUsage.KeyboardRightArrow => "ArrowRight",
        UIKeyboardHidUsage.KeyboardUpArrow => "ArrowUp",
        UIKeyboardHidUsage.KeyboardDownArrow => "ArrowDown",
        UIKeyboardHidUsage.KeyboardHome => "Home",
        UIKeyboardHidUsage.KeyboardEnd => "End",
        UIKeyboardHidUsage.KeyboardPageUp => "PageUp",
        UIKeyboardHidUsage.KeyboardPageDown => "PageDown",
        >= UIKeyboardHidUsage.KeyboardF1 and <= UIKeyboardHidUsage.KeyboardF12 =>
            "F" + (key.KeyCode - UIKeyboardHidUsage.KeyboardF1 + 1),
        _ => null,
    };

    /// <summary>What the key types with no modifier held, lower-cased: the name a chord like ⌘K is
    /// written against.</summary>
    public static string Character(UIKey key) =>
        key.CharactersIgnoringModifiers is { Length: > 0 } characters && !char.IsControl(characters[0])
            ? characters.ToLowerInvariant()
            : "";

    /// <summary>The modifiers held, as on the Mac: ⌘ is Command, ⌃ is Control, ⌥ is Alt.</summary>
    public static KeyModifiers Modifiers(UIKeyModifierFlags flags)
    {
        var modifiers = KeyModifiers.None;
        if (flags.HasFlag(UIKeyModifierFlags.Shift)) modifiers |= KeyModifiers.Shift;
        if (flags.HasFlag(UIKeyModifierFlags.Control)) modifiers |= KeyModifiers.Control;
        if (flags.HasFlag(UIKeyModifierFlags.Alternate)) modifiers |= KeyModifiers.Alt;
        if (flags.HasFlag(UIKeyModifierFlags.Command)) modifiers |= KeyModifiers.Command;
        return modifiers;
    }
}
