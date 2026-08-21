using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.CodeGen.Strategies;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// <c>try</c>/<c>catch</c>/<c>finally</c>. Every catch is catch-all in JavaScript — the exception
/// type is not a construct there — so a typed catch with no variable (<c>catch (OverflowException)</c>)
/// and an untyped <c>catch</c> both use the optional catch binding.
/// </summary>
public class TryStatementStrategy : IStatementIrStrategy
{
    public int Priority => 0;

    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is TryStatementSyntax;
    }

    public JsStatement ConvertIr(StatementSyntax node, ConversionContext context)
    {
        var tryStmt = (TryStatementSyntax)node;
        var converter = context.Converter;

        var catches = tryStmt.Catches.Select(catchClause =>
        {
            // ANNOTATED `any`, because the emitted TypeScript is type-checked and a catch binding
            // is `unknown` there. C# hands you a typed exception, so the body reads
            // `error.Message` without asking — and the emitted module refused to compile on
            // exactly that line. The type is gone either way; this only stops the annotation
            // from being narrower than the language it came from.
            var identifier = catchClause.Declaration?.Identifier.Text;
            var binding = string.IsNullOrEmpty(identifier)
                ? ""
                : context.TypeAnnotations ? $"({identifier}: any)" : $"({identifier})";
            return new JsCatch(binding, converter.ConvertBlockIr(catchClause.Block));
        }).ToList();

        return JsStatement.Try(
            converter.ConvertBlockIr(tryStmt.Block),
            catches,
            tryStmt.Finally is null ? null : converter.ConvertBlockIr(tryStmt.Finally.Block));
    }
}
