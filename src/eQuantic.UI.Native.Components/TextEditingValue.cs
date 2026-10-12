namespace eQuantic.UI.Native.Components;

/// <summary>
/// A field as a phone's keyboard edits it: its text, its selection and the composition in it,
/// Flutter's <c>TextEditingValue</c>. A phone keyboard does not send keys; it reads the text around
/// the caret and hands back a new state of the whole field (a word autocorrected, a suggestion
/// picked, a swipe, dictation), so the host takes the state and works out the edit itself.
/// <para>
/// The composition is IN the text, as on the web and in Flutter: the word a keyboard is still
/// suggesting for is part of the value the app is handed, and <see cref="ComposingStart"/> and
/// <see cref="ComposingEnd"/> only mark it, so it is drawn underlined. A search field filters as
/// the word is typed, not when the space after it is.
/// </para>
/// </summary>
/// <param name="Text">The field's text, composition included.</param>
/// <param name="SelectionBase">Where the selection started: the end that stays.</param>
/// <param name="SelectionExtent">Where it ends: the caret, the end that moves.</param>
/// <param name="ComposingStart">The start of the composition, or -1 when there is none.</param>
/// <param name="ComposingEnd">The end of the composition, or -1 when there is none.</param>
public readonly record struct TextEditingValue(
    string Text,
    int SelectionBase,
    int SelectionExtent,
    int ComposingStart = -1,
    int ComposingEnd = -1)
{
    /// <summary>The selection's low end.</summary>
    public int SelectionStart => Math.Min(SelectionBase, SelectionExtent);

    /// <summary>The selection's high end; equal to <see cref="SelectionStart"/> for a caret.</summary>
    public int SelectionEnd => Math.Max(SelectionBase, SelectionExtent);

    /// <summary>Whether a composition is marked: a range with something in it, inside the text.</summary>
    public bool HasComposing =>
        ComposingStart >= 0 && ComposingEnd > ComposingStart && ComposingEnd <= Text.Length;

    /// <summary>The text with the caret at <paramref name="caret"/> and nothing composed.</summary>
    public static TextEditingValue Collapsed(string text, int caret) => new(text, caret, caret);
}
