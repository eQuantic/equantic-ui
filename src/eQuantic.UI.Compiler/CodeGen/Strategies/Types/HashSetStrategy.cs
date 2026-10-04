using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// <c>HashSet&lt;T&gt;</c> and the faces it answers to, <c>ISet&lt;T&gt;</c> and <c>IReadOnlySet&lt;T&gt;</c>,
/// as the runtime's set (<c>utils/hash-set.ts</c>): its elements found by the element type's equality
/// (<see cref="ElementEquality"/>), the decision a dictionary's keys take, and held by slot as .NET's
/// are. A JavaScript <c>Set</c> found a date, a decimal, a tuple and a record by reference (#531) and
/// appended where .NET reuses a freed slot (#438).
/// <list type="bullet">
/// <item>A construction — <c>new HashSet&lt;T&gt;()</c>, <c>(capacity)</c>, <c>(collection)</c>, a
/// target-typed <c>new()</c> — is the factory, handed the equality and what the constructor is handed;
/// a collection initializer adds each element in turn, one <c>add</c> per element, as C# calls
/// <c>Add</c>. A comparer the fence lets through asks for the default (<c>EQ2007</c> refuses any
/// other).</item>
/// <item><c>Add</c> answers whether the element was new (<c>$eq.collections.setAdd</c>);
/// <c>Contains</c>, <c>Remove</c> and <c>Clear</c> are the set's <c>has</c>, <c>delete</c> and
/// <c>clear</c>; the rest of .NET's members are the set's own, by their camelCase names, each argument
/// in its parameter's place. <c>TryGetValue</c>, whose answer arrives through an <c>out</c>, has no
/// form yet, and is refused rather than called with half its arguments.</item>
/// </list>
/// </summary>
public class HashSetStrategy : IExpressionIrStrategy
{
    /// <summary>.NET's members the runtime's set answers by their camelCase names.</summary>
    private static readonly HashSet<string> OwnMembers = new(StringComparer.Ordinal)
    {
        "UnionWith", "IntersectWith", "ExceptWith", "SymmetricExceptWith",
        "IsSubsetOf", "IsProperSubsetOf", "IsSupersetOf", "IsProperSupersetOf", "Overlaps", "SetEquals",
        "RemoveWhere", "CopyTo", "EnsureCapacity", "TrimExcess",
    };

