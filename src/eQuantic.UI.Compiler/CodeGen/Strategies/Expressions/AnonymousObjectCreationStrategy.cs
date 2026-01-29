using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

public class AnonymousObjectCreationStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is AnonymousObjectCreationExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var creation = (AnonymousObjectCreationExpressionSyntax)node;
        var members = new List<string>();

        foreach (var decl in creation.Initializers)
        {
            var name = decl.NameEquals?.Name.Identifier.Text;
            var expression = context.Converter.ConvertExpression(decl.Expression);

            if (name != null)
            {
                members.Add($"{ToCamelCase(name)}: {expression}");
            }
            else
            {
                // Inferred name: new { x } -> { x: x }
                // Need to extract name from expression if simple identifier
                var inferredName = GetInferredName(decl.Expression);
                if (inferredName != null)
                {
                    members.Add($"{ToCamelCase(inferredName)}: {expression}");
                }
                else
                {
                    // Fallback, tough this shouldn't happen in valid C# anonymous objects
                    members.Add(expression);
                }
            }
        }

        return $"{{ {string.Join(", ", members)} }}";
    }

    private string? GetInferredName(ExpressionSyntax expression)
    {
        if (expression is IdentifierNameSyntax id) return id.Identifier.Text;
        if (expression is MemberAccessExpressionSyntax ma) return ma.Name.Identifier.Text;
        return null; 
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    public int Priority => 10;
}
