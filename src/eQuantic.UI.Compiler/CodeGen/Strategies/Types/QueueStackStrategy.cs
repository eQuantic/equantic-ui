using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Maps the sequence-shaped <c>System.Collections.Generic</c> compat collections —
/// <c>Queue&lt;T&gt;</c>, <c>Stack&lt;T&gt;</c>, <c>LinkedList&lt;T&gt;</c> with its
/// <c>LinkedListNode&lt;T&gt;</c>, and <c>SortedSet&lt;T&gt;</c> — to their runtime twins:
/// <c>new Queue&lt;T&gt;(...)</c> -> <c>$eq.collections.queue(...)</c> (and <c>stack</c>,
/// <c>linkedList</c>, <c>linkedListNode</c>, <c>sortedSet</c>), and each member a twin carries to that
/// twin's member. Priority 15, type-gated. (The key-sorted <c>SortedDictionary</c>/<c>SortedList</c> are
/// handled by their own dictionary strategy.)
/// <para>
/// A member is the twin's by the symbol that declares it, and any other member of the five is refused
/// (EQ2004). Every member went out under its camel-cased name whether or not the twin carried it, so
/// <c>TryDequeue</c>, <c>TryPop</c>, <c>Find</c> and <c>AddAfter</c> compiled and were a TypeError in the
/// browser, <c>Remove(node)</c> removed nothing and <c>new LinkedListNode&lt;T&gt;(value)</c> built a
/// list (#866). The members that take an <c>out</c> answer through it as <c>TryParse</c> does
/// (<see cref="OutArgument"/>), and an overload that takes a node is the twin named for the node.
/// </para>
/// </summary>
public class QueueStackStrategy : IExpressionIrStrategy
{
    private const string Queue = "System.Collections.Generic.Queue<T>";
    private const string Stack = "System.Collections.Generic.Stack<T>";
    private const string LinkedList = "System.Collections.Generic.LinkedList<T>";
    private const string LinkedListNode = "System.Collections.Generic.LinkedListNode<T>";
    private const string SortedSet = "System.Collections.Generic.SortedSet<T>";

    /// <summary>The members each runtime twin carries, by the type that declares them. What is not here
    /// is refused: serialization's hooks, a node's <c>ValueRef</c>, a sorted set's <c>Comparer</c> and
    /// <c>CreateSetComparer</c>, and an explicit <c>GetEnumerator</c>. A sorted set's
    /// <c>GetViewBetween</c> is the twin's since #865, a view sharing the set's elements.</summary>
    private static readonly Dictionary<string, HashSet<string>> Members = new()
    {
        [Queue] =
        [
            "Count", "Capacity", "Enqueue", "Dequeue", "TryDequeue", "Peek", "TryPeek", "Contains", "Clear",
            "ToArray", "CopyTo", "EnsureCapacity", "TrimExcess",
        ],
        [Stack] =
        [
            "Count", "Capacity", "Push", "Pop", "TryPop", "Peek", "TryPeek", "Contains", "Clear", "ToArray",
            "CopyTo", "EnsureCapacity", "TrimExcess",
        ],
        [LinkedList] =
        [
            "Count", "First", "Last", "AddFirst", "AddLast", "AddAfter", "AddBefore", "Remove", "RemoveFirst",
            "RemoveLast", "Find", "FindLast", "Contains", "Clear", "CopyTo",
        ],
        [LinkedListNode] = ["Value", "Next", "Previous", "List"],
        [SortedSet] =
        [
            "Count", "Min", "Max", "Add", "Remove", "Contains", "TryGetValue", "Clear", "CopyTo", "UnionWith",
            "IntersectWith", "ExceptWith", "SymmetricExceptWith", "IsSubsetOf", "IsProperSubsetOf",
            "IsSupersetOf", "IsProperSupersetOf", "Overlaps", "SetEquals", "RemoveWhere", "Reverse",
            "GetViewBetween",
        ],
    };

    /// <summary>The members that answer through an <c>out</c>: the twin answers the value, or undefined
    /// where .NET answers false.</summary>
    private static readonly HashSet<string> Takes = ["TryDequeue", "TryPeek", "TryPop", "TryGetValue"];

