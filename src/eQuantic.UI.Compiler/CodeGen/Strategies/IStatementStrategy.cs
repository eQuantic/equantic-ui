using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// A statement strategy builds a <see cref="JsStatement"/>; <see cref="JsStatementWriter"/> lays
/// it out. There is no text contract on the statement side any more — every strategy crossed
/// over, and a new one is born on the IR.
/// </summary>
public interface IStatementStrategy
{
    bool CanConvert(StatementSyntax node, ConversionContext context);
    JsStatement Convert(StatementSyntax node, ConversionContext context);
    int Priority { get; }
}
