using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for identifier names.
/// Handles:
/// - Local variables -> name
/// - Properties/Fields -> this.name
/// - Console -> console
/// </summary>
public class IdentifierStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is IdentifierNameSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var identifier = (IdentifierNameSyntax)node;
        var name = identifier.Identifier.Text;
        
        // Map 'Component' property (in State classes) to 'this._component'
        if (name == "Component") return "this._component";

        // Priority: Semantic Check > String Check (Fallback)
        var symbol = context.SemanticHelper.GetSymbol(identifier);
        
        // If it's a type symbol, return as is (to allow EnumStrategy to work)
        if (symbol is ITypeSymbol || symbol is INamedTypeSymbol) return name;

        if (context.SemanticHelper.IsSystemConsole(symbol)) return "console";
        if (context.SemanticModel == null && name == "Console") return "console";
        
        // Resolve member access prefix (this.) using semantic model
        if (symbol != null)
        {
            if (symbol.Kind == SymbolKind.Field || symbol.Kind == SymbolKind.Property || symbol.Kind == SymbolKind.Method)
            {
                // If it's a member of the current class and not static, add 'this.'
                if (!symbol.IsStatic && symbol.ContainingType != null)
                {
                    // IMPROVEMENT: Check if the identifier is part of a member access already.
                    // If it's 'other.Property', identifier 'Property' shouldn't get 'this.'
                    if (identifier.Parent is MemberAccessExpressionSyntax ma && ma.Name == identifier)
                    {
                        return ToCamelCase(name);
                    }

                    return $"this.{ToCamelCase(name)}";
                }
            }
        }

        // Fallback Heuristics
        if (name.StartsWith("_"))
        {
            return $"this.{name}";
        }
        
        // If it starts with Uppercase and not obviously a local/param, it's likely a property
        if (char.IsUpper(name[0]))
        {
             // If parent is MemberAccess as the Name part, don't prefix
            if (identifier.Parent is MemberAccessExpressionSyntax ma && ma.Name == identifier)
            {
                return ToCamelCase(name);
            }
            return $"this.{ToCamelCase(name)}";
        }
        
        return name;
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    public int Priority => 10;
}
