using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// TEXT ELEMENTS: <c>StringInfo</c>'s static questions about extended grapheme clusters (UAX #29),
/// where each one begins and how long the one at an index is. A code editor steps over whole
/// characters with them (an emoji is one step and one Backspace, not the two halves of a surrogate
/// pair), and each side asks its PLATFORM, which implements the same annex: .NET its own tables,
/// the browser <c>Intl.Segmenter</c> (<c>$eq.text</c>, which approximates where a browser has none).
/// </summary>
public class TextElementStrategy : IExpressionIrStrategy
{
    public int Priority => 12;

    public bool CanConvert(SyntaxNode node, ConversionContext context) =>
        node is InvocationExpressionSyntax invocation && Template(invocation, context) is not null;

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var args = invocation.ArgumentList.Arguments
            .Select(a => context.Converter.ConvertIr(a.Expression))
            .ToArray();
        context.UsedHelpers.Add(Eq.Import);
        // Each hole is a PARAMETER: a named argument written out of order fills its own.
        var method = (IMethodSymbol)context.SemanticHelper.GetSymbol(invocation)!;
        var emit = PrimitiveStaticStrategy.BindNamedArguments(Template(invocation, context)!, invocation, method);
        return JsExpr.Template(emit, args, context.TypeAnnotations);
    }

    /// <summary>The emission for a supported overload, or null. Only the string overloads: the
    /// span ones have no twin on this side.</summary>
    private static string? Template(InvocationExpressionSyntax invocation, ConversionContext context) =>
        context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol { IsStatic: true } method
        && method.ContainingType.ToDisplayString() == "System.Globalization.StringInfo"
        && method.Parameters.All(p => p.Type.SpecialType is SpecialType.System_String or SpecialType.System_Int32)
            ? (method.Name, method.Parameters.Length) switch
            {
                ("ParseCombiningCharacters", 1) => $"{Eq.TextElementStarts}({{0}})",
                ("GetNextTextElementLength", 1) => $"{Eq.NextTextElementLength}({{0}}, 0)",
                ("GetNextTextElementLength", 2) => $"{Eq.NextTextElementLength}({{0}}, {{1}})",
                _ => null,
            }
            : null;
}
