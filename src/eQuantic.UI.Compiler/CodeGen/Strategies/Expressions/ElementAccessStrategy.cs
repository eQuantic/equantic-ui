using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
        // A DICTIONARY READ fails for a key that is not there. A plain object answers `undefined`,
        // so the absence spread through the program instead of stopping it where .NET stops it —
        // and an undefined reaching a render is a blank, not an error anyone can trace. Only a
        // READ: the same syntax on the left of an assignment is how a key is ADDED.
        if (elementAccess.ArgumentList.Arguments.Count == 1
            && !IsAssignmentTarget(elementAccess)
            && context.SemanticHelper.GetType(elementAccess.Expression).IsDictionaryLike(out _))
        {
            context.UsedHelpers.Add(Eq.Import);
            var map = context.Converter.ConvertExpression(elementAccess.Expression);
            var key = context.Converter.ConvertExpression(elementAccess.ArgumentList.Arguments[0].Expression);
            return JsExpr.Callish($"{Eq.DictGet}({map}, {key})");
        }

        var indexed = context.Converter.ConvertIr(elementAccess.Expression);
        foreach (var arg in elementAccess.ArgumentList.Arguments)
        {
            indexed = JsExpr.Index(indexed, context.Converter.ConvertIr(arg.Expression));
        }
        return indexed;
    }

    /// <summary>Whether this access is being WRITTEN to — the left of an assignment, or the
    /// operand of ++/--. Those create the entry rather than read it.</summary>
    private static bool IsAssignmentTarget(ElementAccessExpressionSyntax access) => access.Parent switch
    {
        AssignmentExpressionSyntax assignment => assignment.Left == access,
        // ++ and -- only. `-map[k]`, `!map[k]` and `~map[k]` are prefix unaries too, and they
        // READ — treating them as writes would put the missing key back to answering undefined,
        // which is the whole of what this change fixes.
        PrefixUnaryExpressionSyntax prefix =>
            prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression),
        PostfixUnaryExpressionSyntax postfix =>
            postfix.IsKind(SyntaxKind.PostIncrementExpression) || postfix.IsKind(SyntaxKind.PostDecrementExpression),
        ArgumentSyntax { RefOrOutKeyword.RawKind: not 0 } => true,
        _ => false,
    };

    public int Priority => 1;
}
