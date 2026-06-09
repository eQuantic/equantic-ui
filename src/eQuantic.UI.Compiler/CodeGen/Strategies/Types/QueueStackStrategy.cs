using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Maps <c>System.Collections.Generic.Queue&lt;T&gt;</c> and <c>Stack&lt;T&gt;</c> to the runtime
/// compat types: <c>new Queue&lt;T&gt;(...)</c> -> <c>$eq.collections.queue(...)</c> (and <c>stack</c>),
/// instance members/methods (<c>Enqueue</c>, <c>Dequeue</c>, <c>Push</c>, <c>Pop</c>, <c>Peek</c>,
/// <c>Count</c>, <c>Contains</c>, <c>ToArray</c>, <c>Clear</c>) -> camelCase. Priority 15, type-gated.
/// </summary>
public class QueueStackStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        switch (node)
        {
            case ObjectCreationExpressionSyntax oc:
                return KindOf(context.SemanticHelper.GetType(oc)) != null || KindOfName(oc.Type.ToString()) != null;

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma }:
                return IsMember(ma, context);

            case MemberAccessExpressionSyntax member:
                return IsMember(member, context);

            default:
                return false;
        }
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        switch (node)
        {
            case ObjectCreationExpressionSyntax oc:
            {
                context.UsedHelpers.Add(Eq.Import);
                var kind = KindOf(context.SemanticHelper.GetType(oc)) ?? KindOfName(oc.Type.ToString()) ?? "queue";
                return $"$eq.collections.{kind}({ConvertArgs(oc.ArgumentList, context)})";
            }

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma } inv:
            {
                var receiver = context.Converter.ConvertExpression(ma.Expression);
                return $"{receiver}.{Camel(ma.Name.Identifier.Text)}({ConvertArgs(inv.ArgumentList, context)})";
            }

            case MemberAccessExpressionSyntax member:
            {
                var receiver = context.Converter.ConvertExpression(member.Expression);
                return $"{receiver}.{Camel(member.Name.Identifier.Text)}";
            }

            default:
                return node.ToString();
        }
    }

    private static bool IsMember(MemberAccessExpressionSyntax ma, ConversionContext context)
    {
        // The receiver's TYPE is the reliable signal (a local Queue/Stack variable). The member symbol's
        // ContainingType can resolve to a LINQ extension or interface (ToArray/Contains/Count on Stack),
        // which would miss — so check the receiver type first.
        if (KindOf(context.SemanticHelper.GetType(ma.Expression)) != null) return true;

        var symbol = context.SemanticHelper.GetSymbol(ma);
        return symbol?.ContainingType != null && KindOf(symbol.ContainingType) != null;
    }

    /// <summary>Returns "queue"/"stack" for the matching generic type, else null.</summary>
    private static string? KindOf(ITypeSymbol? type)
    {
        if (type == null) return null;
        if (type.ContainingNamespace?.ToDisplayString() != "System.Collections.Generic") return null;
        return KindOfName(type.Name);
    }

    private static string? KindOfName(string name) =>
        name.StartsWith("Queue") ? "queue" : name.StartsWith("Stack") ? "stack" : null;

    private static string ConvertArgs(ArgumentListSyntax? argumentList, ConversionContext context)
    {
        if (argumentList == null || argumentList.Arguments.Count == 0) return string.Empty;
        return string.Join(", ", argumentList.Arguments.Select(a => context.Converter.ConvertExpression(a.Expression)));
    }

    private static string Camel(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];

    public int Priority => 15;
}
