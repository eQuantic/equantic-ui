using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen.Ir;

/// <summary>
/// Where a statement that carries an <see cref="JsStatement.Origin"/> begins in the text a writer
/// produced: its line and column, both counted from zero at the start of that text, and the C# it
/// came from. The class writer turns each one into a source-map segment (#293).
/// </summary>
public sealed record JsLineMark(int Line, int Column, SyntaxNode Origin);
