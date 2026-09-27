using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen.Ir;

/// <summary>
/// Text a writer produced, with the marks of the statements in it that carry an origin, each
/// counted from the start of the text (#293). Both writers compose with it: a statement's text
/// holds its expressions', and an expression's holds the statements of an arrow's block (#384).
/// </summary>
internal sealed class JsWritten
{
    private static readonly IReadOnlyList<JsLineMark> NoMarks = [];
    private static readonly IReadOnlyList<JsPosition> NoResumes = [];

    public JsWritten(string text, IReadOnlyList<JsLineMark> marks, IReadOnlyList<JsPosition> resumes)
    {
        Text = text;
        Marks = marks;
        Resumes = resumes;
    }

    public string Text { get; }

    public IReadOnlyList<JsLineMark> Marks { get; }

    /// <summary>
    /// Where the statement around this text takes its line back: right after an arrow's block.
    /// What follows the block on its closing line (a chained call, the next argument, an operand)
    /// belongs to that statement, and read through the map as the block's last statement once the
    /// block's statements carried marks and nothing marked the rest (found in review, #384). The
    /// statement that holds the text turns each one into a mark of its own.
    /// </summary>
    public IReadOnlyList<JsPosition> Resumes { get; }

    /// <summary>Whether anything in it has to move with the text: a mark, or a place to resume.</summary>
    public bool Carries => Marks.Count > 0 || Resumes.Count > 0;

    public static JsWritten Of(string text) => new(text, NoMarks, NoResumes);

    /// <summary>
    /// The same text, marked as beginning the statement <paramref name="origin"/> came from, and
    /// that statement resumed wherever an arrow's block in it closed; no origin, no mark, and the
    /// places to resume wait for a statement around it that has one.
    /// </summary>
    public JsWritten MarkedAt(SyntaxNode? origin) =>
        origin is null
            ? this
            : new JsWritten(Text,
                [new JsLineMark(0, 0, origin), .. Marks, .. Resumes.Select(at => new JsLineMark(at.Line, at.Column, origin))],
                NoResumes);
}
