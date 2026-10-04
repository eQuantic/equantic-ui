using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace eQuantic.UI.Compiler.CodeGen.Extensions;

/// <summary>
/// The fence for a COMPARER handed to a collection's constructor.
/// <para>
/// What these collections lower to, an array, a plain object, a <c>Set</c> or a runtime map,
/// compares with <c>===</c> and ordinal strings, and takes no comparer. The argument used to be
/// dropped in silence: <c>new Dictionary&lt;string, T&gt;(StringComparer.OrdinalIgnoreCase)</c>
/// found <c>"CSharp"</c> in C# and nothing on the web, and with no initializer the dictionary
/// became the comparer itself. A comparer that asks only for what the lowering already does
/// passes: the type's own default, and ordinal strings. Any other is an error, never a drop, which
/// is the rule <c>with(…)</c> in a collection expression and <c>GroupBy</c>'s key comparer already
/// follow.
/// </para>
/// <para>
/// It runs where every expression passes (<see cref="CSharpToJsConverter.ConvertIr"/>) rather than
/// in the strategies that build collections: three of them do, and a fence copied into each is the
/// shape the host-only fence had when it kept being found missing a door.
/// </para>
/// </summary>
public static class CollectionComparerExtensions
{
    /// <summary>Reports EQ2007 at every comparer argument of <paramref name="creation"/> that has
    /// no JavaScript translation.</summary>
    public static void ReportUntranslatableComparer(this BaseObjectCreationExpressionSyntax creation,
        ConversionContext context)
    {
        if (creation.ArgumentList is not { Arguments.Count: > 0 }) return;
        if (context.SemanticHelper.GetOperation(creation) is not IObjectCreationOperation
            {
                Constructor.ContainingType: { } created,
            } operation) return;
        if (!(created.ContainingNamespace?.ToDisplayString() ?? "")
                .StartsWith("System.Collections", StringComparison.Ordinal)) return;

        foreach (var argument in operation.Arguments)
        {
            if (argument.ArgumentKind == ArgumentKind.DefaultValue) continue;
            if (argument.Parameter?.Type is not INamedTypeSymbol parameter) continue;
            if (!IsNamed(parameter, "IEqualityComparer`1") && !IsNamed(parameter, "IComparer`1")) continue;
            if (AsksForTheDefault(argument.Value)) continue;

            context.Report(argument.Syntax, ConversionSeverity.Error, "EQ2007",
                $"'{created.Name}' built with a comparer has no JavaScript translation: what it "
                + "lowers to compares with === and ordinal strings, and takes no comparer. "
                + "Normalize the keys yourself (lower-case them on the way in and on every lookup), "
                + "or drop the comparer.");
        }
    }

    /// <summary>
    /// Reports EQ2007 at an equality comparer handed to a call that builds a collection from it
    /// (<c>ToHashSet(comparer)</c>), where it has no JavaScript translation, and answers whether it
    /// reported. The comparers that ask for the default pass, as they do for a constructor.
    /// </summary>
    internal static bool RefusesAsUntranslatable(this IOperation comparer, string what, ConversionContext context)
    {
        if (AsksForTheDefault(comparer)) return false;
        context.Report(comparer.Syntax, ConversionSeverity.Error, "EQ2007",
            $"'{what}' with a comparer has no JavaScript translation: the set it builds finds its elements as the "
            + "element type's default comparer does, and takes no other. Normalize the elements yourself, or drop the comparer.");
        return true;
    }

