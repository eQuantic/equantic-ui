using System.Linq;
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
                // A STATIC member is reached through the class, not the instance: a bare `Items` reference
                // to `static Items` on `Widget` must emit `Widget.items`, never `this.items` (which the
                // uppercase fallback below would otherwise produce, leaving the value undefined at runtime).
                // (When the identifier is the `.Name` side of `other.Member`, the receiver already qualifies
                // it — emit just the member name.)
                if (symbol.IsStatic && symbol.ContainingType != null && symbol.Kind != SymbolKind.Method)
                {
                    if (identifier.Parent is MemberAccessExpressionSyntax sma && sma.Name == identifier)
                    {
                        return name.ToCamelCase();
                    }
                    return $"{symbol.ContainingType.Name}.{name.ToCamelCase()}";
                }

                // If it's a member of the current class and not static, add 'this.'
                if (!symbol.IsStatic && symbol.ContainingType != null)
                {
                    // IMPROVEMENT: Check if the identifier is part of a member access already.
                    // If it's 'other.Property', identifier 'Property' shouldn't get 'this.'
                    if (identifier.Parent is MemberAccessExpressionSyntax ma && ma.Name == identifier)
                    {
                        return name.ToCamelCase();
                    }

                    var result = $"this.{name.ToCamelCase()}";
                    
                    // If it's a method reference (not being called), add .bind(this)
                    if (symbol is IMethodSymbol)
                    {
                        var isDirectInvocation = identifier.Parent is InvocationExpressionSyntax invocation && 
                                              invocation.Expression == identifier;
                        
                        if (!isDirectInvocation)
                        {
                            result += ".bind(this)";
                        }
                    }

                    return result;
                }
            }

            // C# 12 primary-constructor parameter captured in an instance member (e.g. referenced in Build):
            // it behaves like an instance field, so emit `this.<name>`. A primary constructor is the one whose
            // declaring syntax is the type declaration itself (not a ConstructorDeclaration), which tells it
            // apart from an ordinary ctor's parameters (those stay locals inside their own ctor body).
            if (symbol is IParameterSymbol primaryParam
                && primaryParam.ContainingSymbol is IMethodSymbol { MethodKind: MethodKind.Constructor } pctor
                && pctor.DeclaringSyntaxReferences.Any(r => r.GetSyntax() is TypeDeclarationSyntax))
            {
                if (identifier.Parent is MemberAccessExpressionSyntax pma && pma.Name == identifier)
                    return name.ToCamelCase();
                return $"this.{name.ToCamelCase()}";
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
                return name.ToCamelCase();
            }
            return $"this.{name.ToCamelCase()}";
        }
        
        return name;
    }

    public int Priority => 10;
}
