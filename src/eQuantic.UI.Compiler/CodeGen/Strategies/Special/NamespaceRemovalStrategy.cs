using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;

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
        // A TYPE reached through its namespace is imported by the symbol it binds, however much of the
        // namespace the C# spells (#625): inside `Falei.Web.Chat`, `Portal.Fold.Text(n)` wrote
        // `Fold.text(n)` and imported nothing, because the import was decided by the name as written,
        // while `Fold.Text(n)` under a using imported it. A namespace reached through another is only
        // stripped.
        if (context.SemanticHelper.GetSymbol(memberAccess) is INamedTypeSymbol type)
            type.RegisterIntroduced(context);
        return memberAccess.Name.Identifier.Text;
    }

    public int Priority => 20; // High priority to strip namespaces early
}
