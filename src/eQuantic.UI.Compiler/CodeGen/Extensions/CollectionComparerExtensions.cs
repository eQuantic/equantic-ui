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
            if (!(argument.Parameter?.Type).IsCollectionComparer()) continue;
            if (AsksForTheDefault(argument.Value)) continue;

            context.Report(argument.Syntax, ConversionSeverity.Error, "EQ2007",
                $"'{created.Name}' built with a comparer has no JavaScript translation: what it "
                + "lowers to compares with === and ordinal strings, and takes no comparer. "
                + "Normalize the keys yourself (lower-case them on the way in and on every lookup), "
                + "or drop the comparer.");
        }
    }

    /// <summary>
    /// Whether <paramref name="type"/> is a collection constructor's COMPARER parameter:
    /// <c>IEqualityComparer&lt;T&gt;</c> or <c>IComparer&lt;T&gt;</c> of System.Collections.Generic.
    /// The one answer to "which argument is the comparer", for this fence and for every strategy that
    /// builds a collection and has to skip it: a strategy that asks the question its own way is the
    /// copy that refused <c>StringComparer.Ordinal</c> while this fence passed it (#577).
    /// </summary>
    internal static bool IsCollectionComparer(this ITypeSymbol? type) =>
        type is INamedTypeSymbol named && (IsNamed(named, "IEqualityComparer`1") || IsNamed(named, "IComparer`1"));

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
