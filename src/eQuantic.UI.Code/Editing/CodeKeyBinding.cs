using eQuantic.UI.Primitives;

namespace eQuantic.UI.Code;

/// <summary>
/// One entry of an editor's keymap: a chord and the command it runs. A key matches it only with exactly
/// its modifiers, a single letter in either case. A chord may name several commands, each an entry of its
/// own, and they are tried in the table's order until one runs (<see cref="CodeKeymap"/>).
/// </summary>
/// <param name="Chord">The key and its modifiers, the SDK's <see cref="KeyChord"/> as a
/// <see cref="Shortcut"/> takes it.</param>
/// <param name="Command">What the chord runs; <see cref="CodeCommand.None"/> gives the chord back to the
/// host.</param>
public readonly record struct CodeKeyBinding(KeyChord Chord, CodeCommand Command)
{
    /// <summary>
    /// The chord that comes first, for a binding of two keys (⌘K, of ⌘K ⌘I), or null for one key. The
    /// first chord waits for one more key only while the command could run, and any key but a modifier
    /// ends the wait.
    /// </summary>
    public KeyChord? First { get; init; }
}