    public bool CanConvert(SyntaxNode node, ConversionContext context) => node switch
    {
        // Target-typed `new(…)` included (BaseObjectCreation) — see DateTimeStrategy.
        BaseObjectCreationExpressionSyntax creation => KindOfCreation(creation, context) is not null,
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax callee } invocation =>
            DeclaringType(context.SemanticHelper.GetSymbol(invocation)) is not null
            || DeclaringType(context.SemanticHelper.GetSymbol(callee)) is not null,
        // The callee of a call is the call's to translate, with its arguments.
        MemberAccessExpressionSyntax access => (access.Parent is not InvocationExpressionSyntax { Expression: var callee }
                || callee != access)
            && DeclaringType(context.SemanticHelper.GetSymbol(access)) is not null,
        _ => false,
    };

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        switch (node)
        {
            case BaseObjectCreationExpressionSyntax creation:
                return Creation(creation, context);

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax callee } invocation:
            {
                var method = (context.SemanticHelper.GetSymbol(invocation) ?? context.SemanticHelper.GetSymbol(callee)) as IMethodSymbol;
                if (method is null) return JsExpr.Opaque(context.Unhandled(invocation, "Queue/Stack"));
                if (!Supported(method)) return Refused(invocation, method, context);
                var receiver = context.Converter.ConvertIr(callee.Expression);
                if (Takes.Contains(method.Name)) return Take(invocation, method, receiver, context);
                var holes = string.Join(", ", Enumerable.Range(1, invocation.ArgumentList.Arguments.Count).Select(index => "{" + index + "}"));
                // Each parameter's argument in its place, a named one included, evaluated in the order
                // it is written: `q.CopyTo(arrayIndex: 1, array: a)` handed the index as the array.
                return ParameterTemplate.Call($"{{0}}.{TwinName(method)}({holes})", receiver, invocation, method, context);
            }

            case MemberAccessExpressionSyntax access:
            {
                var symbol = context.SemanticHelper.GetSymbol(access)!;
                if (!Supported(symbol)) return Refused(access, symbol, context);
                var receiver = context.Converter.ConvertIr(access.Expression);
                if (symbol is not IMethodSymbol method)
                    return SortedSetEnd(receiver, symbol, context) ?? JsExpr.Member(receiver, symbol.Name.ToCamelCase());
                // A method REFERENCE is a method group, and a group keeps its receiver, read once, as C#
                // reads it when the delegate is made: `items.ForEach(queue.Enqueue)` lost it, and each
                // call threw a TypeError on `this`. A take answers through an out, which no group can.
                if (Takes.Contains(method.Name)) return Refused(access, method, context);
                return JsExpr.Template($"{{0}}.{TwinName(method)}.bind({{0}})", [receiver]);
            }
        }
        return JsExpr.Opaque(context.Unhandled(node, "Queue/Stack"));
    }

    /// <summary>
    /// A sorted set's <c>Min</c> or <c>Max</c> over a value type: the twin's element, or the type's default
    /// where the set holds nothing, as .NET answers it. The twin has no element to answer then, so
    /// <c>new SortedSet&lt;int&gt;().Min + 1</c> was NaN where .NET says 1. A reference or a nullable
    /// element's default is null, which the twin's nothing already reads as, and a type parameter's is
    /// unknown here. ONE rule for a member access, a <c>?.</c>, which reads its member as one, and a
    /// property pattern. The read is grouped, so a strategy that still writes text can take it as a
    /// receiver (<c>s.Min.Year</c>), and a <c>?.</c> guards it rather than chain into it, where
    /// <c>s?.min ?? 0</c> would answer 0 for a set that is null. Null for any other member.
    /// </summary>
    internal static JsExpr? SortedSetEnd(JsExpr receiver, ISymbol? member, ConversionContext context)
    {
        if (member is not IPropertySymbol { Name: "Min" or "Max" } end || DeclaringType(end) != SortedSet
            || end.Type is not { IsValueType: true } element || element is ITypeParameterSymbol
            || element.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            return null;
        return JsExpr.Group(JsExpr.Binary(JsExpr.Member(receiver, end.Name.ToCamelCase()), "??",
            JsExpr.Opaque(DefaultValue.Of(element, context))));
    }

    /// <summary>
    /// A take — <c>TryDequeue</c>, <c>TryPeek</c>, <c>TryPop</c> and <c>TryGetValue</c> — over the twin's
    /// member, which answers the value or undefined: true with the value in the out argument, or false
    /// with the element type's default there, what .NET leaves. The value the set is asked for is read
    /// in its place among the arguments.
    /// </summary>
    private static JsExpr Take(InvocationExpressionSyntax invocation, IMethodSymbol method, JsExpr receiver,
        ConversionContext context)
    {
        var arguments = invocation.ArgumentList.Arguments;
        var result = arguments.First(argument => argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword));
        var input = arguments.FirstOrDefault(argument => !argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword));
        var name = method.Name.ToCamelCase();
        // C# reads the receiver before every argument. An out named before the value the set is asked
        // for has its place read first (`Get().TryGetValue(actualValue: out slots[i++], equalValue: k)`),
        // and a receiver read inside the call ran after it: the receiver is then bound first.
        var placeFirst = input is not null && result.SpanStart < input.SpanStart
            && !OutArgument.IsDiscard(result, context) && !OutArgument.IsBareName(OutArgument.Target(result, context))
            && receiver is not JsLiteral and not JsIdentifier { Name: "this" };
        var target = placeFirst ? JsExpr.Identifier("$receiver") : receiver;
        var read = input is null
            ? JsExpr.Call(JsExpr.Member(target, name))
            : JsExpr.Call(JsExpr.Member(target, name), context.Converter.ConvertIr(input.Expression));
        var outType = method.Parameters.First(parameter => parameter.RefKind == RefKind.Out).Type;
        var answer = JsExpr.Callish(OutArgument.TryAnswer(result, input?.SpanStart ?? invocation.SpanStart, read,
            DefaultValue.Of(outType, context), context));
        return placeFirst ? JsExpr.Call(JsExpr.Arrow("$receiver", answer), receiver) : answer;
    }

    /// <summary>The twin's name for a member: its own in camelCase, and for an overload that takes a node
    /// where another takes a value, the one named for the node (<c>AddLast(node)</c> is
    /// <c>addNodeLast</c>, <c>Remove(node)</c> is <c>removeNode</c>).</summary>
    private static string TwinName(IMethodSymbol method)
    {
        var name = method.Name.ToCamelCase();
        if (method.ContainingType.OriginalDefinition.ToDisplayString() != LinkedList) return name;
        // The node the overload takes where its sibling takes a value is its last parameter:
        // `AddAfter(node, newNode)` beside `AddAfter(node, value)`, `Remove(node)` beside `Remove(value)`.
        var takesANode = method.OriginalDefinition.Parameters.LastOrDefault()?.Type.OriginalDefinition
            .ToDisplayString() == LinkedListNode;
        return method.Name switch
        {
            "AddFirst" or "AddLast" or "AddAfter" or "AddBefore" when takesANode => "addNode" + method.Name[3..],
            "Remove" when takesANode => "removeNode",
            _ => name,
        };
    }

    /// <summary>The construction: the twin's factory over what the constructor is handed, a sorted set's
    /// ordering beside its elements, and then the initializer's elements, one <c>Add</c> each.</summary>
    private static JsExpr Creation(BaseObjectCreationExpressionSyntax creation, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        var resolved = context.SemanticHelper.GetType(creation);
        var kind = KindOfCreation(creation, context) ?? "queue";
        JsExpr construction = kind == "sortedSet" && SortedSetConstruction(creation, resolved, context) is { } sorted
            ? sorted
            : JsExpr.Call(JsExpr.Identifier($"$eq.collections.{kind}"),
                (creation.ArgumentList?.Arguments ?? default).Select(argument => context.Converter.ConvertIr(argument.Expression)).ToList());
        // `new SortedSet<int> { 2, 1 }` is a construction and then one Add per element, as C# runs it.
        // The initializer was dropped, so the set began empty in the browser alone. A node's `Value` is
        // settable, so `new LinkedListNode<int>(1) { Value = 2 }` assigns it, where it went out as a
        // call to an `add` no node has.
        if (creation.Initializer is not { Expressions.Count: > 0 } initializer) return construction;
        return initializer.IsKind(SyntaxKind.ObjectInitializerExpression)
            ? Expressions.ObjectInitializer.Apply(construction, initializer, context)
            : Expressions.ObjectCreationStrategy.AddPerElementConstruction(initializer, construction, context);
    }

    /// <summary>The empty queue, stack or linked list a collection expression builds
    /// (<c>Queue&lt;int&gt; pending = []</c>), constructed as <c>new()</c> constructs it, with the capacity
    /// its <c>with(…)</c> hands the constructor. It went out as an array (#1144).</summary>
    internal static JsExpr Empty(string kind, JsExpr? capacity, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        var factory = JsExpr.Identifier($"$eq.collections.{kind}");
        return capacity is null ? JsExpr.Call(factory) : JsExpr.Call(factory, capacity);
    }

    /// <summary>
    /// <c>new SortedSet&lt;T&gt;(…)</c>: the elements it copies, and the order it keeps them in, which is
    /// its element type's (<see cref="ValueOrdering"/>) unless the comparer it is handed asks for the
    /// code-unit one. Every set ordered its elements by <c>&lt;</c>, so strings did not follow the
    /// current culture and decimals compared their text; and a comparer reached the factory as the
    /// elements to copy, which threw. A collection it copies is followed by the name of the parameter
    /// it binds to, by which the factory refuses a null one (#726). Null where the model cannot bind
    /// the construction.
    /// </summary>
    private static JsExpr? SortedSetConstruction(BaseObjectCreationExpressionSyntax creation, ITypeSymbol? type,
        ConversionContext context)
    {
        if (type is not INamedTypeSymbol { TypeArguments: [var element] }
            || context.SemanticHelper.GetOperation(creation) is not IObjectCreationOperation operation)
            return null;
        var ordering = ValueOrdering.Of(element);
        JsExpr? source = null;
        string? parameter = null;
        foreach (var argument in operation.Arguments)
        {
            if (argument.ArgumentKind == ArgumentKind.DefaultValue) continue;
            if ((argument.Parameter?.Type).IsCollectionComparer())
                ordering = argument.Value.OrderingAskedFor(element);
            else
            {
                source = context.Converter.ConvertIr((ExpressionSyntax)argument.Value.Syntax);
                parameter = argument.Parameter?.Name;
            }
        }
        var factory = JsExpr.Identifier("$eq.collections.sortedSet");
        if (source is not null && parameter is not null)
            return JsExpr.Call(factory, source, JsExpr.Literal(ordering ?? "undefined"), JsExpr.Literal($"'{parameter}'"));
        if (ordering is null)
            return source is null ? JsExpr.Call(factory) : JsExpr.Call(factory, source);
        return JsExpr.Call(factory, source ?? JsExpr.Literal("null"), JsExpr.Literal(ordering));
    }

    /// <summary>Whether the runtime's twin carries <paramref name="member"/>.</summary>
    private static bool Supported(ISymbol member) =>
        DeclaringType(member) is { } declaring && Members[declaring].Contains(member.Name);

    private static JsExpr Refused(SyntaxNode node, ISymbol member, ConversionContext context)
    {
        var declaring = DeclaringType(member)!;
        context.Report(node, ConversionSeverity.Error, "EQ2004",
            $"'{member.ContainingType.ToDisplayString()}.{member.Name}' has no JavaScript translation: the "
            + $"runtime's {member.ContainingType.Name} carries {string.Join(", ", Members[declaring].Order(StringComparer.Ordinal))}, "
            + "and this is not one of them.");
        return JsExpr.Opaque("undefined");
    }

    /// <summary>The one of the five types that declares <paramref name="member"/>, or null. A LINQ
    /// operator is an extension, which the LINQ strategies lower: claimed here, <c>queue.First()</c>
    /// called a <c>first</c> the queue does not have.</summary>
    private static string? DeclaringType(ISymbol? member)
    {
        if (member is IMethodSymbol { IsExtensionMethod: true } or IMethodSymbol { ReducedFrom: not null }) return null;
        if (member?.ContainingType?.OriginalDefinition.ToDisplayString() is not { } declaring) return null;
        return Members.ContainsKey(declaring) ? declaring : null;
    }

    /// <summary>The runtime factory a construction calls ("queue", "stack", "linkedList",
    /// "linkedListNode", "sortedSet"), or null. Semantic resolution is AUTHORITATIVE: a resolved
    /// non-collection type (e.g. the Photon vocabulary <c>Stack</c>) must not fall through to the name,
    /// which only speaks for snippets with no semantic model.</summary>
    private static string? KindOfCreation(BaseObjectCreationExpressionSyntax creation, ConversionContext context)
    {
        var resolved = context.SemanticHelper.GetType(creation);
        if (resolved is not null)
        {
            return resolved.OriginalDefinition.ToDisplayString() switch
            {
                Queue => "queue",
                Stack => "stack",
                LinkedList => "linkedList",
                LinkedListNode => "linkedListNode",
                SortedSet => "sortedSet",
                _ => null,
            };
        }
        if (creation is not ObjectCreationExpressionSyntax { Type: var written }) return null;
        var name = (written as GenericNameSyntax ?? (written as QualifiedNameSyntax)?.Right as GenericNameSyntax)?.Identifier.ValueText;
        return name switch
        {
            "Queue" => "queue",
            "Stack" => "stack",
            "LinkedList" => "linkedList",
            "LinkedListNode" => "linkedListNode",
            "SortedSet" => "sortedSet",
            _ => null,
        };
    }

    public int Priority => 15;
}
