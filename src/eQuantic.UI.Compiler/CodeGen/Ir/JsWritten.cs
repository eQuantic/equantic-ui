using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen.Ir;

/// <summary>
/// Text a writer produced, with the marks of the statements in it that carry an origin, each
/// counted from the start of the text (#293). Both writers compose with it: a statement's text
/// holds its expressions', and an expression's holds the statements of an arrow's block (#384).
/// </summary>
internal sealed record JsWritten(string Text, IReadOnlyList<JsLineMark> Marks)
{
    public static JsWritten Of(string text) => new(text, []);

    /// <summary>The same text, marked as beginning the statement <paramref name="origin"/> came
    /// from; no origin, no mark.</summary>
    public JsWritten MarkedAt(SyntaxNode? origin) =>
        origin is null ? this : this with { Marks = [new JsLineMark(0, 0, origin), .. Marks] };
}
