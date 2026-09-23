using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// A character's GENERAL CATEGORY: <c>CharUnicodeInfo.GetUnicodeCategory</c> and
/// <c>char.GetUnicodeCategory</c>, for a code point, a character, or a string and an index. Each
/// side asks its platform: .NET its own table, the browser its Unicode property escapes
/// (<c>$eq.text.unicodeCategory</c>), and the answer crosses as any enum member does, by its
/// camel-cased name, which the transpiled casts and comparisons read. The code editor asks it to know a mark that begins a
/// text element, which has no advance of its own. It was fenced as "derivable, parked until someone
/// needs it".
/// </summary>
public class UnicodeCategoryStrategy : IExpressionIrStrategy
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
        return JsExpr.Template(Template(invocation, context)!, args, context.TypeAnnotations);
    }

    /// <summary>The emission for a supported overload, or null.</summary>
    private static string? Template(InvocationExpressionSyntax invocation, ConversionContext context) =>
        context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol { IsStatic: true, Name: "GetUnicodeCategory" } method
        && (method.ContainingType.SpecialType == SpecialType.System_Char
            || method.ContainingType.ToDisplayString() == "System.Globalization.CharUnicodeInfo")
            ? method.Parameters.Length switch
            {
                1 => $"{Eq.UnicodeCategory}({{0}})",
                2 => $"{Eq.UnicodeCategory}({{0}}, {{1}})",
                _ => null,
            }
            : null;
}
