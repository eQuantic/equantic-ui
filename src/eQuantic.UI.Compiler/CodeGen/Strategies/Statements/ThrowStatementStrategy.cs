using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary><c>throw expr;</c>, and the bare rethrow <c>throw;</c>.</summary>
public class ThrowStatementStrategy : IStatementIrStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is ThrowStatementSyntax;
    }

    public JsStatement ConvertIr(StatementSyntax node, ConversionContext context)
    {
        var throwStmt = (ThrowStatementSyntax)node;
        return JsStatement.Throw(throwStmt.Expression == null
            ? null
            : context.Converter.ConvertIr(throwStmt.Expression));
    }

    public int Priority => 0;
}