    /// <summary>
    /// The order an <c>IComparer&lt;T&gt;</c> handed to a sort or a search asks for, as the runtime's
    /// <c>SortOrder</c> (<c>utils/list.ts</c>): the element type's default for <c>null</c> and
    /// <c>Comparer&lt;T&gt;.Default</c> (<paramref name="fallback"/>); a <c>StringComparer</c>'s comparison
    /// for one of its six; a <c>Comparer&lt;T&gt;.Create(comparison)</c>'s comparison; and any other value
    /// as the comparer its twin is, whose <c>compare</c> the runtime calls (a null one at run time is the
    /// default, as .NET takes it), written into <paramref name="hole"/>, where the call's template puts
    /// the argument. <c>Value</c> is what fills the hole, the comparer or <c>Create</c>'s comparison, and
    /// null where nothing does: the others are no value this side, and converting one would name a member
    /// nothing emits. Null after reporting EQ2007 for a comparer of the platform no twin carries (a
    /// <c>StringComparer</c> made for a culture, a comparer a library compiled).
    /// </summary>
    internal static (string Order, IOperation? Value)? SortOrderAskedFor(this IOperation comparer, string fallback,
        string hole, ConversionContext context)
    {
        var value = comparer;
        while (value is IConversionOperation conversion) value = conversion.Operand;
        switch (value)
        {
            case ILiteralOperation { ConstantValue: { HasValue: true, Value: null } }:
            case IPropertyReferenceOperation { Property: { Name: "Default", ContainingType: var home } }
                when IsNamed(home, "Comparer`1"):
                return (fallback, null);
            case IPropertyReferenceOperation { Property: { IsStatic: true, Name: var name, ContainingType: var home } }
                when home.ToDisplayString() == "System.StringComparer" && StringComparisonOf(name) is { } comparison:
                context.UsedHelpers.Add(Eq.Import);
                return ($"{Eq.StringOrder}('{comparison}')", null);
            case IInvocationOperation { TargetMethod: { Name: "Create", IsStatic: true, ContainingType: var home } } create
                when IsNamed(home, "Comparer`1") && create.Arguments.Length == 1:
                context.UsedHelpers.Add(Eq.Import);
                return ($"{Eq.ComparerOrder}({{ compare: {hole} }}, {fallback}, "
                    + $"'System.Collections.Generic.ComparisonComparer`1[{ReflectionName.Of((create.Type as INamedTypeSymbol)?.TypeArguments.FirstOrDefault())}]')",
                    create.Arguments[0].Value);
        }
        if (value.Type is { } type && (type.Locations.Any(location => location.IsInSource) || type.TypeKind == TypeKind.Interface))
        {
            context.UsedHelpers.Add(Eq.Import);
            return ($"{Eq.ComparerOrder}({hole}, {fallback}, '{ReflectionName.Of(type)}')", comparer);
        }
        context.Report(comparer.Syntax, ConversionSeverity.Error, "EQ2007",
            $"A sort or a search handed '{comparer.Syntax}' has no JavaScript translation: the comparers that cross "
            + "are the element type's default, a StringComparer's six, Comparer<T>.Create, and a comparer the app writes. "
            + "Pass a Comparison<T>, or one of those.");
        return null;
    }

    /// <summary>The <c>StringComparison</c> member a <c>StringComparer</c> property compares by, as it crosses.</summary>
    private static string? StringComparisonOf(string property) => property switch
    {
        "Ordinal" => "ordinal",
        "OrdinalIgnoreCase" => "ordinalIgnoreCase",
        "CurrentCulture" => "currentCulture",
        "CurrentCultureIgnoreCase" => "currentCultureIgnoreCase",
        "InvariantCulture" => "invariantCulture",
        "InvariantCultureIgnoreCase" => "invariantCultureIgnoreCase",
        _ => null,
    };

    /// <summary>
    /// Whether the comparer asks for nothing the lowering does not already do: <c>null</c> (the
    /// constructor's own default), <c>EqualityComparer&lt;T&gt;.Default</c> or
    /// <c>Comparer&lt;T&gt;.Default</c>, and <c>StringComparer.Ordinal</c>, which is how every twin
    /// compares a string, and the order a sorted one is built in when it is named
    /// (<see cref="OrderingAskedFor"/>).
    /// </summary>
    private static bool AsksForTheDefault(IOperation value) => Asked(value) is not Ask.Other;

    /// <summary>
    /// The order a comparer handed to a sorted collection asks for, as an ordering of
    /// <see cref="Strategies.ValueOrdering"/>: its element type's own for <c>null</c> and
    /// <c>Comparer&lt;T&gt;.Default</c>, and the code-unit order for <c>StringComparer.Ordinal</c>,
    /// where the element type's would be the current culture's. Null for any other comparer, which
    /// <see cref="ReportUntranslatableComparer"/> reports, and for an element type with no order here.
    /// </summary>
    internal static string? OrderingAskedFor(this IOperation comparer, ITypeSymbol element) => Asked(comparer) switch
    {
        Ask.Ordinal => "'value'",
        Ask.Default => Strategies.ValueOrdering.Of(element),
        _ => null,
    };

    private enum Ask { Default, Ordinal, Other }

    private static Ask Asked(IOperation value)
    {
        while (value is IConversionOperation conversion) value = conversion.Operand;
        return value switch
        {
            ILiteralOperation { ConstantValue: { HasValue: true, Value: null } } => Ask.Default,
            IPropertyReferenceOperation { Property: { Name: "Default", ContainingType: var home } }
                when IsNamed(home, "EqualityComparer`1") || IsNamed(home, "Comparer`1") => Ask.Default,
            IPropertyReferenceOperation { Property: { Name: "Ordinal", ContainingType: var home } }
                when home.ToDisplayString() == "System.StringComparer" => Ask.Ordinal,
            _ => Ask.Other,
        };
    }

    private static bool IsNamed(INamedTypeSymbol type, string metadataName) =>
        type.OriginalDefinition.MetadataName == metadataName
        && type.OriginalDefinition.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic";
}
