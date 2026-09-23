namespace eQuantic.UI.Primitives;

/// <summary>
/// Which of the two keyboard traditions a host's users live in — the thing a key's MEANING depends
/// on once a modifier is held.
/// <para>
/// The two disagree on the keys an editor uses most. On Apple's, ⌥ moves by word and ⌘ jumps to the
/// end of the line; everywhere else Ctrl (which <see cref="KeyModifiers.Command"/> already stands for
/// there) moves by word and Home/End reach the line's ends. The same chord, ⌘← and Ctrl+←, means
/// "line start" in one and "one word back" in the other, so the modifier alone cannot say which —
/// the host has to. Only the host knows where it runs; a model never guesses.
/// </para>
/// </summary>
public enum KeyboardConvention : byte
{
    /// <summary>Windows, Linux, ChromeOS, Android with a keyboard: Ctrl moves by word, Home/End
    /// reach the line's ends.</summary>
    Standard = 0,

    /// <summary>macOS, iPadOS: ⌥ moves by word, ⌘ reaches the line's and the document's ends.</summary>
    Apple = 1,
}
