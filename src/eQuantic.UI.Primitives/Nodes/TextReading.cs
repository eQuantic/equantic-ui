namespace eQuantic.UI.Primitives;

/// <summary>
/// What a screen reader is handed of a surface that keeps its text to itself: the text it reads as the
/// field's VALUE, and where the selection stands inside that text (#1333).
/// <para>
/// <em>How does Flutter solve it?</em> An <c>EditableText</c> gives its semantics node the text as its
/// <c>value</c> and the selection as its <c>textSelection</c>, so a reader hears the field and what it holds
/// as one thing and reads the line the caret is on. A <see cref="CodeSurface"/> draws its text itself, so the
/// realizer cannot read it off a native control: the model says it here.
/// </para>
/// <para>
/// The text need not be the whole document. A document of thousands of lines is handed as the lines around
/// the caret, as a code editor in a browser does for its screen reader, so a keystroke never rewrites the
/// whole of it; the offsets are then counted in that text, not in the document.
/// </para>
/// </summary>
/// <param name="Text">The text a reader hears as the field's value.</param>
/// <param name="Anchor">Where the selection starts, in UTF-16 code units of <paramref name="Text"/>: the end
/// that stays when it is extended.</param>
/// <param name="Focus">Where the selection ends, in UTF-16 code units of <paramref name="Text"/>: the end that
/// moves, the caret. Equal to <paramref name="Anchor"/> when nothing is selected.</param>
public readonly record struct TextReading(string Text, int Anchor, int Focus);
