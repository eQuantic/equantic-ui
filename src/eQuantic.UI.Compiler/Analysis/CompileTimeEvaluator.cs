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
            return null;

        if (!IsCompileTimeEvaluatable(typeInfo.Type))
            return null;

        // Try different evaluation strategies
        var result = EvaluateMemberAccess(expression)
            ?? EvaluateMethodCall(expression)
            ?? EvaluateBinaryExpression(expression)
            ?? EvaluateObjectCreation(expression)
            ?? EvaluateConstantValue(expression);

        if (result != null)
            _cache[key] = result;

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
    /// Evaluates member access expressions like TW.Display.Flex or TW.Text.Gray400.ToString().
    /// </summary>
    private string? EvaluateMemberAccess(ExpressionSyntax expression)
    {
        if (expression is not MemberAccessExpressionSyntax memberAccess)
            return null;

        var symbol = _semanticModel.GetSymbolInfo(memberAccess).Symbol;
        if (symbol == null)
            return null;

        // Handle .ToString() calls on evaluable expressions
        if (symbol is IMethodSymbol { Name: "ToString" } && memberAccess.Expression != null)
        {
            var baseValue = TryEvaluate(memberAccess.Expression);
            if (baseValue != null)
            {
                return baseValue; // Already a string, ToString() is a no-op
            }
        }

        // Handle static readonly fields
        if (symbol is IFieldSymbol { IsReadOnly: true, IsStatic: true } fieldSymbol)
            return ExtractFieldValue(fieldSymbol);

        // Handle static properties with constant getters
        if (symbol is IPropertySymbol { IsStatic: true } propSymbol)
            return ExtractPropertyValue(propSymbol);

        return null;
    }

    /// <summary>
    /// Extracts the constant value from a static readonly field.
    /// </summary>
    private string? ExtractFieldValue(IFieldSymbol fieldSymbol)
    {
        // Get field declaration syntax
        var syntaxRef = fieldSymbol.DeclaringSyntaxReferences.FirstOrDefault();

        // If syntax reference is null, the symbol comes from an external assembly
        // Try to extract value through runtime reflection or naming conventions
        if (syntaxRef == null)
        {

            // For TailwindClass fields, try to invoke the implicit string operator via reflection
            var result = TryExtractFromExternalAssembly(fieldSymbol);
            return result;
        }

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
    /// Tries to invoke a method from external assembly using reflection.
    /// </summary>
    private string? TryInvokeMethodViaReflection(IMethodSymbol methodSymbol, List<object?> args)
    {
        try
        {
            var assembly = methodSymbol.ContainingAssembly;

            // Try to load the assembly
            var loadedAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == assembly.Name);

            if (loadedAssembly == null && _semanticModel?.Compilation != null)
            {
                var reference = _semanticModel.Compilation.References
                    .OfType<PortableExecutableReference>()
                    .FirstOrDefault(r => r.Display != null && r.Display.Contains(assembly.Name));

                if (reference?.FilePath != null && File.Exists(reference.FilePath))
                {
                    loadedAssembly = System.Reflection.Assembly.LoadFrom(reference.FilePath);
                }
            }

            if (loadedAssembly == null)
            {
                return null;
            }

            // Get the type containing the method
            var typeName = methodSymbol.ContainingType.ToDisplayString();
            var type = FindTypeInAssembly(loadedAssembly, typeName);

            if (type == null)
            {
                return null;
            }

            // Get the method
            var paramTypes = args.Select(a => a?.GetType() ?? typeof(object)).ToArray();
            var method = type.GetMethod(methodSymbol.Name,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

            if (method == null)
            {
                return null;
            }

            // Invoke the method
            var result = method.Invoke(null, args.ToArray());

            if (result == null)
            {
                return null;
            }

            var stringValue = result.ToString();
            return stringValue;
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    /// <summary>
    /// Helper to find a type in an assembly, handling nested types.
    /// </summary>
    private System.Type? FindTypeInAssembly(System.Reflection.Assembly assembly, string typeName)
    {
        // Try with the display string first
        var type = assembly.GetType(typeName);
        if (type != null) return type;

        // Try converting nested class notation
        var parts = typeName.Split('.');
        for (int i = parts.Length - 2; i >= 0; i--)
        {
            var nsPart = string.Join(".", parts.Take(i + 1));
            var classPart = string.Join("+", parts.Skip(i + 1));
            var reflectionTypeName = $"{nsPart}.{classPart}";

            type = assembly.GetType(reflectionTypeName);
            if (type != null) return type;
        }

        return null;
    }

    /// <summary>
    /// Tries to extract field value from external assembly using reflection.
    /// </summary>
    private string? TryExtractFromExternalAssembly(IFieldSymbol fieldSymbol)
    {
        try
        {
            // Get the assembly where the field is defined
            var assembly = fieldSymbol.ContainingAssembly;

            // Try to load the actual .NET assembly
            var loadedAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == assembly.Name);

            // If not already loaded, try to load it from the compilation references
            if (loadedAssembly == null && _semanticModel?.Compilation != null)
            {
                var reference = _semanticModel.Compilation.References
                    .OfType<PortableExecutableReference>()
                    .FirstOrDefault(r => r.Display != null && r.Display.Contains(assembly.Name));

                if (reference?.FilePath != null && File.Exists(reference.FilePath))
                {
                    loadedAssembly = System.Reflection.Assembly.LoadFrom(reference.FilePath);
                }
            }

            if (loadedAssembly == null)
            {
                return null;
            }

            // Get the type containing the field
            // Roslyn gives us: "eQuantic.UI.Tailwind.TW.Display"
            // Reflection needs: "eQuantic.UI.Tailwind.TW+Display"
            var typeName = fieldSymbol.ContainingType.ToDisplayString();

            // Try with the display string first (in case it's not nested)
            var type = loadedAssembly.GetType(typeName);

            // If not found, convert nested class notation
            if (type == null)
            {
                // Split into namespace and class parts
                // For "eQuantic.UI.Tailwind.TW.Display", we need "eQuantic.UI.Tailwind.TW+Display"
                var parts = typeName.Split('.');

                // Try different combinations to find where the class nesting starts
                for (int i = parts.Length - 2; i >= 0; i--)
                {
                    var nsPart = string.Join(".", parts.Take(i + 1));
                    var classPart = string.Join("+", parts.Skip(i + 1));
                    var reflectionTypeName = $"{nsPart}.{classPart}";

                    type = loadedAssembly.GetType(reflectionTypeName);

                    if (type != null)
                    {
                        break;
                    }
                }
            }

            if (type == null)
            {
                return null;
            }

            // Get the field
            var field = type.GetField(fieldSymbol.Name,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static);

            if (field == null)
            {
                return null;
            }

            // Get the field value
            var value = field.GetValue(null);

            if (value == null)
            {
                return null;
            }

            // Convert to string using implicit operator or ToString()
            var stringValue = value.ToString();
            return stringValue;
        }
        catch (Exception ex)
        {
            return null;
        }
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

        // Special handling for TW.When() - check if first argument is runtime-evaluable
        if (symbol.Name == "When" && symbol.ContainingType.Name == "TailwindClass")
        {
            // TW.When() with runtime condition cannot be evaluated at compile-time
            // We need the condition value at runtime
            return null;
        }

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
                    // Check if it's a .ToString() call on an evaluable expression
                    if (arg.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "ToString" } toStringCall)
                    {
                        var baseValue = TryEvaluate(toStringCall.Expression);
                        if (baseValue != null)
                        {
                            argValues.Add(baseValue);
                            continue;
                        }
                    }

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

        // If no syntax reference, try to invoke via reflection (external assembly)
        if (syntaxRef == null)
        {
            return TryInvokeMethodViaReflection(method, args);
        }

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
    /// Optimized to handle long chains like TW.A + TW.B + TW.C + TW.D + ...
    /// </summary>
    private string? EvaluateBinaryExpression(ExpressionSyntax expression)
    {
        if (expression is not BinaryExpressionSyntax binary)
            return null;

        // Handle + operator for TailwindClass
        if (binary.IsKind(SyntaxKind.AddExpression))
        {
            // Flatten the expression tree to avoid deep recursion
            var parts = new List<string>();
            if (!FlattenAddExpression(expression, parts))
                return null;

            // Join all non-empty parts with spaces
            var result = string.Join(" ", parts.Where(p => !string.IsNullOrEmpty(p)));
            return string.IsNullOrEmpty(result) ? null : result;
        }

        return null;
    }

    /// <summary>
    /// Flattens a tree of + expressions into a list of evaluated values.
    /// Returns false if any sub-expression cannot be evaluated.
    /// </summary>
    private bool FlattenAddExpression(ExpressionSyntax expression, List<string> parts)
    {
        // If it's a binary + expression, recursively flatten both sides
        if (expression is BinaryExpressionSyntax { OperatorToken.Text: "+" } binary)
        {
            if (!FlattenAddExpression(binary.Left, parts))
                return false;
            if (!FlattenAddExpression(binary.Right, parts))
                return false;
            return true;
        }

        // Otherwise, evaluate the expression
        var value = TryEvaluate(expression);
        if (value == null)
            return false;

        if (!string.IsNullOrEmpty(value))
            parts.Add(value);

        return true;
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
