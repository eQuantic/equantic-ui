using eQuantic.UI.Primitives;
using static eQuantic.UI.Native.Shell.Windows.Win32;

namespace eQuantic.UI.Native.Shell.Windows;

/// <summary>
/// What a Windows key press MEANS to the host, in the DOM's spelling — the vocabulary every shell
/// speaks so a chord like <c>⌘K</c> / <c>Ctrl+K</c> is authored once. Pure functions over the
/// message's own numbers, so the mapping is testable without a window.
/// </summary>
public static class WindowsKeys
{
    /// <summary>
    /// The key's DOM name from its VIRTUAL-KEY code, never from the character it produced. A layout
    /// decides what a key types; it does not decide which key is Enter — and an AZERTY keyboard's
    /// arrows have to work as arrows. Letters, digits and punctuation answer with the character the
    /// unshifted key types (<c>MapVirtualKey</c>), lower-cased: chords match case-insensitively, and
    /// the text a press INSERTS travels separately through <c>WM_CHAR</c>.
    /// </summary>
    public static string NameOf(uint virtualKey)
    {
        switch (virtualKey)
        {
            case 0x09: return "Tab";
            case 0x0D: return "Enter";
            case 0x08: return "Backspace";
            case 0x2E: return "Delete";
            case 0x1B: return "Escape";
            case 0x20: return " ";
            case 0x25: return "ArrowLeft";
            case 0x26: return "ArrowUp";
            case 0x27: return "ArrowRight";
            case 0x28: return "ArrowDown";
            case 0x24: return "Home";
            case 0x23: return "End";
            case 0x21: return "PageUp";
            case 0x22: return "PageDown";
            case 0x2D: return "Insert";
            case 0x14: return "CapsLock";
            case 0x10 or 0xA0 or 0xA1: return "Shift";
            case 0x11 or 0xA2 or 0xA3: return "Control";
            case 0x12 or 0xA4 or 0xA5: return "Alt";
            case 0x5B or 0x5C: return "Meta";
        }

        // F1..F24 are contiguous from 0x70.
        if (virtualKey is >= 0x70 and <= 0x87) return "F" + (virtualKey - 0x70 + 1);

        // The numeric keypad's own keys, which MapVirtualKey does not always name.
        if (virtualKey is >= 0x60 and <= 0x69) return ((char)('0' + (virtualKey - 0x60))).ToString();
        switch (virtualKey)
        {
            case 0x6A: return "*";
            case 0x6B: return "+";
            case 0x6D: return "-";
            case 0x6E: return ".";
            case 0x6F: return "/";
        }

        // Everything else IS what it types — the DOM's rule too, and what a chord is written
        // against. The high bit marks a dead key; the low half is still the character.
        var character = (char)(MapVirtualKeyW(virtualKey, MAPVK_VK_TO_CHAR) & 0x7FFF);
        if (character == 0 || char.IsControl(character)) return "";
        return char.ToLowerInvariant(character).ToString();
    }

    /// <summary>
    /// The modifiers held right now. <see cref="KeyModifiers.Command"/> is CTRL on Windows — the
    /// "⌘K / Ctrl+K" idiom, authored once — and Alt is Alt. The Windows key is nobody's: the shell
    /// owns every chord on it, and a page that claimed one would fight the OS and lose.
    /// </summary>
    public static KeyModifiers Modifiers()
    {
        var modifiers = KeyModifiers.None;
        if (IsDown(VK_SHIFT)) modifiers |= KeyModifiers.Shift;
        if (IsDown(VK_CONTROL)) modifiers |= KeyModifiers.Command;
        if (IsDown(VK_MENU)) modifiers |= KeyModifiers.Alt;
        return modifiers;
    }

    /// <summary>
    /// How far a wheel message asked the content to move, in dp. A notch is <c>WHEEL_DELTA</c> and
    /// the system says how many LINES a notch scrolls; a line is <see cref="Touch.WheelLine"/>. A
    /// precision touchpad reports fractions of a notch through the same message and scales
    /// linearly. Positive wheel deltas mean "away from the user", which is scrolling UP — so the
    /// sign flips, exactly as the Mac flips <c>scrollingDeltaY</c>.
    /// </summary>
    public static float WheelTravel(int wheelDelta, uint linesPerNotch)
    {
        // "Scroll a page per notch" has no page here; a generous line count is the honest stand-in.
        var lines = linesPerNotch == WHEEL_PAGESCROLL ? 10f : Math.Max(1, (int)linesPerNotch);
        return -(wheelDelta / (float)WHEEL_DELTA) * lines * Touch.WheelLine;
    }

    /// <summary>
    /// The text a <c>WM_CHAR</c> should INSERT, or empty. Control characters are the keys handled
    /// by name (Enter, Tab, Backspace, Escape), and a Ctrl chord's character (Ctrl+A arrives as
    /// U+0001) must never be typed into the field someone was selecting all of.
    /// </summary>
    public static string TypedText(char character, KeyModifiers modifiers)
    {
        if (char.IsControl(character)) return "";
        if ((modifiers & (KeyModifiers.Command | KeyModifiers.Control)) != 0) return "";
        return character.ToString();
    }
}
