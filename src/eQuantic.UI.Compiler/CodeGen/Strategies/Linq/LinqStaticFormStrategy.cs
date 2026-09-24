using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// A LINQ operator called in its STATIC form, <c>Enumerable.Count(xs)</c> or, under
/// <c>using static System.Linq.Enumerable</c>, <c>Count(xs)</c>. The LINQ strategies read the source
/// from the left of the member access, which in this form is the type: the call came out as
/// <c>Enumerable.filter(xs).length</c>, a ReferenceError in the browser, and a
/// <c>ToDictionary</c> keyed its dictionary by the source, all without a diagnostic. The form is
/// refused here, above every strategy that could claim it, in words that say what to write.
/// <para>
/// <c>Max</c> and <c>Min</c> pass: <see cref="MinMaxStrategy"/> binds their arguments by parameter,
/// and the conformance suite runs both forms. Any other operator that learns to do the same leaves
/// this list the same way, with its cases.
/// </para>
/// </summary>
internal class LinqStaticFormStrategy : IExpressionIrStrategy
{
    /// <summary>Above the LINQ strategies (10 to 12), and above every strategy that claimed one of these
    /// calls by its name or receiver (the list and string statics at 15 and 20).</summary>
    public int Priority => 45;

    public bool CanConvert(SyntaxNode node, ConversionContext context) =>
        node is InvocationExpressionSyntax invocation
        && context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol
        {
            MethodKind: MethodKind.Ordinary, IsExtensionMethod: true, Name: not ("Max" or "Min"),
        } method
        && context.SemanticHelper.IsLinqExtension(method.ContainingType);

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var name = ((IMethodSymbol)context.SemanticHelper.GetSymbol(node)!).Name;
        return JsExpr.Opaque(context.Unhandled(node, $"LINQ {name} in its static form (write source.{name}(…))"));
    }
}
