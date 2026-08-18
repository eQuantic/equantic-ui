using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Invocation;

/// <summary>
/// Strategy for ToString method conversion.
/// Handles: x.ToString() → String(x)
/// This is safer than x.toString() in JS because String(x) handles null/undefined gracefully.
/// </summary>
public class ToStringStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        return memberAccess.Name.Identifier.Text == "ToString";
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var caller = context.Converter.ConvertExpression(memberAccess.Expression);

        // WHICH argument is which: C# has ToString(), ToString(format), ToString(provider) and
        // ToString(format, provider), and `ToString(x)` reads the same in source either way. Taking
        // args[0] as the format is how `ToString(CultureInfo.InvariantCulture)` emitted
        // `$eq.text.format(value, CultureInfo.InvariantCulture)` — a name that exists in .NET and
        // in no browser, so the page died with "CultureInfo is not defined" while the server, which
        // runs the C#, was perfectly happy.
        var args = invocation.ArgumentList.Arguments;
        var provider = args.FirstOrDefault(argument => IsFormatProvider(argument.Expression, context));
        var formatArg = args.FirstOrDefault(argument => argument != provider);

        var invariant = false;
        if (provider is not null)
        {
            var culture = CultureNameOf(provider.Expression);
            if (culture == "InvariantCulture")
            {
                invariant = true;
            }
            else if (culture != "CurrentCulture")
            {
                // Never approximate a provider nobody tested: a custom IFormatProvider, or a culture
                // read from a variable, has no counterpart in the Intl subset this framework pins.
                context.Report(node, ConversionSeverity.Error, "EQ2108",
                    "Only CultureInfo.InvariantCulture and CultureInfo.CurrentCulture cross to "
                    + "JavaScript. Format with an explicit specifier — ToString(\"N2\") follows the "
                    + "app's culture on both targets — or convert with the invariant culture.");
                return $"String({caller})";
            }
        }

        if (formatArg is not null)
        {
            var fmt = context.Converter.ConvertExpression(formatArg.Expression);
            context.UsedHelpers.Add(Eq.Import);
            // The alignment slot stays empty: this shape has none, and the invariant flag is what
            // makes the helper stop reading the culture the reader happens to be in.
            return invariant
                ? $"{Eq.Format}({caller}, {fmt}, undefined, true)"
                : $"{Eq.Format}({caller}, {fmt})";
        }

        if (provider is not null)
        {
            // A provider with NO specifier. JavaScript's `String(x)` is already the invariant
            // rendering of a number, so the invariant ask is answered exactly; the CURRENT culture's
            // general format is not in the tested subset, and asking for it by name is how a page
            // gets digits nobody pinned.
            if (invariant) return $"String({caller})";

            context.Report(node, ConversionSeverity.Error, "EQ2109",
                "ToString(CultureInfo.CurrentCulture) has no specifier to pin, and the general "
                + "format is outside the tested Intl subset. Name the format — ToString(\"N2\"), "
                + "ToString(\"F1\") — which reads the same on the server and in the browser.");
            return $"String({caller})";
        }

        // A FRACTIONAL number with no culture at all is the quiet one. C# renders it in whatever
        // culture the thread is in — a pt request renders "0,55" from the server — and JavaScript's
        // `String(x)` is always invariant, so the browser re-renders "0.55" over it. Two targets,
        // two answers, from source that looks obviously correct. A warning rather than an error:
        // this compiles in apps today, and the fix is one argument away.
        if (context.SemanticHelper.GetType(memberAccess.Expression) is
            { SpecialType: SpecialType.System_Single or SpecialType.System_Double
                or SpecialType.System_Decimal })
        {
            context.Report(node, ConversionSeverity.Warning, "EQ2110",
                "A fractional number converted with no culture reads differently on each target: "
                + "C# follows the request's culture (a comma, in pt) and JavaScript is always "
                + "invariant. Say which you mean — ToString(CultureInfo.InvariantCulture) for a "
                + "value a machine reads, or ToString(\"N2\") for one a person reads.");
        }

        // An ENUM crosses as a lowercase string (`Kind.B` → 'b'), so String() hands back the WIRE
        // value while the server hands back the C# member name. Any text printing an enum then
        // reads one way in the SSR markup and another after hydration — a word that changes by
        // itself, which nobody attributes to the compiler.
        if (context.SemanticHelper.GetType(memberAccess.Expression) is INamedTypeSymbol
            { TypeKind: TypeKind.Enum } enumType)
        {
            return EnumNameLookup(enumType, memberAccess.Expression, caller);
        }

        return $"String({caller})";
    }

    /// <summary>
    /// The C# member NAME for an enum value. A literal member folds to its name; anything else gets
    /// the wire→name map inline, because no enum object is emitted to hold one — the members are
    /// converted to string literals at their use sites and nothing survives to look up.
    /// </summary>
    internal static string EnumNameLookup(INamedTypeSymbol enumType, ExpressionSyntax expression,
        string caller)
    {
        var members = enumType.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.ConstantValue is not null)
            .Select(f => f.Name)
            .ToList();
        if (members.Count == 0) return $"String({caller})";

        // `Kind.B.ToString()` — the value is known here, so say it.
        if (expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: var literal }
            && members.Contains(literal))
        {
            return $"'{literal}'";
        }

        var map = string.Join(", ", members.Select(m => $"{Wire(m)}: '{m}'"));
        return $"({{{map}}})[{caller}]";
    }

    /// <summary>
    /// Is this argument the PROVIDER rather than the format? Asked of the model, because the two
    /// overloads are one syntax. Without a model — the playground compiles a buffer alone — the
    /// source's own spelling is the only evidence there is, and `CultureInfo.X` is unambiguous.
    /// </summary>
    private static bool IsFormatProvider(ExpressionSyntax expression, ConversionContext context)
    {
        if (context.SemanticHelper.GetType(expression) is { } type)
        {
            return type.Name == "IFormatProvider"
                || type.AllInterfaces.Any(i => i.ToDisplayString() == "System.IFormatProvider");
        }

        return expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "CultureInfo" } };
    }

    /// <summary>The culture a provider expression NAMES, or null when it names none — a variable, a
    /// field, a custom provider. Syntax, deliberately: what matters is that the author WROTE the
    /// invariant culture, and a value that only exists at runtime cannot be honoured at build time.</summary>
    private static string? CultureNameOf(ExpressionSyntax expression) =>
        expression is MemberAccessExpressionSyntax member ? member.Name.Identifier.Text : null;

    /// <summary>The wire spelling of a member — the same camelCase the member access converts to,
    /// so the map's keys match the values that will be looked up in it.</summary>
    private static string Wire(string member) =>
        $"'{char.ToLowerInvariant(member[0])}{member[1..]}'";

    public int Priority => 10;
}
