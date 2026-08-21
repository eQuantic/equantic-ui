using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.Services;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Indexing: <c>arr[i]</c>, with a multi-argument indexer becoming nested subscripts
/// (<c>arr[1, 2]</c> → <c>arr[1][2]</c>). A C# 15 extension indexer lowers to the static
/// <c>item(receiver, …)</c> the emitter writes on the declaring class.
/// </summary>
public class ElementAccessStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is ElementAccessExpressionSyntax;
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var elementAccess = (ElementAccessExpressionSyntax)node;

        // C# 15 extension INDEXER (`seq[2]` bound to an extension block's this[]): the emitter
        // lowers the indexer to a static `item(receiver, …)` on the declaring class.
        if (context.SemanticHelper.GetSymbol(elementAccess) is IPropertySymbol { IsIndexer: true } indexer
            && indexer.ExtensionBlockHome() is { } extensionHome)
        {
            extensionHome.RegisterIntroduced(context);
            var indexerArgs = string.Join(", ", elementAccess.ArgumentList.Arguments
                .Select(a => context.Converter.ConvertExpression(a.Expression)));
            var receiver = context.Converter.ConvertExpression(elementAccess.Expression);
            return JsExpr.Callish($"{extensionHome.Name}.item({receiver}, {indexerArgs})");
        }

        // Each indexer argument is one subscript.
        var indexed = context.Converter.ConvertIr(elementAccess.Expression);
        foreach (var arg in elementAccess.ArgumentList.Arguments)
        {
            indexed = JsExpr.Index(indexed, context.Converter.ConvertIr(arg.Expression));
        }
        return indexed;
    }

    public int Priority => 1;
}
