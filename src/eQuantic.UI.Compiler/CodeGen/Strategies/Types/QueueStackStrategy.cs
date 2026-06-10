using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Maps the sequence-shaped <c>System.Collections.Generic</c> compat collections —
/// <c>Queue&lt;T&gt;</c>, <c>Stack&lt;T&gt;</c>, <c>LinkedList&lt;T&gt;</c> and <c>SortedSet&lt;T&gt;</c> —
/// to their runtime equivalents: <c>new Queue&lt;T&gt;(...)</c> -> <c>$eq.collections.queue(...)</c>
/// (and <c>stack</c>/<c>linkedList</c>/<c>sortedSet</c>), with instance members/methods (<c>Enqueue</c>,
/// <c>Push</c>, <c>AddFirst</c>, <c>Add</c>, <c>Count</c>, <c>First</c>, <c>Min</c>, <c>Contains</c>,
/// <c>ToArray</c>, <c>Clear</c>, …) -> camelCase. Priority 15, type-gated. (The key-sorted
/// <c>SortedDictionary</c>/<c>SortedList</c> are handled by their own dictionary strategy.)
/// </summary>
public class QueueStackStrategy : ConversionStrategyBase
{
    public override bool CanConvert(SyntaxNode node, ConversionContext context)
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

    public override string Convert(SyntaxNode node, ConversionContext context)
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
                return $"{receiver}.{ma.Name.Identifier.Text.ToCamelCase()}({ConvertArgs(inv.ArgumentList, context)})";
            }

            case MemberAccessExpressionSyntax member:
            {
                var receiver = context.Converter.ConvertExpression(member.Expression);
                return $"{receiver}.{member.Name.Identifier.Text.ToCamelCase()}";
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

    /// <summary>Returns the runtime factory name ("queue"/"stack"/"linkedList"/"sortedSet") for the
    /// matching generic type, else null.</summary>
    private static string? KindOf(ITypeSymbol? type)
    {
        if (type == null) return null;
        if (type.ContainingNamespace?.ToDisplayString() != "System.Collections.Generic") return null;
        return KindOfName(type.Name);
    }

    private static string? KindOfName(string name) =>
        name.StartsWith("Queue") ? "queue"
        : name.StartsWith("Stack") ? "stack"
        : name.StartsWith("LinkedList") ? "linkedList"
        : name.StartsWith("SortedSet") ? "sortedSet"
        : null;

    public override int Priority => 15;
}
