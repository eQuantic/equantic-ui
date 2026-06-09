using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Special;

/// <summary>
/// Catches C# constructs that have <b>no possible JavaScript equivalent</b> (unsafe/pointer code,
/// stack allocation, typed-reference intrinsics) and turns them into a build <b>error</b> instead of
/// silently emitting unconvertible C# verbatim. These depend on a manual-memory, pointer-based runtime
/// the browser does not have; there is no compat helper that can stand in for them.
/// </summary>
/// <remarks>
/// Runs at a low priority so any genuine future strategy for one of these kinds would win. A placeholder
/// (<c>undefined</c>) is emitted so transpilation continues and <i>all</i> errors are collected in one
/// pass rather than stopping at the first.
/// </remarks>
public class UnsupportedConstructStrategy : IConversionStrategy
{
    // Note: sizeof(int) is constant-folded to 4 and stackalloc maps to a typed array elsewhere, so
    // those are NOT listed here — only constructs with genuinely no JS representation.
    public bool CanConvert(SyntaxNode node, ConversionContext context) => node switch
    {
        MakeRefExpressionSyntax => true,    // __makeref
        RefValueExpressionSyntax => true,   // __refvalue
        RefTypeExpressionSyntax => true,    // __reftype
        PointerTypeSyntax => true,
        FunctionPointerTypeSyntax => true,
        _ => false,
    };

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        context.Report(node, ConversionSeverity.Error, "EQ2001",
            $"C# construct '{Describe(node)}' cannot be transpiled to JavaScript — it has no runtime equivalent in the browser.");
        return "undefined";
    }

    private static string Describe(SyntaxNode node) => node switch
    {
        MakeRefExpressionSyntax => "__makeref",
        RefValueExpressionSyntax => "__refvalue",
        RefTypeExpressionSyntax => "__reftype",
        PointerTypeSyntax => "pointer type",
        FunctionPointerTypeSyntax => "function pointer",
        _ => node.Kind().ToString(),
    };

    // Low priority: a real strategy for any of these (should one ever exist) outranks this safety net.
    public int Priority => 2;
}