    public bool CanConvert(SyntaxNode node, ConversionContext context) => node switch
    {
        BaseObjectCreationExpressionSyntax creation => IsCreation(creation, context),
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member } invocation =>
            MemberOf(invocation, member, context) is not null,
        _ => false,
    };

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context) => node switch
    {
        BaseObjectCreationExpressionSyntax creation => Construction(creation, context),
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member } invocation =>
            Call(invocation, member, MemberOf(invocation, member, context)!, context),
        _ => JsExpr.Opaque(context.Unhandled(node, "HashSet")),
    };

    /// <summary>Whether a creation builds a <c>HashSet&lt;T&gt;</c>: by the type the model gives it, or,
    /// where the model cannot be asked, by the name it writes.</summary>
    private static bool IsCreation(BaseObjectCreationExpressionSyntax creation, ConversionContext context)
    {
        if (context.SemanticHelper.GetType(creation) is { } type) return IsHashSet(type);
        return context.CanGuess(creation)
            && creation is ObjectCreationExpressionSyntax { Type: var written }
            && (written as GenericNameSyntax ?? (written as QualifiedNameSyntax)?.Right as GenericNameSyntax)
                is { Identifier.Text: "HashSet", TypeArgumentList.Arguments.Count: 1 };
    }

    private static bool IsHashSet(ITypeSymbol? type) =>
        type is INamedTypeSymbol { TypeArguments.Length: 1 } named
        && named.OriginalDefinition.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic"
        && named.OriginalDefinition.Name == "HashSet";

    /// <summary>Whether a type IS the runtime's set on the other side: the class or one of its faces.</summary>
    private static bool IsSet(ITypeSymbol? type) =>
        type is INamedTypeSymbol { TypeArguments.Length: 1 } named
        && named.OriginalDefinition.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic"
        && named.OriginalDefinition.Name is "HashSet" or "ISet" or "IReadOnlySet";

    /// <summary>
    /// The set member a call is, or null when this invocation is not one: a member of the set's own,
    /// called on a receiver typed as a set. An extension (LINQ's, above all) is not the set's, and goes
    /// where every extension goes.
    /// </summary>
    private static string? MemberOf(InvocationExpressionSyntax invocation, MemberAccessExpressionSyntax member,
        ConversionContext context)
    {
        var name = member.Name.Identifier.Text;
        if (name is not ("Add" or "Contains" or "Remove" or "Clear" or "TryGetValue") && !OwnMembers.Contains(name))
            return null;
        if (context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol { IsExtensionMethod: true } or IMethodSymbol { ReducedFrom: not null })
            return null;
        return IsSet(context.SemanticHelper.GetType(member.Expression)) ? name : null;
    }

    private static JsExpr Call(InvocationExpressionSyntax invocation, MemberAccessExpressionSyntax member, string name,
        ConversionContext context)
    {
        var receiver = context.Converter.ConvertIr(member.Expression);
        var method = context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol;
        switch (name)
        {
            // `Add` ANSWERS something in C#: true when the value was new. A JS Set.add returns the set
            // — always truthy — so `if (!set.Add(x)) set.Remove(x)` quietly became "add, and never
            // remove", which is a checkbox that ticks once and then ignores you.
            case "Add":
                context.UsedHelpers.Add(Eq.Import);
                return ParameterTemplate.Call($"{Eq.SetAdd}({{0}}, {{1}})", receiver, invocation, method, context);
            case "Contains":
                return ParameterTemplate.Call("{0}.has({1})", receiver, invocation, method, context);
            case "Remove":
                return ParameterTemplate.Call("{0}.delete({1})", receiver, invocation, method, context);
            case "Clear":
                return JsExpr.Call(JsExpr.Member(receiver, "clear"));
            case "TryGetValue":
                return JsExpr.Opaque(context.Unhandled(invocation, "HashSet.TryGetValue, whose value arrives through an out"));
        }
        var count = invocation.ArgumentList.Arguments.Count;
        var holes = string.Join(", ", Enumerable.Range(1, count).Select(index => "{" + index + "}"));
        return ParameterTemplate.Call($"{{0}}.{name.ToCamelCase()}({holes})", receiver, invocation, method, context);
    }

    /// <summary>
    /// The factory, handed the element type's equality and what the constructor is handed — a
    /// collection or a capacity — and then the initializer's elements, one <c>add</c> each, in order.
    /// </summary>
    private static JsExpr Construction(BaseObjectCreationExpressionSyntax creation, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        var type = context.SemanticHelper.GetType(creation) as INamedTypeSymbol;
        var equality = ElementEquality.Of(type?.TypeArguments.FirstOrDefault());
        var arguments = new List<JsExpr>();
        JsExpr? source = null;
        if (context.SemanticHelper.GetOperation(creation) is IObjectCreationOperation operation)
        {
            foreach (var argument in operation.Arguments)
            {
                if (argument.ArgumentKind == ArgumentKind.DefaultValue) continue;
                // A comparer that reached here asks for the element type's own (EQ2007 refuses any other).
                if (argument.Parameter?.Type.Name == "IEqualityComparer") continue;
                source = context.Converter.ConvertIr((ExpressionSyntax)argument.Value.Syntax);
            }
        }
        else if (creation.ArgumentList is { Arguments.Count: 1 } list)
        {
            source = context.Converter.ConvertIr(list.Arguments[0].Expression);
        }

        if (source is not null || equality is not null) arguments.Add(JsExpr.Literal(equality ?? "false"));
        if (source is not null) arguments.Add(source);
        JsExpr set = JsExpr.Call(JsExpr.Identifier(Eq.HashSet), arguments);
        if (creation.Initializer is not { } initializer) return set;
        foreach (var element in initializer.Expressions)
        {
            var added = element is InitializerExpressionSyntax { Expressions.Count: 1 } braced ? braced.Expressions[0] : element;
            set = JsExpr.Call(JsExpr.Member(set, "add"), context.Converter.ConvertIr(added));
        }
        return set;
    }

    public int Priority => 10;
}
