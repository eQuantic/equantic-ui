namespace eQuantic.UI.Native.Shell.MacOS;

/// <summary>
/// What a macOS key press MEANS to the host, in the DOM's spelling: the vocabulary every shell speaks
/// so a chord like <c>F7</c> or <c>⌘K</c> is authored once (the Windows twin is <c>WindowsKeys</c>).
/// Pure functions over the event's own numbers, so the mapping is testable without a window.
/// </summary>
public static class MacKeys
{
    /// <summary>
    /// The key's DOM name, taken from the KEY CODE rather than the character it produced. A layout
    /// decides what a key types; it does not decide which key is Return, and a French keyboard's
    /// arrows have to work as arrows. A function key types a character of its own in the private use
    /// area (F7 types U+F70A), which reached the host as the key's name, so no chord written
    /// <c>F7</c> could ever match it.
    /// </summary>
    public static string NameOf(ushort keyCode, string characters) => keyCode switch
    {
        48 => "Tab",
        36 or 76 => "Enter",
        51 => "Backspace",
        117 => "Delete",
        53 => "Escape",
        49 => " ",
        123 => "ArrowLeft",
        124 => "ArrowRight",
        125 => "ArrowDown",
        126 => "ArrowUp",
        115 => "Home",
        119 => "End",
        116 => "PageUp",
        121 => "PageDown",
        _ when FunctionKeyNumber(keyCode) is > 0 and var number => "F" + number,
        // Everything else IS what it typed: that is the DOM's rule too, and it is what a chord
        // like ⌘K is written against.
        _ => characters,
    };

    /// <summary>
    /// Whether the key is one of F1 to F20. Such a key composes nothing, so it never goes through the
    /// input method: it is a command, and the input context would take it and answer for it.
    /// </summary>
    public static bool IsFunctionKey(ushort keyCode) => FunctionKeyNumber(keyCode) > 0;

    /// <summary>The number of a function key, 1 to 20, from its key code (<c>kVK_F1</c>…
    /// <c>kVK_F20</c>, which the keyboard does not lay out in order); 0 for any other key.</summary>
    private static int FunctionKeyNumber(ushort keyCode) => keyCode switch
    {
        122 => 1,
        120 => 2,
        99 => 3,
        118 => 4,
        96 => 5,
        97 => 6,
        98 => 7,
        100 => 8,
        101 => 9,
        109 => 10,
        103 => 11,
        111 => 12,
        105 => 13,
        107 => 14,
        113 => 15,
        106 => 16,
        64 => 17,
        79 => 18,
        80 => 19,
        90 => 20,
        _ => 0,
    };
}
