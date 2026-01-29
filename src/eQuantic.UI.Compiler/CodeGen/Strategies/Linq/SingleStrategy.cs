using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Converts LINQ .Single() and .SingleOrDefault() to JavaScript .find().
/// Note: In C#, Single throws if there are 0 or more than 1 elements.
/// In JS, we use find() which returns the first match (simpler behavior for UI code).
/// - Single() -> array.find(() => true) (assumes single element)
/// - Single(predicate) -> array.find(predicate)
/// - SingleOrDefault() -> array.find(() => true) ?? null
/// - SingleOrDefault(predicate) -> array.find(predicate) ?? null
/// </summary>
public class SingleStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        var methodName = memberAccess.Name.Identifier.Text;
        if (methodName != "Single" && methodName != "SingleOrDefault")
            return false;

        var symbol = context.SemanticHelper.GetSymbol(invocation);
        if (symbol is IMethodSymbol ms && context.SemanticHelper.IsLinqExtension(ms.ContainingType))
            return true;

        if (context.SemanticModel == null || symbol == null)
            return true;

        return false;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var methodName = memberAccess.Name.Identifier.Text;

        var caller = context.Converter.ConvertExpression(memberAccess.Expression);
        var args = invocation.ArgumentList.Arguments;
        var isOrDefault = methodName == "SingleOrDefault";
        var defaultSuffix = isOrDefault ? " ?? null" : "";

        if (args.Count > 0)
        {
            var predicate = context.Converter.ConvertExpression(args[0].Expression);
            return $"({caller}.find({predicate}){defaultSuffix})";
        }

        // Single() without predicate - just get first element
        return $"({caller}[0]{defaultSuffix})";
    }

    public int Priority => 10;
}
