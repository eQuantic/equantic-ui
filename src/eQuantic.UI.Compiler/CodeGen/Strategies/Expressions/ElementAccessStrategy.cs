using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for element access (indexers).
/// Handles: dict[key], array[0]
/// </summary>
public class ElementAccessStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is ElementAccessExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
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
            return $"{extensionHome.Name}.item({receiver}, {indexerArgs})";
        }

        var expr = context.Converter.ConvertExpression(elementAccess.Expression);
        
        // Convert indexer arguments
        // If multiple args: arr[1, 2] -> arr[1][2]
        var args = elementAccess.ArgumentList.Arguments;
        var sb = new System.Text.StringBuilder(expr);
        
        foreach (var arg in args)
        {
            sb.Append("[").Append(context.Converter.ConvertExpression(arg.Expression)).Append("]");
        }

        return sb.ToString();
    }

    public int Priority => 1;
}
