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
    private readonly Dictionary<string, string> _cache = [];
    private readonly Dictionary<string, ITypeSymbol> _cacheTypes = [];
    private readonly HashSet<string> _evaluationStack = [];

    public CompileTimeEvaluator(SemanticModel semanticModel)
    {
        _semanticModel = semanticModel;
    }

    /// <summary>
    /// Clears the evaluation cache.
    /// Useful when semantic model changes or for unit testing.
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
        _cacheTypes.Clear();
        _evaluationStack.Clear();
    }

    /// <summary>
    /// Gets diagnostic information about cached evaluations.
    /// Useful for debugging and optimization.
    /// </summary>
    public (int CachedCount, int TypesCached) GetCacheStats()
    {
        return (_cache.Count, _cacheTypes.Count);
    }

    /// <summary>
    /// Checks if an expression has been successfully cached.
    /// </summary>
    public bool IsCached(string expressionText)
    {
        return _cache.ContainsKey(expressionText);
    }

    /// <summary>
    /// Tries to evaluate an expression to a constant string value.
    /// Returns null if the expression cannot be evaluated at compile-time.
    /// Enhanced with recursion detection and type caching.
    /// </summary>
    public string? TryEvaluate(ExpressionSyntax expression)
    {
        if (expression == null) return null;

        // Check cache first
        var key = expression.ToString();
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        // Detect circular dependencies / infinite recursion
        if (_evaluationStack.Contains(key))
        {
            return null; // Circular reference detected
        }

        try
        {
            // Add to stack to detect recursion
            _evaluationStack.Add(key);

            // Check if type is marked with [CompileTimeEvaluate]
            var typeInfo = _semanticModel.GetTypeInfo(expression);
            var type = typeInfo.Type;

            if (type == null)
                return null;

            // Cache the type for debugging/analysis
            _cacheTypes[key] = type;

            if (!IsCompileTimeEvaluatable(type))
                return null;

            // Try different evaluation strategies in order of likelihood
            var result = EvaluateMemberAccess(expression)
                ?? EvaluateMethodCall(expression)
                ?? EvaluateBinaryExpression(expression)
                ?? EvaluateObjectCreation(expression)
                ?? EvaluateConstantValue(expression);

            // Cache successful evaluations
            if (result != null)
                _cache[key] = result;

            return result;
        }
        finally
        {
            // Always remove from stack when done
            _evaluationStack.Remove(key);
        }
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
    /// Enhanced with better type matching and implicit operator handling.
    /// </summary>
    private string? TryInvokeMethodViaReflection(IMethodSymbol methodSymbol, List<object?> args, List<ITypeSymbol?>? argTypes = null)
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

            // Try to find the best matching method
            var methods = type.GetMethods(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(m => m.Name == methodSymbol.Name)
                .ToList();

            System.Reflection.MethodInfo? method = null;

            // If we have type information, try to match parameters more precisely
            if (argTypes != null && argTypes.Count == args.Count)
            {
                method = FindBestMethodMatch(methods, args, argTypes, loadedAssembly);
            }

            // Fallback to simple matching
            if (method == null)
            {
                method = methods.FirstOrDefault(m => m.GetParameters().Length == args.Count);
            }

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

            // Handle implicit string conversion if result is a struct
            var resultType = result.GetType();
            if (resultType.IsValueType && !resultType.IsPrimitive)
            {
                // Try to find implicit operator to string
                var implicitOp = resultType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    .FirstOrDefault(m =>
                        m.Name == "op_Implicit" &&
                        m.ReturnType == typeof(string));

                if (implicitOp != null)
                {
                    var converted = implicitOp.Invoke(null, new[] { result });
                    if (converted is string str)
                    {
                        return str;
                    }
                }
            }

            var stringValue = result.ToString();
            return stringValue;
        }
        catch
        {
            // Silently fail - method invocation via reflection can fail for many reasons
            return null;
        }
    }

    /// <summary>
    /// Finds the best matching method based on parameter types.
    /// </summary>
    private System.Reflection.MethodInfo? FindBestMethodMatch(
        List<System.Reflection.MethodInfo> methods,
        List<object?> args,
        List<ITypeSymbol?> argTypes,
        System.Reflection.Assembly assembly)
    {
        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            if (parameters.Length != args.Count)
                continue;

            var allMatch = true;
            for (int i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;
                var argType = argTypes[i];

                // Check if argument type matches
                if (argType != null)
                {
                    var argTypeName = argType.ToDisplayString();
                    var reflectionType = FindTypeInAssembly(assembly, argTypeName);

                    if (reflectionType != null && paramType.IsAssignableFrom(reflectionType))
                    {
                        continue;
                    }
                }

                // Check if argument value type matches
                if (args[i] != null && paramType.IsAssignableFrom(args[i]!.GetType()))
                {
                    continue;
                }

                // Check for primitive type matches
                if (args[i] != null && IsPrimitiveCompatible(paramType, args[i]!.GetType()))
                {
                    continue;
                }

                allMatch = false;
                break;
            }

            if (allMatch)
                return method;
        }

        return null;
    }

    /// <summary>
    /// Checks if two primitive types are compatible.
    /// </summary>
    private static bool IsPrimitiveCompatible(Type targetType, Type sourceType)
    {
        if (targetType == sourceType)
            return true;

        // Handle numeric conversions
        Type[] numericTypes =
        [
            typeof(int), typeof(long), typeof(short), typeof(byte),
            typeof(uint), typeof(ulong), typeof(ushort), typeof(sbyte),
            typeof(float), typeof(double), typeof(decimal)
        ];

        return numericTypes.Contains(targetType) && numericTypes.Contains(sourceType);
    }

    /// <summary>
    /// Helper to find a type in an assembly, handling nested types.
    /// </summary>
    private Type? FindTypeInAssembly(System.Reflection.Assembly assembly, string typeName)
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
        catch
        {
            // Silently fail - field access via reflection can fail for many reasons
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
    /// Enhanced to handle implicit conversions and complex nested expressions.
    /// Includes symbolic compilation for TW/TailwindClass methods.
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

        // Try symbolic compilation for known TW/TailwindClass methods
        var symbolicResult = TrySymbolicCompilation(invocation, symbol);
        if (symbolicResult != null)
            return symbolicResult;

        // Evaluate all arguments with enhanced type handling
        var argValues = new List<object?>();
        var argTypes = new List<ITypeSymbol?>();

        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            var argTypeInfo = _semanticModel.GetTypeInfo(arg.Expression);
            argTypes.Add(argTypeInfo.Type);

            // Try to get constant value first
            var constValue = _semanticModel.GetConstantValue(arg.Expression);
            if (constValue.HasValue)
            {
                argValues.Add(constValue.Value);
                continue;
            }

            // Try to evaluate recursively (for nested expressions like TW.Dark(TW.Bg.Zinc900))
            var evaluatedArg = TryEvaluate(arg.Expression);
            if (evaluatedArg != null)
            {
                argValues.Add(evaluatedArg);
                continue;
            }

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

            // Check if we can convert via implicit operator
            if (argTypeInfo.Type != null && IsCompileTimeEvaluatable(argTypeInfo.Type))
            {
                // Try to apply implicit string conversion
                var implicitStringValue = TryApplyImplicitConversion(arg.Expression, argTypeInfo.Type);
                if (implicitStringValue != null)
                {
                    argValues.Add(implicitStringValue);
                    continue;
                }
            }

            return null; // Cannot evaluate this argument
        }

        // Evaluate the method with the constant arguments
        return EvaluateMethodBody(symbol, argValues, argTypes);
    }

    /// <summary>
    /// Symbolic compilation for TW/TailwindClass methods.
    /// Analyzes method source code and executes it symbolically (no hardcoding).
    /// </summary>
    private string? TrySymbolicCompilation(InvocationExpressionSyntax invocation, IMethodSymbol methodSymbol)
    {
        var containingTypeName = methodSymbol.ContainingType.Name;

        // Only handle TW or TailwindClass methods
        if (containingTypeName != "TW" && containingTypeName != "TailwindClass")
            return null;

        // Parse arguments symbolically
        var args = invocation.ArgumentList.Arguments
            .Select(arg => TryEvaluate(arg.Expression))
            .ToList();

        // If any argument failed to evaluate, we can't symbolically compile
        if (args.Any(a => a == null))
            return null;

        var evaluatedArgs = args.Where(a => a != null).Select(a => a!).ToList();

        // Try to get method source code
        var syntaxRef = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null)
        {
            // Method is from external assembly - try reflection
            return TryInvokeMethodViaReflection(methodSymbol, evaluatedArgs.Cast<object?>().ToList(), null);
        }

        var methodDecl = syntaxRef.GetSyntax() as MethodDeclarationSyntax;
        if (methodDecl == null)
            return null;

        // Analyze method implementation to detect pattern
        var pattern = DetectMethodPattern(methodDecl, methodSymbol);
        if (pattern != null)
        {
            return ExecutePattern(pattern, evaluatedArgs);
        }

        // Fallback to regular method body evaluation
        return null;
    }

    /// <summary>
    /// Detects common patterns in method implementations.
    /// Returns pattern descriptor if recognized, null otherwise.
    /// </summary>
    private static MethodPattern? DetectMethodPattern(MethodDeclarationSyntax methodDecl, IMethodSymbol methodSymbol)
    {
        // Get method body expression
        ExpressionSyntax? bodyExpr = methodDecl.ExpressionBody?.Expression;

        if (bodyExpr == null && methodDecl.Body != null)
        {
            var returnStmt = methodDecl.Body.Statements
                .OfType<ReturnStatementSyntax>()
                .FirstOrDefault();
            bodyExpr = returnStmt?.Expression;
        }

        if (bodyExpr == null)
            return null;

        // Pattern 1: new Type($"{arg}/{opacity}") - Interpolated string constructor
        if (bodyExpr is ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 1 } objCreation)
        {
            var arg = objCreation.ArgumentList.Arguments[0].Expression;

            // Interpolated string pattern
            if (arg is InterpolatedStringExpressionSyntax interpolated)
            {
                return new MethodPattern
                {
                    Type = PatternType.InterpolatedString,
                    Template = interpolated,
                    Parameters = [.. methodSymbol.Parameters]
                };
            }

            // String.Join pattern: new Type(string.Join(" ", args))
            if (arg is InvocationExpressionSyntax invocation &&
                invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.Text == "Join")
            {
                return new MethodPattern
                {
                    Type = PatternType.StringJoin,
                    Template = invocation,
                    Parameters = [.. methodSymbol.Parameters]
                };
            }

            // String.Format pattern: new Type(string.Format("{0}:{1}", prefix, value))
            if (arg is InvocationExpressionSyntax formatInvocation &&
                formatInvocation.Expression is MemberAccessExpressionSyntax formatAccess &&
                formatAccess.Name.Identifier.Text == "Format")
            {
                return new MethodPattern
                {
                    Type = PatternType.StringFormat,
                    Template = formatInvocation,
                    Parameters = [.. methodSymbol.Parameters]
                };
            }

            // Simple parameter passthrough: new Type(paramName)
            if (arg is IdentifierNameSyntax identifier)
            {
                return new MethodPattern
                {
                    Type = PatternType.ParameterPassthrough,
                    Template = identifier,
                    Parameters = [.. methodSymbol.Parameters]
                };
            }

            // Binary expression: new Type(prefix + ":" + value)
            if (arg is BinaryExpressionSyntax binary)
            {
                return new MethodPattern
                {
                    Type = PatternType.BinaryExpression,
                    Template = binary,
                    Parameters = [.. methodSymbol.Parameters]
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Executes a detected pattern with the given arguments.
    /// </summary>
    private string? ExecutePattern(MethodPattern pattern, List<string> args)
    {
        return pattern.Type switch
        {
            PatternType.InterpolatedString => ExecuteInterpolatedStringPattern(pattern, args),
            PatternType.StringJoin => ExecuteStringJoinPattern(pattern, args),
            PatternType.StringFormat => ExecuteStringFormatPattern(pattern, args),
            PatternType.ParameterPassthrough => ExecuteParameterPassthroughPattern(pattern, args),
            PatternType.BinaryExpression => ExecuteBinaryExpressionPattern(pattern, args),
            _ => null
        };
    }

    /// <summary>
    /// Executes string.Join pattern.
    /// Example: string.Join(" ", args.Select(c => $"hover:{c}")) with args ["bg-white", "text-black"]
    /// => "hover:bg-white hover:text-black"
    /// </summary>
    private string? ExecuteStringJoinPattern(MethodPattern pattern, List<string> args)
    {
        if (pattern.Template is not InvocationExpressionSyntax invocation)
            return null;

        // For params arrays, we assume all args are the array elements
        // This is a simplified approach - could be enhanced to parse the actual LINQ expression
        if (pattern.Parameters.Count == 1 && pattern.Parameters[0].IsParams)
        {
            // Extract separator from string.Join("separator", ...)
            if (invocation.ArgumentList.Arguments.Count >= 1)
            {
                var separatorArg = invocation.ArgumentList.Arguments[0].Expression;
                if (separatorArg is LiteralExpressionSyntax literal)
                {
                    var separator = literal.Token.ValueText;
                    return string.Join(separator, args);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Executes string.Format pattern.
    /// Example: string.Format("{0}:{1}", prefix, value) with args ["dark", "bg-zinc-900"]
    /// => "dark:bg-zinc-900"
    /// </summary>
    private string? ExecuteStringFormatPattern(MethodPattern pattern, List<string> args)
    {
        if (pattern.Template is not InvocationExpressionSyntax invocation)
            return null;

        if (invocation.ArgumentList.Arguments.Count < 1)
            return null;

        var formatArg = invocation.ArgumentList.Arguments[0].Expression;
        if (formatArg is not LiteralExpressionSyntax formatLiteral)
            return null;

        var format = formatLiteral.Token.ValueText;

        try
        {
            return string.Format(format, args.Cast<object>().ToArray());
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Executes parameter passthrough pattern.
    /// Example: new Type(value) with args ["bg-white"] => "bg-white"
    /// </summary>
    private string? ExecuteParameterPassthroughPattern(MethodPattern pattern, List<string> args)
    {
        if (pattern.Template is not IdentifierNameSyntax identifier)
            return null;

        var paramIndex = pattern.Parameters
            .FindIndex(p => p.Name == identifier.Identifier.Text);

        if (paramIndex >= 0 && paramIndex < args.Count)
        {
            return args[paramIndex];
        }

        return null;
    }

    /// <summary>
    /// Executes binary expression pattern.
    /// Example: prefix + ":" + value with args ["dark", "bg-zinc-900"] => "dark:bg-zinc-900"
    /// </summary>
    private string? ExecuteBinaryExpressionPattern(MethodPattern pattern, List<string> args)
    {
        if (pattern.Template is not BinaryExpressionSyntax binary)
            return null;

        // Create a simple evaluator for the binary expression
        var leftValue = EvaluateBinaryOperand(binary.Left, pattern, args);
        var rightValue = EvaluateBinaryOperand(binary.Right, pattern, args);

        if (leftValue == null || rightValue == null)
            return null;

        // Only support string concatenation for now
        if (binary.IsKind(SyntaxKind.AddExpression))
        {
            return leftValue + rightValue;
        }

        return null;
    }

    /// <summary>
    /// Evaluates an operand in a binary expression (recursively).
    /// </summary>
    private string? EvaluateBinaryOperand(ExpressionSyntax operand, MethodPattern pattern, List<string> args)
    {
        return operand switch
        {
            // String literal: "hello"
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression)
                => literal.Token.ValueText,

            // Parameter reference: paramName
            IdentifierNameSyntax identifier => GetParameterValue(identifier.Identifier.Text, pattern, args),

            // Nested binary: a + b
            BinaryExpressionSyntax nestedBinary => ExecuteBinaryExpressionPattern(
                new MethodPattern { Type = PatternType.BinaryExpression, Template = nestedBinary, Parameters = pattern.Parameters },
                args),

            _ => null
        };
    }

    /// <summary>
    /// Gets a parameter value by name.
    /// </summary>
    private static string? GetParameterValue(string paramName, MethodPattern pattern, List<string> args)
    {
        var paramIndex = pattern.Parameters.FindIndex(p => p.Name == paramName);
        return paramIndex >= 0 && paramIndex < args.Count ? args[paramIndex] : null;
    }

    /// <summary>
    /// Executes an interpolated string pattern by substituting arguments.
    /// Example: new($"{className}/{opacity}") with args ["bg-white", "80"] => "bg-white/80"
    /// </summary>
    private string? ExecuteInterpolatedStringPattern(MethodPattern pattern, List<string> args)
    {
        if (pattern.Template is not InterpolatedStringExpressionSyntax interpolated)
            return null;

        var result = "";

        foreach (var content in interpolated.Contents)
        {
            switch (content)
            {
                case InterpolatedStringTextSyntax text:
                    result += text.TextToken.ValueText;
                    break;

                case InterpolationSyntax interpolation:
                {
                    // Check if it's a parameter reference
                    if (interpolation.Expression is IdentifierNameSyntax identifier)
                    {
                        var paramIndex = pattern.Parameters
                            .FindIndex(p => p.Name == identifier.Identifier.Text);

                        if (paramIndex >= 0 && paramIndex < args.Count)
                        {
                            result += args[paramIndex];
                        }
                        else
                        {
                            return null; // Cannot resolve parameter
                        }
                    }
                    else if (interpolation.Expression is InvocationExpressionSyntax invocation)
                    {
                        // Handle method calls within interpolation: $"{classes.Join(" ")}"
                        var evaluated = EvaluateMethodCall(invocation);
                        if (evaluated != null)
                        {
                            result += evaluated;
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        return null; // Complex interpolation not supported
                    }
                    break;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Pattern descriptor for method implementations.
    /// </summary>
    private class MethodPattern
    {
        public PatternType Type { get; set; }
        public SyntaxNode? Template { get; set; }
        public List<IParameterSymbol> Parameters { get; set; } = [];
    }

    /// <summary>
    /// Known method pattern types that can be evaluated at compile-time.
    /// </summary>
    private enum PatternType
    {
        /// <summary>
        /// Interpolated string: new($"{arg1}/{arg2}")
        /// </summary>
        InterpolatedString,

        /// <summary>
        /// String.Join: new(string.Join(" ", args))
        /// </summary>
        StringJoin,

        /// <summary>
        /// String.Format: new(string.Format("{0}:{1}", arg1, arg2))
        /// </summary>
        StringFormat,

        /// <summary>
        /// Parameter passthrough: new(value)
        /// </summary>
        ParameterPassthrough,

        /// <summary>
        /// Binary expression concatenation: new(prefix + ":" + value)
        /// </summary>
        BinaryExpression
    }

    /// <summary>
    /// Tries to apply implicit string conversion to a compile-time evaluatable type.
    /// Handles structs like TailwindClass that have implicit operators.
    /// </summary>
    private string? TryApplyImplicitConversion(ExpressionSyntax expression, ITypeSymbol typeSymbol)
    {
        // First evaluate the expression to get its raw value
        var rawValue = TryEvaluate(expression);
        if (rawValue != null)
        {
            return rawValue; // Already converted to string
        }

        // Check if the type has an implicit operator to string
        var stringType = _semanticModel.Compilation.GetSpecialType(SpecialType.System_String);
        var conversion = _semanticModel.Compilation.ClassifyConversion(typeSymbol, stringType);

        if (conversion.IsImplicit && conversion.IsUserDefined)
        {
            // Try to evaluate the expression and apply the conversion
            // This handles cases like TW.Bg.White which returns TailwindClass
            // The recursive TryEvaluate should handle this
            return TryEvaluate(expression);
        }

        return null;
    }

    /// <summary>
    /// Evaluates a method body with constant arguments.
    /// Enhanced to handle type information for better reflection invocation.
    /// </summary>
    private string? EvaluateMethodBody(IMethodSymbol method, List<object?> args, List<ITypeSymbol?>? argTypes = null)
    {
        // Get method syntax
        var syntaxRef = method.DeclaringSyntaxReferences.FirstOrDefault();

        // If no syntax reference, try to invoke via reflection (external assembly)
        if (syntaxRef == null)
        {
            return TryInvokeMethodViaReflection(method, args, argTypes);
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
    /// Enhanced to handle more complex patterns including conditional expressions.
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

            // Handle binary expressions and other complex initializers
            if (arg is BinaryExpressionSyntax binary)
            {
                return EvaluateBinaryExpressionWithParams(binary, method, args);
            }
        }

        // Handle conditional expressions: condition ? trueExpr : falseExpr
        if (expression is ConditionalExpressionSyntax conditional)
        {
            return EvaluateConditionalWithParams(conditional, method, args);
        }

        // Recursively evaluate
        return TryEvaluate(expression);
    }

    /// <summary>
    /// Evaluates binary expressions with parameter substitution.
    /// </summary>
    private string? EvaluateBinaryExpressionWithParams(BinaryExpressionSyntax binary, IMethodSymbol method, List<object?> args)
    {
        // Handle string concatenation
        if (binary.IsKind(SyntaxKind.AddExpression))
        {
            var left = EvaluateExpressionWithParams(binary.Left, method, args);
            var right = EvaluateExpressionWithParams(binary.Right, method, args);

            if (left != null && right != null)
            {
                return left + right;
            }
        }

        return null;
    }

    /// <summary>
    /// Evaluates conditional expressions with parameter substitution.
    /// </summary>
    private string? EvaluateConditionalWithParams(ConditionalExpressionSyntax conditional, IMethodSymbol method, List<object?> args)
    {
        // Try to evaluate the condition
        var conditionValue = EvaluateExpressionWithParams(conditional.Condition, method, args);

        if (conditionValue != null && bool.TryParse(conditionValue, out var boolValue))
        {
            var targetExpr = boolValue ? conditional.WhenTrue : conditional.WhenFalse;
            return EvaluateExpressionWithParams(targetExpr, method, args);
        }

        // If we can't evaluate the condition, we can't evaluate at compile-time
        return null;
    }

    /// <summary>
    /// Evaluates any expression with parameter substitution.
    /// </summary>
    private string? EvaluateExpressionWithParams(ExpressionSyntax expression, IMethodSymbol method, List<object?> args)
    {
        // Handle parameter references
        if (expression is IdentifierNameSyntax identifier)
        {
            var paramIndex = method.Parameters
                .Select((p, i) => (p, i))
                .FirstOrDefault(x => x.p.Name == identifier.Identifier.Text).i;

            if (paramIndex >= 0 && paramIndex < args.Count)
            {
                return args[paramIndex]?.ToString();
            }
        }

        // Handle literals
        if (expression is LiteralExpressionSyntax literal)
        {
            var constValue = _semanticModel.GetConstantValue(literal);
            if (constValue.HasValue)
            {
                return constValue.Value?.ToString();
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
    /// Evaluates object creation expressions like new TailwindClass("flex") or new("flex").
    /// </summary>
    private string? EvaluateObjectCreation(ExpressionSyntax expression)
    {
        ArgumentListSyntax? argumentList = null;

        if (expression is ObjectCreationExpressionSyntax creation)
        {
            argumentList = creation.ArgumentList;
        }
        else if (expression is ImplicitObjectCreationExpressionSyntax implicitCreation)
        {
            argumentList = implicitCreation.ArgumentList;
        }
        else
        {
            return null;
        }

        // Handle: new TailwindClass("flex")
        if (argumentList?.Arguments.Count == 1)
        {
            var arg = argumentList.Arguments[0].Expression;

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
