using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Every dictionary of System.Collections.Generic — <c>Dictionary</c>, <c>IDictionary</c>,
/// <c>IReadOnlyDictionary</c>, <c>SortedDictionary</c> and <c>SortedList</c> — lowered to the runtime
/// class it is on this side: the runtime's <c>Dictionary</c>, which holds its entries by slot as .NET's
/// does, or the <c>SortedMap</c> of a sorted one. This strategy owns construction (a copy included),
/// both initializer forms, the entry's plain write, the members the classes answer
/// (<c>ContainsKey</c>, <c>Add</c>, <c>Remove</c>, <c>Clear</c>, <c>TryGetValue</c>,
/// <c>GetValueOrDefault</c>, <c>Keys</c>, <c>Values</c>, <c>Count</c>), and a lookup through the keys
/// (<c>d.Keys.Contains(k)</c>, <c>d.Keys.Count</c>).
/// <para>
/// The ENTRY's read and its read-modify-writes are not here: they go where every compound target's
/// go, and take the classes' read and write from <see cref="DictionaryEntry"/>.
/// </para>
/// <para>
/// A key is found as <c>EqualityComparer&lt;TKey&gt;.Default</c> finds it, which eqc decides from the
/// key type (<see cref="ElementEquality"/>), the factory's second argument: by identity, through the
/// class's JavaScript Map, by value, through <c>$eq.equals</c>, or, where the key type does not decide,
/// by the key's own equality, which the runtime asks the value for. A comparer is not this strategy's
/// to judge: the fence every creation passes (<see cref="CollectionComparerExtensions"/>,
/// EQ2007) refuses one that changes equality, so a comparer that reaches the construction asks for
/// what the lowering already does, and a sorted dictionary keeps the order it asks for.
/// </para>
/// <para>
/// Where the model cannot be asked (<see cref="ConversionContext.CanGuess"/>), a creation is known
/// by the name it writes, and a call by the two names no other collection answers,
/// <c>ContainsKey</c> and <c>TryGetValue</c>; every key is then found by identity.
/// </para>
/// </summary>
internal sealed class DictionaryStrategy : IExpressionIrStrategy
{
    public int Priority => 25;

