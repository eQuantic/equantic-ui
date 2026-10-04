using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Special;

/// <summary>
/// Strategy for nameof() operator.
/// Handles: nameof(variable) -> "variable"
/// Returns the string name of a variable, type, or member at compile time.
/// </summary>
public class NameofStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not IdentifierNameSyntax identifier)
            return false;

        return identifier.Identifier.Text == "nameof";
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;

        if (invocation.ArgumentList.Arguments.Count == 0)
            return "''";

        // nameof is a CONSTANT, and the model knows its value: the name, not its spelling, so
        // nameof(@class) is "class" where the identifier's text wrote '@class' (#520). The syntax
        // answers only where the model cannot be asked.
        var name = context.SemanticHelper.TryGetConstantValue(node, out var constant) && constant is string bound
            ? bound
            : ExtractName(invocation.ArgumentList.Arguments[0].Expression);
        return JsStringLiteral.Quote(name);
    }

    private string ExtractName(ExpressionSyntax expression)
    {
        return expression switch
        {
            // Simple identifier: nameof(x) -> "x"
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,

            // Member access: nameof(obj.Property) -> "Property"
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,

            // Generic name: nameof(List<int>) -> "List"
            GenericNameSyntax genericName => genericName.Identifier.ValueText,

            // Qualified name: nameof(System.String) -> "String"
            QualifiedNameSyntax qualifiedName => ExtractName(qualifiedName.Right),

            // Element access: nameof(array[0]) -> "array"
            ElementAccessExpressionSyntax elementAccess => ExtractName(elementAccess.Expression),

            // Invocation: nameof(Method()) -> "Method"
            InvocationExpressionSyntax invoc => ExtractName(invoc.Expression),

            _ => expression.ToString()
        };
    }

    public int Priority => 15; // Higher than InvocationStrategy
}
