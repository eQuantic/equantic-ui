using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Extensions;

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
            // Target-typed `new(…)` included (BaseObjectCreation) — see DateTimeStrategy.
            case BaseObjectCreationExpressionSyntax oc:
            {
                // Semantic resolution is AUTHORITATIVE: a resolved non-collection type (e.g. the Photon
                // vocabulary `Stack`) must not fall through to the name heuristic — that heuristic only
                // covers snippets with no semantic model.
                var resolved = context.SemanticHelper.GetType(oc);
                if (resolved != null) return KindOf(resolved) != null;
                return oc is ObjectCreationExpressionSyntax named && KindOfName(named.Type.ToString()) != null;
            }

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
            case BaseObjectCreationExpressionSyntax oc:
            {
                context.UsedHelpers.Add(Eq.Import);
                var resolvedType = context.SemanticHelper.GetType(oc);
                var kind = KindOf(resolvedType)
                    ?? (resolvedType is null && oc is ObjectCreationExpressionSyntax named
                        ? KindOfName(named.Type.ToString())
                        : null)
                    ?? "queue";
                var construction = kind == "sortedSet" && SortedSetConstruction(oc, resolvedType, context) is { } sorted
                    ? sorted
                    : $"$eq.collections.{kind}({ConvertArgs(oc.ArgumentList, context)})";
                // `new SortedSet<int> { 2, 1 }` is a construction and then one Add per element, as
                // C# runs it. The initializer was dropped, so the set began empty in the browser alone.
                return oc.Initializer is { } initializer && initializer.Expressions.Count > 0
                    ? Expressions.ObjectCreationStrategy.AddPerElementConstruction(initializer, construction, context)
                    : construction;
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
                return context.Unhandled(node, "Queue/Stack");
        }
    }

    /// <summary>
    /// <c>new SortedSet&lt;T&gt;(…)</c>: the elements it copies, and the order it keeps them in, which is
    /// its element type's (<see cref="ValueOrdering"/>) unless the comparer it is handed asks for the
    /// code-unit one. Every set ordered its elements by <c>&lt;</c>, so strings did not follow the
    /// current culture and decimals compared their text; and a comparer reached the factory as the
    /// elements to copy, which threw. Null where the model cannot bind the construction.
    /// </summary>
    private static string? SortedSetConstruction(BaseObjectCreationExpressionSyntax creation, ITypeSymbol? type,
        ConversionContext context)
    {
        if (type is not INamedTypeSymbol { TypeArguments: [var element] }
            || context.SemanticHelper.GetOperation(creation) is not IObjectCreationOperation operation)
            return null;
        var ordering = ValueOrdering.Of(element);
        string? source = null;
        foreach (var argument in operation.Arguments)
        {
            if (argument.ArgumentKind == ArgumentKind.DefaultValue) continue;
            if (argument.Parameter?.Type.Name == "IComparer")
                ordering = argument.Value.OrderingAskedFor(element);
            else
                source = context.Converter.ConvertExpression((ExpressionSyntax)argument.Value.Syntax);
        }
        return ordering is null
            ? $"$eq.collections.sortedSet({source})"
            : $"$eq.collections.sortedSet({source ?? "null"}, {ordering})";
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