    public bool CanConvert(SyntaxNode node, ConversionContext context) => node switch
    {
        BaseObjectCreationExpressionSyntax creation => FactoryOf(creation, context) is not null,
        AssignmentExpressionSyntax { Left: ElementAccessExpressionSyntax entry } assignment =>
            assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) && DictionaryEntry.Of(entry, context) is not null,
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member } invocation =>
            CallOf(invocation, member, context) is not null,
        MemberAccessExpressionSyntax member => ReadOf(member, context) is not null,
        _ => false,
    };

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        switch (node)
        {
            case BaseObjectCreationExpressionSyntax creation:
                return Construction(creation, context);

            case AssignmentExpressionSyntax { Left: ElementAccessExpressionSyntax entry } assignment:
                // The entry's write answers the value, as C#'s assignment does: `set` answers the dictionary.
                context.UsedHelpers.Add(Eq.Import);
                return DictionaryEntry.Write(
                    context.Converter.ConvertIr(entry.Expression),
                    context.Converter.ConvertIr(entry.ArgumentList.Arguments[0].Expression),
                    context.Converter.ConvertIr(assignment.Right));

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member } invocation
                when CallOf(invocation, member, context) is { } call:
                return Call(invocation, member, call, context);

            case MemberAccessExpressionSyntax member when ReadOf(member, context) is { } read:
                return Read(member, read, context);

            default:
                return context.Unhandled(node, "Dictionary");
        }
    }

    /// <summary>
    /// The dictionary an initializer nested under a dictionary-typed member seeds (<c>Map = { ["a"] = 1 }</c>),
    /// built as a constructed one is.
    /// </summary>
    internal static JsExpr Seeded(ITypeSymbol type, InitializerExpressionSyntax initializer, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        return Initialized(Factory(type.DictionaryFactory()!, type, null), initializer, context);
    }

    /// <summary>
    /// The dictionary with its initializer's entries, one call each, in the order C# makes them: a
    /// collection initializer's <c>{ key, value }</c> by <c>add</c>, which refuses a key already there,
    /// and an object initializer's <c>[key] = value</c> by the indexer's <c>set</c>, which replaces it
    /// (#440). One call per entry, chained, so an entry that throws stops the ones after it before
    /// their keys and values are evaluated, as C# stops them: listed as one array, they all ran first.
    /// </summary>
    private static JsExpr Initialized(JsExpr dictionary, InitializerExpressionSyntax? initializer, ConversionContext context)
    {
        if (initializer is null) return dictionary;
        var member = initializer.IsKind(SyntaxKind.ObjectInitializerExpression) ? "set" : "add";
        foreach (var element in initializer.Expressions)
        {
            switch (element)
            {
                case InitializerExpressionSyntax { Expressions.Count: 2 } pair:
                    dictionary = JsExpr.Call(JsExpr.Member(dictionary, member),
                        context.Converter.ConvertIr(pair.Expressions[0]), context.Converter.ConvertIr(pair.Expressions[1]));
                    break;
                case AssignmentExpressionSyntax { Left: ImplicitElementAccessSyntax { ArgumentList.Arguments.Count: 1 } key } assignment:
                    dictionary = JsExpr.Call(JsExpr.Member(dictionary, member),
                        context.Converter.ConvertIr(key.ArgumentList.Arguments[0].Expression), context.Converter.ConvertIr(assignment.Right));
                    break;
                default:
                    context.Unhandled(element, "Dictionary initializer");
                    break;
            }
        }
        return dictionary;
    }

    /// <summary>The factory a creation constructs by: its type's, where the model knows the type, and
    /// the one the name it writes says only where the model cannot be asked.</summary>
    private static string? FactoryOf(BaseObjectCreationExpressionSyntax creation, ConversionContext context)
    {
        if (context.SemanticHelper.GetType(creation) is { } type) return type.DictionaryFactory();
        if (!context.CanGuess(creation)
            || creation is not ObjectCreationExpressionSyntax { Type: var written }
            || (written as GenericNameSyntax ?? (written as QualifiedNameSyntax)?.Right as GenericNameSyntax)
                is not { TypeArgumentList.Arguments.Count: 2 } generic)
        {
            return null;
        }
        return generic.Identifier.Text switch
        {
            "Dictionary" => Eq.Dictionary,
            "SortedDictionary" => Eq.SortedDictionary,
            "SortedList" => Eq.SortedList,
            _ => null,
        };
    }

    /// <summary>
    /// <c>new Dictionary&lt;K, V&gt;(…) { … }</c>: the factory, seeded by the dictionary or the pairs the
    /// constructor copies, and then the initializer's entries, one call each. A capacity has no meaning
    /// here. A comparer is never the seed: the fence has judged it (see the type's summary), and a
    /// sorted dictionary takes the order it asks for (<see cref="CollectionComparerExtensions.OrderingAskedFor"/>),
    /// the code-unit order for <c>StringComparer.Ordinal</c>.
    /// </summary>
    /// <remarks>
    /// #443 refused every constructor with a comparer PARAMETER, whatever its argument, so
    /// <c>new Dictionary&lt;string, T&gt;(StringComparer.Ordinal)</c>, which is the default for a string
    /// key, failed the build with EQ1004 where 0.2.0-preview.59 built it (#577, met by a site
    /// upgrading to 0.2.0-preview.60). That was a second, stricter copy of the fence, the shape the
    /// fence's own documentation says a copy takes.
    /// </remarks>
    private static JsExpr Construction(BaseObjectCreationExpressionSyntax creation, ConversionContext context)
    {
        var type = context.SemanticHelper.GetType(creation);
        var factory = FactoryOf(creation, context)!;
        context.UsedHelpers.Add(Eq.Import);
        JsExpr? source = null;
        string? ordering = null;

        if (context.SemanticHelper.GetOperation(creation) is IObjectCreationOperation operation)
        {
            // Read in PARAMETER order, which the bound operation gives whatever order a named argument
            // was written in. Only the seed is converted: a capacity and a comparer are dropped, and
            // the fence has already refused a comparer whose evaluation could matter.
            var key = factory != Eq.Dictionary && type is INamedTypeSymbol { TypeArguments: [var keyType, _] }
                ? keyType
                : null;
            foreach (var argument in operation.Arguments)
            {
                if (argument.ArgumentKind == ArgumentKind.DefaultValue) continue;
                var parameter = argument.Parameter?.Type;
                if (parameter?.SpecialType == SpecialType.System_Int32) continue;
                if (parameter.IsCollectionComparer())
                {
                    if (key is not null) ordering = argument.Value.OrderingAskedFor(key);
                    continue;
                }
                source = context.Converter.ConvertIr((ExpressionSyntax)argument.Value.Syntax);
            }
        }
        else
        {
            // No operation to read. Where the model still binds the constructor (a node only a
            // strategy's symbol override knows), its parameters say which argument is which, as they
            // did before #577; where nothing binds, a number is a capacity.
            var constructor = context.SemanticHelper.GetSymbol(creation) as IMethodSymbol;
            var arguments = creation.ArgumentList?.Arguments ?? default;
            for (var i = 0; i < arguments.Count; i++)
            {
                var parameter = constructor?.Parameters.ElementAtOrDefault(i)?.Type;
                var dropped = parameter is null
                    ? arguments[i].Expression.IsKind(SyntaxKind.NumericLiteralExpression)
                    : parameter.SpecialType == SpecialType.System_Int32 || parameter.IsCollectionComparer();
                if (dropped) continue;
                source = context.Converter.ConvertIr(arguments[i].Expression);
            }
        }

        return Initialized(Factory(factory, type, source, ordering), creation.Initializer, context);
    }

    /// <summary><c>factory(seed)</c>, and <c>factory(seed, equality)</c> when the keys are not found by
    /// identity (<see cref="ElementEquality"/>), or <c>factory(seed, ordering)</c> for a sorted one, whose
    /// keys keep their type's order (<see cref="ValueOrdering"/>) rather than the one <c>&lt;</c> gives,
    /// or the order its comparer asked for (<paramref name="asked"/>).</summary>
    private static JsExpr Factory(string factory, ITypeSymbol? type, JsExpr? seed, string? asked = null)
    {
        var arguments = new List<JsExpr>();
        var key = type is INamedTypeSymbol { TypeArguments: [var keyType, _] } ? keyType : null;
        var second = key is null ? null
            : factory == Eq.Dictionary ? ElementEquality.Of(key)
            : asked ?? ValueOrdering.Of(key);
        if (seed is not null) arguments.Add(seed);
        else if (second is not null) arguments.Add(JsExpr.Literal("null"));
        if (second is not null) arguments.Add(JsExpr.Literal(second));
        return JsExpr.Call(JsExpr.Identifier(factory), arguments);
    }

    /// <summary>The call a dictionary answers, or null when this invocation is not one.</summary>
    private static string? CallOf(InvocationExpressionSyntax invocation, MemberAccessExpressionSyntax member,
        ConversionContext context)
    {
        var name = member.Name.Identifier.Text;
        // Unbound where the model cannot be asked: the two names only a dictionary answers.
        if (context.SemanticHelper.GetSymbol(invocation) is null && context.CanGuess(invocation))
        {
            return (name, invocation.ArgumentList.Arguments.Count) is ("ContainsKey", 1) or ("TryGetValue", 2)
                ? name
                : null;
        }
        switch (name)
        {
            case "ContainsKey" or "Add" or "Remove" or "Clear":
                return IsDictionary(member.Expression, context) ? name : null;
            // The dictionary a lookup reads is its receiver, or the first argument of an extension's
            // static form (DictionaryLookup.DictionaryOf).
            case "TryGetValue" or "GetValueOrDefault":
                return DictionaryLookup.DictionaryOf(invocation, context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol)
                    is { } dictionary && IsDictionary(dictionary, context) ? name : null;
            // Membership through the keys is the dictionary's own, found by its comparison: the keys'
            // array compares by SameValueZero whatever the key type.
            case "Contains" when invocation.ArgumentList.Arguments.Count == 1
                && member.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "Keys" } keys
                && IsDictionary(keys.Expression, context):
                return "Keys.Contains";
            default:
                return null;
        }
    }

    private static JsExpr Call(InvocationExpressionSyntax invocation, MemberAccessExpressionSyntax member, string call,
        ConversionContext context)
    {
        var arguments = invocation.ArgumentList.Arguments;
        switch (call)
        {
            // The lookups answer as .NET's do, and evaluate each argument once.
            case "TryGetValue" when arguments.Count > 1:
                return DictionaryLookup.TryGetValue(invocation, context);
            case "GetValueOrDefault" when arguments.Count > 0:
                return DictionaryLookup.GetValueOrDefault(invocation, context);
            case "Remove" when arguments.Count == 2:
                return DictionaryLookup.Remove(invocation, context);
            case "Keys.Contains":
                return JsExpr.Call(
                    JsExpr.Member(context.Converter.ConvertIr(((MemberAccessExpressionSyntax)member.Expression).Expression), "has"),
                    context.Converter.ConvertIr(arguments[0].Expression));
        }

        var receiver = context.Converter.ConvertIr(member.Expression);
        var values = arguments.Select(argument => context.Converter.ConvertIr(argument.Expression)).ToArray();
        return (call, values.Length) switch
        {
            ("ContainsKey", 1) => JsExpr.Call(JsExpr.Member(receiver, "has"), values),
            // `set` replaces the value of a key already there, where .NET's Add throws (#440).
            // Add refuses a key already there, as .NET's does, where the indexer's write replaces (#440).
            ("Add", 2) => JsExpr.Call(JsExpr.Member(receiver, "add"), values),
            ("Remove", 1) => JsExpr.Call(JsExpr.Member(receiver, "delete"), values),
            ("Clear", 0) => JsExpr.Call(JsExpr.Member(receiver, "clear")),
            _ => context.Unhandled(invocation, $"Dictionary.{call}"),
        };
    }

    /// <summary>What a member access on a dictionary reads — its keys, its values or its count, the
    /// count through its keys or values included — or null when it reads none of them.</summary>
    private static string? ReadOf(MemberAccessExpressionSyntax member, ConversionContext context)
    {
        var name = member.Name.Identifier.Text;
        if (name is "Keys" or "Values" or "Count" && IsDictionary(member.Expression, context)) return name;
        return name == "Count"
            && member.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "Keys" or "Values" } view
            && IsDictionary(view.Expression, context)
                ? "View.Count"
                : null;
    }

    private static JsExpr Read(MemberAccessExpressionSyntax member, string read, ConversionContext context)
    {
        if (read == "View.Count")
            return JsExpr.Member(context.Converter.ConvertIr(((MemberAccessExpressionSyntax)member.Expression).Expression), "size");
        var receiver = context.Converter.ConvertIr(member.Expression);
        return read switch
        {
            "Keys" => JsExpr.Call(JsExpr.Member(receiver, "keys")),
            "Values" => JsExpr.Call(JsExpr.Member(receiver, "values")),
            _ => JsExpr.Member(receiver, "size"),
        };
    }

    private static bool IsDictionary(ExpressionSyntax expression, ConversionContext context) =>
        context.SemanticHelper.GetType(expression).IsDictionary();
}
