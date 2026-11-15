using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// <c>ICollection&lt;T&gt;.IsReadOnly</c>, read of what the interface holds when the code runs: an array behind it is,
/// and the array a list is on this side has no such member, so it read undefined (#711).
/// </summary>
public class CollectionIsReadOnlyStrategy : IExpressionIrStrategy
{
    public int Priority => 12;

    public bool CanConvert(SyntaxNode node, ConversionContext context) =>
        node is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "IsReadOnly" }
        && context.SemanticHelper.GetSymbol(node) is IPropertySymbol { ContainingType.OriginalDefinition: var face }
        && face.ToDisplayString() == "System.Collections.Generic.ICollection<T>";

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Call(JsExpr.Identifier(Eq.CollectionIsReadOnly),
            context.Converter.ConvertIr(((MemberAccessExpressionSyntax)node).Expression));
    }
}
