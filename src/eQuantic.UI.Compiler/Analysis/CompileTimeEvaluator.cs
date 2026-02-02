using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace eQuantic.UI.Compiler.Analysis;

/// <summary>
/// Evaluates expressions at compile-time for types marked with [CompileTimeEvaluate].
/// </summary>
public class CompileTimeEvaluator
{
    private readonly SemanticModel _semanticModel;
    private readonly Dictionary<string, string> _cache = new();

    public CompileTimeEvaluator(SemanticModel semanticModel)
    {
        _semanticModel = semanticModel;
    }

    /// <summary>
    /// Tries to evaluate an expression to a constant string value.
    /// Returns null if the expression cannot be evaluated at compile-time.
    /// </summary>
    public string? TryEvaluate(ExpressionSyntax expression)
    {
        if (expression == null) return null;

        // Check cache first
        var key = expression.ToString();
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        // Check if type is marked with [CompileTimeEvaluate]
        var typeInfo = _semanticModel.GetTypeInfo(expression);
        if (typeInfo.Type == null)
        {
            Console.WriteLine($"[DEBUG] Type is null for expression: {expression}");
            return null;
        }

        if (!IsCompileTimeEvaluatable(typeInfo.Type))
        {
            Console.WriteLine($"[DEBUG] Type {typeInfo.Type.Name} is not marked with [CompileTimeEvaluate]");
            return null;
        }

        Console.WriteLine($"[DEBUG] Evaluating: {expression} (Type: {typeInfo.Type.Name})");

        // Try different evaluation strategies
        var result = EvaluateMemberAccess(expression)
            ?? EvaluateMethodCall(expression)
            ?? EvaluateBinaryExpression(expression)
            ?? EvaluateObjectCreation(expression)
            ?? EvaluateConstantValue(expression);

        if (result != null)
        {
            Console.WriteLine($"[DEBUG] Successfully evaluated to: '{result}'");
            _cache[key] = result;
        }
        else
        {
            Console.WriteLine($"[DEBUG] Failed to evaluate: {expression}");
        }

        return result;
    }

    /// <summary>
    /// Checks if a type is marked with [CompileTimeEvaluate].
    /// </summary>
    private bool IsCompileTimeEvaluatable(ITypeSymbol typeSymbol)
    {
        return typeSymbol.GetAttributes()
            .Any(attr =>
                attr.AttributeClass?.Name == "CompileTimeEvaluateAttribute" &&
                attr.AttributeClass.ContainingNamespace.ToDisplayString() == "eQuantic.UI.Core.Styling");
    }

    /// <summary>
    /// Evaluates member access expressions like TW.Display.Flex.
    /// </summary>
    private string? EvaluateMemberAccess(ExpressionSyntax expression)
    {
        if (expression is not MemberAccessExpressionSyntax memberAccess)
            return null;

        Console.WriteLine($"[DEBUG] EvaluateMemberAccess: {memberAccess}");

        var symbol = _semanticModel.GetSymbolInfo(memberAccess).Symbol;

        if (symbol == null)
        {
            Console.WriteLine($"[DEBUG] Symbol is null for: {memberAccess}");
            return null;
        }

        Console.WriteLine($"[DEBUG] Symbol: {symbol.Name} (Kind: {symbol.Kind})");

        // Handle static readonly fields
        if (symbol is IFieldSymbol { IsReadOnly: true, IsStatic: true } fieldSymbol)
        {
            Console.WriteLine($"[DEBUG] Found static readonly field: {fieldSymbol.Name}");
            var result = ExtractFieldValue(fieldSymbol);
            Console.WriteLine($"[DEBUG] ExtractFieldValue returned: {result ?? "null"}");
            return result;
        }

        // Handle static properties with constant getters
        if (symbol is IPropertySymbol { IsStatic: true } propSymbol)
        {
            Console.WriteLine($"[DEBUG] Found static property: {propSymbol.Name}");
            return ExtractPropertyValue(propSymbol);
        }

        Console.WriteLine($"[DEBUG] Symbol is neither static readonly field nor static property");
        return null;
    }

