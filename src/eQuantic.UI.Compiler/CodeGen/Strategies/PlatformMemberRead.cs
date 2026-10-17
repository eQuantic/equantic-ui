using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// A read of a property or a field of a .NET type that every strategy has passed by, which the
/// member-access fallback writes as the member's camelCase name on its receiver. That name answers
/// only where the value the browser holds carries it, and the few members it does are listed here. Any
/// other is EQ2004, as a call of a .NET method that reaches the invocation fallback is: a read and a
/// call name a member alike, and a fence that guards calls and not reads is none.
/// <c>CultureInfo.CurrentCulture.Name</c> went out as <c>CultureInfo.currentCulture.name</c>, and
/// <c>Environment.NewLine</c>, <c>TimeZoneInfo.Local</c> or <c>typeof(T).Name</c> as names nothing
/// defines, each built with no diagnostic and threw or read undefined in the browser (#847).
/// <para>
/// A member a .NET interface declares is not asked: the value read through one may be a type of the
/// app's own, whose twin carries the member by that name.
/// </para>
/// <para>
/// A STATIC of a package's type is the same refusal (#822): the build has no source for the type and the
/// runtime no twin of it, so <c>Status.Label</c> named a class nothing defines, where a call of the same
/// type was refused already.
/// </para>
/// </summary>
internal static class PlatformMemberRead
{
    /// <summary>
    /// The members the browser reads by their own name, each by the type that declares it first (an
    /// override is its root's), with what carries it.
    /// </summary>
    private static readonly HashSet<(string Type, string Member)> CarriedByName =
    [
        // JavaScript's own length: a string, and an array, which a span is too.
        ("System.String", "Length"),
        ("System.Array", "Length"),
        ("System.Span`1", "Length"),
        ("System.ReadOnlySpan`1", "Length"),
        // What an exception's twin was handed, which utils/exceptions.ts carries by these names (#558).
        ("System.Exception", "Message"),
        ("System.Exception", "InnerException"),
        ("System.ArgumentException", "ParamName"),
        ("System.ArgumentOutOfRangeException", "ActualValue"),
        ("System.ObjectDisposedException", "ObjectName"),
        ("System.TypeInitializationException", "TypeName"),
        ("System.AggregateException", "InnerExceptions"),
        // The runtime's Decimal, whose scale is the one .NET keeps (DecimalConformanceTests).
        ("System.Decimal", "Scale"),
        // The pair a dictionary yields, which $eq.collections.pair makes.
        ("System.Collections.Generic.KeyValuePair`2", "Key"),
        ("System.Collections.Generic.KeyValuePair`2", "Value"),
    ];

    /// <summary>
    /// Reports EQ2004 and answers true when <paramref name="symbol"/>, read at <paramref name="node"/>,
    /// is a property or a field of a .NET type that no value in the browser carries by its name. A chain
    /// is refused once, at its first such member (<c>CultureInfo.CurrentCulture</c> in
    /// <c>CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator</c>), through <c>?.</c> as
    /// through <c>.</c>: what is read from a refused read is the same refusal. A receiver is converted
    /// before what is read from it, so it has been refused, or not, by then; one a strategy translated
    /// (an indexer, <c>Nullable&lt;T&gt;.Value</c>) was not, and a read made on it is asked as any
    /// other. The report is placed at the in-tree read, which a <c>?.</c> tail stands for after it is
    /// rebuilt.
    /// </summary>
    internal static bool ReportIfUntranslated(ISymbol? symbol, SyntaxNode node, ConversionContext context)
    {
        var read = context.SemanticHelper.Original(node);
        if (ReceiverOf(read) is { } receiver && context.RefusedReads.Contains(receiver))
        {
            // Read from a refused read, and so refused with it, whatever it is: what is read from it
            // next is too.
            context.RefusedReads.Add(read);
            return false;
        }
        if (!NothingCarries(symbol)) return false;

        context.RefusedReads.Add(read);
        context.Report(read, ConversionSeverity.Error, "EQ2004", symbol!.ContainingType.IsPackages()
            ? $"'{symbol.ContainingType.ToDisplayString()}.{symbol.Name}' has no JavaScript translation: "
                + $"{symbol.ContainingType.PackageReason()}, so the read would name a class nothing defines. "
                + TwinlessTypeExtensions.PackageRemedy
            : $"'{symbol.ContainingType.ToDisplayString()}.{symbol.Name}' has no JavaScript translation: no value the "
            + "browser holds carries it, so the read would name a member nothing defines. Read it in a "
            + "[ServerAction] and hand the browser its answer, or — if the class this read sits in only "
            + "ever runs on the server — mark THAT class [ServerOnly] so no module is emitted for it.");
        return true;
    }

    /// <summary>Whether <paramref name="symbol"/> is a property or a field of a .NET class or struct
    /// that the browser does not carry by its name, or a static one of a package's type, whatever kind of
    /// type declares it, which has no twin and no module to carry it (#822). A member a .NET interface
    /// declares is not asked (above), and a package interface's static was let through with it, written as
    /// `IStatus.label` (Copilot's second review of #958). A value of a package's type reaches the browser
    /// only as the data the server sends, whose members it reads by name.</summary>
    private static bool NothingCarries(ISymbol? symbol) =>
        symbol is IPropertySymbol or IFieldSymbol
        && symbol.ContainingType is { } declaring
        && !symbol.Locations.Any(location => location.IsInSource)
        && (BoundaryShape.IsPlatform(declaring)
            ? declaring.TypeKind != TypeKind.Interface && !CarriedByName.Contains(Root(symbol))
            : symbol.IsStatic && declaring.IsPackages());

    /// <summary>
    /// What a read is made on, its parentheses and a <c>!</c> looked through: the left of a <c>.</c>,
    /// and for a <c>?.</c> binding the receiver of the conditional access whose tail holds it. Not the
    /// nearest conditional access: <c>a?.B?.C</c> is <c>a?.(.B?.C)</c>, and <c>.B</c> is the inner
    /// one's receiver, not part of its tail.
    /// </summary>
    private static SyntaxNode? ReceiverOf(SyntaxNode read) => Bare(read switch
    {
        MemberAccessExpressionSyntax access => access.Expression,
        MemberBindingExpressionSyntax binding => binding.Ancestors().OfType<ConditionalAccessExpressionSyntax>()
            .FirstOrDefault(conditional => conditional.WhenNotNull.Span.Contains(binding.Span))?.Expression,
        _ => null,
    });

    private static ExpressionSyntax? Bare(ExpressionSyntax? expression) => expression switch
    {
        ParenthesizedExpressionSyntax parenthesized => Bare(parenthesized.Expression),
        PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } suppressed => Bare(suppressed.Operand),
        _ => expression,
    };

    /// <summary>The type that declares the member first, by its metadata name, and the member's name.</summary>
    private static (string Type, string Member) Root(ISymbol symbol)
    {
        var root = symbol;
        while (root is IPropertySymbol { OverriddenProperty: { } overridden }) root = overridden;
        var type = root.ContainingType.OriginalDefinition;
        return ($"{type.ContainingNamespace.ToDisplayString()}.{type.MetadataName}", root.Name);
    }
}
