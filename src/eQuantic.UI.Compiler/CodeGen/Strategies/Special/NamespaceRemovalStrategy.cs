using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Special;

/// <summary>
/// Removes namespace qualifiers from identifiers.
/// Example: My.Namespace.Class -> Class
/// </summary>
public class NamespaceRemovalStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not MemberAccessExpressionSyntax memberAccess)
            return false;

        // Semantic Check: Is the expression a namespace?
        var symbol = context.SemanticHelper.GetSymbol(memberAccess.Expression);
        if (symbol is INamespaceSymbol)
        {
            return true;
        }

        // Fallback: heuristic (starts with uppercase, likely static access)
        if (context.SemanticModel == null)
        {
             // This is harder to guess without semantic model, so we might skip
             return false;
        }

        return false;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)node;
        // Just return the identifier name, effectively stripping the namespace
        return memberAccess.Name.Identifier.Text;
    }

    public int Priority => 20; // High priority to strip namespaces early
}