    /// <summary>
    /// Extracts the constant value from a static readonly field.
    /// </summary>
    private string? ExtractFieldValue(IFieldSymbol fieldSymbol)
    {
        // Get field declaration syntax
        var syntaxRef = fieldSymbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null) return null;

        var declaration = syntaxRef.GetSyntax();

        // Find VariableDeclaratorSyntax
        var variableDeclarator = declaration.DescendantNodesAndSelf()
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault(v => v.Identifier.Text == fieldSymbol.Name);

        if (variableDeclarator?.Initializer == null) return null;

        // Recursively evaluate the initializer
        return TryEvaluate(variableDeclarator.Initializer.Value);
    }

    /// <summary>
    /// Extracts the constant value from a static property.
    /// </summary>
    private string? ExtractPropertyValue(IPropertySymbol propSymbol)
    {
        // Get property declaration syntax
        var syntaxRef = propSymbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null) return null;

        var propertyDecl = syntaxRef.GetSyntax() as PropertyDeclarationSyntax;
        if (propertyDecl == null) return null;

        // Handle expression-bodied property: public static TailwindClass Flex => new("flex");
        if (propertyDecl.ExpressionBody != null)
        {
            return TryEvaluate(propertyDecl.ExpressionBody.Expression);
        }

        // Handle getter with body
        var getter = propertyDecl.AccessorList?.Accessors
            .FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration));

        if (getter?.ExpressionBody != null)
        {
            return TryEvaluate(getter.ExpressionBody.Expression);
        }

        if (getter?.Body != null)
        {
            var returnStmt = getter.Body.Statements
                .OfType<ReturnStatementSyntax>()
                .FirstOrDefault();

            if (returnStmt?.Expression != null)
            {
                return TryEvaluate(returnStmt.Expression);
            }
        }

        return null;
    }

    /// <summary>
    /// Evaluates method calls like TW.P(6) or TW.Dark(TW.Bg.Zinc900).
    /// </summary>
    private string? EvaluateMethodCall(ExpressionSyntax expression)
    {
        if (expression is not InvocationExpressionSyntax invocation)
            return null;

        var symbol = _semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        if (symbol == null || !symbol.IsStatic) return null;

        // Evaluate all arguments
        var argValues = new List<object?>();
        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            // Try to get constant value
            var constValue = _semanticModel.GetConstantValue(arg.Expression);
            if (constValue.HasValue)
            {
                argValues.Add(constValue.Value);
            }
            else
            {
                // Try to evaluate recursively (for nested expressions like TW.Dark(TW.Bg.Zinc900))
                var evaluatedArg = TryEvaluate(arg.Expression);
                if (evaluatedArg != null)
                {
                    argValues.Add(evaluatedArg);
                }
                else
                {
                    return null; // Cannot evaluate this argument
                }
            }
        }

        // Evaluate the method with the constant arguments
        return EvaluateMethodBody(symbol, argValues);
    }

    /// <summary>
    /// Evaluates a method body with constant arguments.
    /// </summary>
    private string? EvaluateMethodBody(IMethodSymbol method, List<object?> args)
    {
        // Get method syntax
        var syntaxRef = method.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null) return null;

        var methodDecl = syntaxRef.GetSyntax() as MethodDeclarationSyntax;
        if (methodDecl == null) return null;

        // For expression-bodied methods: => new($"p-{size}")
        if (methodDecl.ExpressionBody != null)
        {
            return EvaluateWithArguments(methodDecl.ExpressionBody.Expression, method, args);
        }

        // For block bodies: { return new($"p-{size}"); }
        if (methodDecl.Body != null)
        {
            var returnStmt = methodDecl.Body.Statements
                .OfType<ReturnStatementSyntax>()
                .FirstOrDefault();

            if (returnStmt?.Expression != null)
            {
                return EvaluateWithArguments(returnStmt.Expression, method, args);
            }
        }

        return null;
    }

    /// <summary>
    /// Evaluates an expression by substituting parameter values.
    /// </summary>
    private string? EvaluateWithArguments(ExpressionSyntax expression, IMethodSymbol method, List<object?> args)
    {
        // For simple interpolated strings: new($"p-{size}")
        if (expression is ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 1 } objCreation)
        {
            var arg = objCreation.ArgumentList.Arguments[0].Expression;

            if (arg is InterpolatedStringExpressionSyntax interpolated)
            {
                return EvaluateInterpolatedString(interpolated, method, args);
            }

            if (arg is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return literal.Token.ValueText;
            }
        }

        // Recursively evaluate
        return TryEvaluate(expression);
    }

    /// <summary>
    /// Evaluates an interpolated string by substituting parameter values.
    /// </summary>
    private string? EvaluateInterpolatedString(InterpolatedStringExpressionSyntax interpolated, IMethodSymbol method, List<object?> args)
    {
        var result = "";

        foreach (var content in interpolated.Contents)
        {
            if (content is InterpolatedStringTextSyntax text)
            {
                result += text.TextToken.ValueText;
            }
            else if (content is InterpolationSyntax interpolation)
            {
                // Check if it's a parameter reference
                if (interpolation.Expression is IdentifierNameSyntax identifier)
                {
                    var paramIndex = method.Parameters
                        .Select((p, i) => (p, i))
                        .FirstOrDefault(x => x.p.Name == identifier.Identifier.Text).i;

                    if (paramIndex >= 0 && paramIndex < args.Count)
                    {
                        result += args[paramIndex]?.ToString() ?? "";
                    }
                    else
                    {
                        return null; // Cannot resolve parameter
                    }
                }
                else
                {
                    return null; // Complex interpolation not supported
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Evaluates binary expressions like left + right.
    /// </summary>
    private string? EvaluateBinaryExpression(ExpressionSyntax expression)
    {
        if (expression is not BinaryExpressionSyntax binary)
            return null;

        // Handle + operator for TailwindClass
        if (binary.IsKind(SyntaxKind.AddExpression))
        {
            var leftValue = TryEvaluate(binary.Left);
            var rightValue = TryEvaluate(binary.Right);

            if (leftValue == null || rightValue == null) return null;

            // Simulate TailwindClass.operator+ behavior
            if (string.IsNullOrEmpty(leftValue)) return rightValue;
            if (string.IsNullOrEmpty(rightValue)) return leftValue;

            return $"{leftValue} {rightValue}";
        }

        return null;
    }

    /// <summary>
    /// Evaluates object creation expressions like new TailwindClass("flex").
    /// </summary>
    private string? EvaluateObjectCreation(ExpressionSyntax expression)
    {
        if (expression is not ObjectCreationExpressionSyntax creation)
            return null;

        // Handle: new TailwindClass("flex")
        if (creation.ArgumentList?.Arguments.Count == 1)
        {
            var arg = creation.ArgumentList.Arguments[0].Expression;

            // Direct string literal
            if (arg is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return literal.Token.ValueText;
            }

            // Interpolated string
            if (arg is InterpolatedStringExpressionSyntax interpolated)
            {
                // For simple cases without parameters
                var result = "";
                foreach (var content in interpolated.Contents)
                {
                    if (content is InterpolatedStringTextSyntax text)
                    {
                        result += text.TextToken.ValueText;
                    }
                    else
                    {
                        return null; // Cannot evaluate complex interpolations
                    }
                }
                return result;
            }

            // Constant value
            var constValue = _semanticModel.GetConstantValue(arg);
            if (constValue.HasValue && constValue.Value is string str)
            {
                return str;
            }
        }

        return null;
    }

    /// <summary>
    /// Tries to get a constant value from an expression.
    /// </summary>
    private string? EvaluateConstantValue(ExpressionSyntax expression)
    {
        var constValue = _semanticModel.GetConstantValue(expression);
        if (constValue.HasValue && constValue.Value is string str)
        {
            return str;
        }

        return null;
    }
}
