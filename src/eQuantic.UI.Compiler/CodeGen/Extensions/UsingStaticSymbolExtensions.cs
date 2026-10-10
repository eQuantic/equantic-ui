using eQuantic.UI.Compiler.CodeGen.Ir;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Extensions;

/// <summary>
/// A member of .NET's own types reached bare, through <c>using static</c>, that no strategy claimed.
/// The class-static rule names a static member on its class, which is right for the types the
/// transpiler EMITS and names a class nothing defines for a platform one: <c>Double.naN</c> and
/// <c>String.join(",", ...parts)</c> compiled with no diagnostic (#485). One question, asked from
/// both branches that can return such a name, the member READ and the CALL, so the rule and its
/// message cannot drift apart.
/// </summary>
internal static class UsingStaticSymbolExtensions
{
    /// <summary>
    /// The member as its qualified spelling translates it, or null when that spelling has no
    /// translation either. A member reached bare through <c>using static</c> IS the member its
    /// qualified spelling names, so it goes where that one goes: <c>Now</c> is <c>DateTime.Now</c> and
    /// <c>NewGuid()</c> is <c>Guid.NewGuid()</c>, where every strategy that matches only a member
    /// access had passed the bare spelling by, to EQ2004 (#556). The qualified copy stands for the
    /// bare node, so the model answers for it, and its type part for the declaring type. A translation
    /// never names the .NET class, which nothing emits: a spelling that still does, and reported
    /// nothing, is left to <see cref="ReportIfPlatformReachedBare"/>.
    /// </summary>
    internal static JsExpr? AsQualified(this ISymbol symbol, ExpressionSyntax bare, ConversionContext context)
    {
        var declaring = symbol.ContainingType;
        if (declaring is null || !BoundaryShape.IsPlatform(declaring)) return null;
        var type = SyntaxFactory.IdentifierName(declaring.Name);
        ExpressionSyntax? qualified = bare switch
        {
            InvocationExpressionSyntax { Expression: SimpleNameSyntax called } call => SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, type, called.WithoutTrivia()),
                call.ArgumentList),
            SimpleNameSyntax name => SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, type, name.WithoutTrivia()),
            _ => null,
        };
        if (qualified is null) return null;

        var access = qualified as MemberAccessExpressionSyntax ?? (MemberAccessExpressionSyntax)((InvocationExpressionSyntax)qualified).Expression;
        var helper = context.SemanticHelper;
        helper.MapSymbol(access.Expression, declaring);
        if (qualified is InvocationExpressionSyntax copy && bare is InvocationExpressionSyntax original)
        {
            helper.MapSynthetic(copy, original);
            helper.MapSynthetic(access, original.Expression);
            helper.MapSynthetic(access.Name, original.Expression);
            // The arguments are copied whole, so the copy and the original line up node for node.
            foreach (var (argument, source) in copy.ArgumentList.DescendantNodesAndSelf().Zip(original.ArgumentList.DescendantNodesAndSelf()))
                helper.MapSynthetic(argument, source);
        }
        else
        {
            helper.MapSynthetic(access, bare);
            helper.MapSynthetic(access.Name, bare);
        }

        var reported = context.Diagnostics.Count;
        var converted = context.Converter.ConvertIr(qualified);
        // What the qualified spelling raised was raised at its copy, which has no place in the file. An
        // error means it has no translation either, which the bare spelling reports for itself where
        // it is written; anything milder is reported again there.
        var raised = context.Diagnostics.Skip(reported).ToList();
        context.Diagnostics.RemoveRange(reported, raised.Count);
        if (raised.Any(diagnostic => diagnostic.Severity == ConversionSeverity.Error)) return null;
        foreach (var diagnostic in raised) context.Report(bare, diagnostic.Severity, diagnostic.Code, diagnostic.Message);
        // The class-static rule's own shape, `Type.member`, and not any text that starts with the type's
        // name: `Object`, `Array` and `String` are JavaScript globals a translation may call.
        var fallback = $"{declaring.Name}.{symbol.Name.ToCamelCase()}";
        var written = JsExprWriter.Write(converted);
        var untranslated = written.StartsWith(fallback, StringComparison.Ordinal)
            && (written.Length == fallback.Length || written[fallback.Length] is not ('_' or '$') && !char.IsLetterOrDigit(written[fallback.Length]));
        return untranslated ? null : converted;
    }

    /// <summary>Reports EQ2004 and returns true when <paramref name="symbol"/> is a platform type's
    /// member, which has no translation once a strategy has passed it by.</summary>
    internal static bool ReportIfPlatformReachedBare(this ISymbol symbol, SyntaxNode node, ConversionContext context)
    {
        var declaring = symbol.ContainingType;
        if (declaring is null || !BoundaryShape.IsPlatform(declaring)) return false;
        context.Report(node, ConversionSeverity.Error, "EQ2004",
            $"'{declaring.ToDisplayString()}.{symbol.Name}', reached through `using static`, has no "
            + "JavaScript translation: nothing emits that class. Write it qualified where its "
            + "qualified form translates, or keep it on the server.");
        return true;
    }
}
