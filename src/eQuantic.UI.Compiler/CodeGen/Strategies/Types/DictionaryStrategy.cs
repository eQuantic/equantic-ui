using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
/// key type (<see cref="KeyEquality"/>), the factory's second argument: by identity, through the
/// class's JavaScript Map, by value, through <c>$eq.equals</c>, or, where the key type does not decide,
/// by the key's own equality, which the runtime asks the value for. A comparer has no form here and
/// is refused.
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
    /// How <c>EqualityComparer&lt;T&gt;.Default</c> tells two values of <paramref name="type"/> apart,
    /// as the runtime's <c>KeyEquality</c> argument writes it. Null by IDENTITY: a number, a string, a
    /// char, a bool, a long, an enum, a <c>Guid</c> and an array, which SameValueZero compares as .NET
    /// does. <c>true</c> by VALUE, through <c>$eq.equals</c>: every type LINQ's keyed operators compare
    /// so (a record, a struct, a tuple, an anonymous type, a decimal, a date). And <c>'own'</c> where
    /// the type does not decide, because the value may be of a type that overrides <c>Equals</c>:
    /// <c>object</c>, an interface, a type parameter, and a class, whose subclass may override it. The
    /// runtime then asks the value for its own equality, and keeps a key with none in its Map. Those
    /// were found by identity, so two equal records under <c>object</c> were two keys (found in
    /// review, #443).
    /// </summary>
    internal static string? KeyEquality(ITypeSymbol? type)
    {
        var unwrapped = type.UnwrapNullable() ?? type;
        if (LinqKeys.ComparesByValue(unwrapped)) return "true";
        return unwrapped switch
        {
            { SpecialType: SpecialType.System_Object or SpecialType.System_ValueType or SpecialType.System_Enum } => "'own'",
            { TypeKind: TypeKind.Interface or TypeKind.TypeParameter or TypeKind.Dynamic } => "'own'",
            // A class of the app's or a library's, never a special one: `string` overrides Equals too,
            // and is a primitive on this side, which SameValueZero compares by value already.
            { TypeKind: TypeKind.Class, SpecialType: SpecialType.None } => "'own'",
            _ => null,
        };
    }

    /// <summary>
    /// The dictionary an initializer nested under a dictionary-typed member seeds (<c>Map = { ["a"] = 1 }</c>),
    /// built as a constructed one is.
    /// </summary>
    internal static JsExpr Seeded(ITypeSymbol type, InitializerExpressionSyntax initializer, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        return Factory(type.DictionaryFactory()!, type,
            Entries(initializer, context) is { Count: > 0 } pairs ? Pairs(null, pairs) : null);
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
    /// constructor copies and then the initializer's pairs, in the order C# evaluates them. A capacity
    /// has no meaning here, and a comparer has no form.
    /// </summary>
    private static JsExpr Construction(BaseObjectCreationExpressionSyntax creation, ConversionContext context)
    {
        var type = context.SemanticHelper.GetType(creation);
        var constructor = context.SemanticHelper.GetSymbol(creation) as IMethodSymbol;
        if (constructor?.Parameters.Any(parameter => parameter.Type.Name is "IEqualityComparer" or "IComparer") == true)
            return context.Unhandled(creation, $"{type?.Name} with a comparer");

        context.UsedHelpers.Add(Eq.Import);
        JsExpr? source = null;
        var arguments = creation.ArgumentList?.Arguments ?? default;
        for (var i = 0; i < arguments.Count; i++)
        {
            // A capacity: an int parameter, or a number where no model says which parameter it fills.
            if (constructor?.Parameters.ElementAtOrDefault(i)?.Type.SpecialType == SpecialType.System_Int32
                || (constructor is null && arguments[i].Expression.IsKind(SyntaxKind.NumericLiteralExpression)))
                continue;
            source = context.Converter.ConvertIr(arguments[i].Expression);
        }

        var pairs = Entries(creation.Initializer, context);
        return Factory(FactoryOf(creation, context)!, type, pairs is { Count: > 0 } ? Pairs(source, pairs) : source);
    }

    /// <summary><c>factory(seed)</c>, and <c>factory(seed, equality)</c> when the keys are not found by
    /// identity (<see cref="KeyEquality"/>).</summary>
    private static JsExpr Factory(string factory, ITypeSymbol? type, JsExpr? seed)
    {
        var arguments = new List<JsExpr>();
        var equality = factory == Eq.Dictionary && type is INamedTypeSymbol { TypeArguments: [var key, _] }
            ? KeyEquality(key)
            : null;
        if (seed is not null) arguments.Add(seed);
        else if (equality is not null) arguments.Add(JsExpr.Literal("null"));
        if (equality is not null) arguments.Add(JsExpr.Literal(equality));
        return JsExpr.Call(JsExpr.Identifier(factory), arguments);
    }

    /// <summary>The pairs a constructor seeds with: what it copies, spread, then the initializer's own.</summary>
    private static JsExpr Pairs(JsExpr? source, IReadOnlyList<string> pairs)
    {
        var copied = source is null ? "" : $"...{JsExprWriter.Write(source)}, ";
        return JsExpr.Literal($"[{copied}{string.Join(", ", pairs)}]");
    }

    /// <summary>
    /// The <c>[key, value]</c> pairs an initializer adds, in order: <c>{ key, value }</c> elements of a
    /// collection initializer, or <c>[key] = value</c> ones of an object initializer. Null for none.
    /// </summary>
    private static List<string>? Entries(InitializerExpressionSyntax? initializer, ConversionContext context)
    {
        if (initializer is null) return null;
        var pairs = new List<string>();
        foreach (var element in initializer.Expressions)
        {
            switch (element)
            {
                case InitializerExpressionSyntax { Expressions.Count: 2 } pair:
                    pairs.Add($"[{context.Converter.ConvertExpression(pair.Expressions[0])}, "
                        + $"{context.Converter.ConvertExpression(pair.Expressions[1])}]");
                    break;
                case AssignmentExpressionSyntax { Left: ImplicitElementAccessSyntax { ArgumentList.Arguments.Count: 1 } key } assignment:
                    pairs.Add($"[{context.Converter.ConvertExpression(key.ArgumentList.Arguments[0].Expression)}, "
                        + $"{context.Converter.ConvertExpression(assignment.Right)}]");
                    break;
                default:
                    context.Unhandled(element, "Dictionary initializer");
                    break;
            }
        }
        return pairs;
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
            ("Add", 2) => JsExpr.Call(JsExpr.Member(receiver, "set"), values),
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
