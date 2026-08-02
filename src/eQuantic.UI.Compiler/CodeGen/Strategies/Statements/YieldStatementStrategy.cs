using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// <c>yield return x</c> and <c>yield break</c>. Inside a method the emitter is MATERIALISING (the
/// normal case — see <see cref="ConversionContext.IteratorBuffer"/>) they append to the buffer and
/// return it; a real JS generator is only emitted where nothing collects.
/// </summary>
public class YieldStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is YieldStatementSyntax;
    }

    public string Convert(StatementSyntax node, ConversionContext context)
    {
        var yieldStmt = (YieldStatementSyntax)node;
        
        var buffer = context.IteratorBuffer;

        if (yieldStmt.Kind() == SyntaxKind.YieldBreakStatement)
        {
            return buffer is null ? "return;" : $"return {buffer};";
        }

        var expr = context.Converter.ConvertExpression(yieldStmt.Expression);
        return buffer is null ? $"yield {expr};" : $"{buffer}.push({expr});";
    }

    public int Priority => 10;
}
