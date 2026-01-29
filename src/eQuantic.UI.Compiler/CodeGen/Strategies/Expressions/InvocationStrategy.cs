using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// General strategy for method invocations (fallback).
/// Handles:
/// - Console.WriteLine -> console.log
/// - Math methods
/// - Dictionary methods (TryGetValue, ContainsKey)
/// - Service provider methods (GetService, GetRequiredService)
/// - General method calls
/// Note: String and List methods are handled by dedicated strategies in Primitives/
/// </summary>
public class InvocationStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is InvocationExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var methodExpression = invocation.Expression;
        var methodName = methodExpression.ToString();

        if (methodExpression is MemberAccessExpressionSyntax memberAccess)
        {
            methodName = memberAccess.Name.Identifier.Text;
        }

        // 1. Resolve Arguments
        var argsList = new List<string>();
        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            if (arg.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword))
            {
                if (arg.Expression is DeclarationExpressionSyntax decl)
                {
                    argsList.Add(decl.Designation.ToString());
                }
                else
                {
                    argsList.Add(arg.Expression.ToString().Trim());
                }
            }
            else
            {
                argsList.Add(context.Converter.ConvertExpression(arg.Expression));
            }
        }
        var args = string.Join(", ", argsList);

        // 2. Semantic Resolution
        var symbol = context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol;

        // 4. General Method Call (Fallback)
        if (methodExpression is MemberAccessExpressionSyntax genAccess)
        {
            var caller = context.Converter.ConvertExpression(genAccess.Expression);
            
            // Local method call (this.Method)
            bool isLocal = false;
            if (symbol != null && !symbol.IsStatic)
            {
                 isLocal = true; 
            }
            else if (context.SemanticModel == null && char.IsUpper(methodName[0])) 
            {
                isLocal = true; // Heuristic
            }

            return $"{caller}.{ToCamelCase(methodName)}({args})";
        }

        // Direct invocation (Function() -> function())
        bool needsThis = false;
        
        // Use semantic resolution if available
        if (symbol != null && !symbol.IsStatic)
        {
            if (symbol.ContainingType != null)
            {
                needsThis = true;
            }
        }
        
        // Heuristic fallback
        if (!needsThis && !string.IsNullOrEmpty(context.CurrentClassName))
        {
            if (char.IsUpper(methodName[0]) && symbol == null)
            {
                 needsThis = true;
            }
        }

        if (needsThis)
        {
            return $"this.{ToCamelCase(methodName)}({args})";
        }

        return $"{ToCamelCase(methodName)}({args})";
    }

    private string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    public int Priority => 1; // Lowest priority (fallback)
}
