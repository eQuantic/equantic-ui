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

            // Handle delegate/action Invoke() calls
            if (methodName == "Invoke")
            {
                return $"{caller}({args})";
            }

            // EXTENSION METHOD in reduced form (`node.Also(x => …)`): JS has no extensions, so the
            // call goes back to its static home with the receiver as the first argument —
            // `NodeExtensions.also(node, x => …)`. The declaring static class is emitted as its own
            // module by the app-type pipeline, and the qualified name here is what makes the
            // import scanner pick it up. BCL extensions (LINQ et al.) never reach this branch —
            // their dedicated strategies run at higher priority.
            if (symbol is { IsExtensionMethod: true, ReducedFrom: not null, ContainingType: not null })
            {
                // The declaring class never appears in the SOURCE (the call is reduced), so the
                // syntax-walking import collector can't see it — register the name we introduced.
                context.UsedAppTypes.Add(symbol.ContainingType.Name);
                var receiverFirst = string.IsNullOrEmpty(args) ? caller : $"{caller}, {args}";
                return $"{symbol.ContainingType.Name}.{methodName.ToCamelCase()}({receiverFirst})";
            }

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

            return $"{caller}.{methodName.ToCamelCase()}({args})";
        }

        // Invoking a DELEGATE VALUE by bare name (`configure(node)`, `OnSelect(i)`): the invocation
        // symbol is the delegate's Invoke, so resolve what the NAME binds to. A parameter/local is
        // a plain callable in scope — VERBATIM (it must match the binding, not our casing rules);
        // a delegate-typed MEMBER is `this.<camel>(…)` like every other member access.
        if (symbol is { MethodKind: MethodKind.DelegateInvoke }
            && methodExpression is IdentifierNameSyntax delegateIdentifier)
        {
            var delegateTarget = context.SemanticModel?.GetSymbolInfo(delegateIdentifier).Symbol;
            if (delegateTarget is IParameterSymbol or ILocalSymbol)
                return $"{delegateIdentifier.Identifier.Text}({args})";
            return $"this.{delegateIdentifier.Identifier.Text.ToCamelCase()}({args})";
        }

        // Direct invocation (Function() -> function())
        bool needsThis = false;

        // A STATIC method reached unqualified is a sibling on the enclosing type (or a `using static`
        // import) — it compiles to a class static, so it must be called THROUGH the class:
        // `DashboardView.thousands(...)`, never bare `thousands(...)` (undefined at runtime) nor
        // `this.thousands(...)`. Local functions are excluded (they are plain in-scope functions).
        if (symbol != null && symbol.IsStatic
            && symbol.MethodKind != MethodKind.LocalFunction
            && symbol.ContainingType != null)
        {
            return $"{symbol.ContainingType.Name}.{methodName.ToCamelCase()}({args})";
        }

        // Use semantic resolution if available. Local functions are NOT members of `this` even
        // though they have a containing type — they compile to a plain `function` in the same scope.
        if (symbol != null && !symbol.IsStatic && symbol.MethodKind != MethodKind.LocalFunction)
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
            return $"this.{methodName.ToCamelCase()}({args})";
        }

        return $"{methodName.ToCamelCase()}({args})";
    }

    public int Priority => 1; // Lowest priority (fallback)
}
