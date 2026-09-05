# Compiler Implementation Guide - Compile-Time Evaluation

## Quick Start for Compiler Developers

This guide shows how to implement compile-time evaluation support in the eQuantic.UI compiler (eqc).

## Step 1: Add Attribute Detection

**File:** `src/eQuantic.UI.Compiler/Analysis/TypeAnalyzer.cs` (or similar)

```csharp
public class TypeAnalyzer
{
    public bool IsCompileTimeEvaluatable(ITypeSymbol typeSymbol)
    {
        return typeSymbol.GetAttributes()
            .Any(attr => 
                attr.AttributeClass?.Name == "CompileTimeEvaluateAttribute" &&
                attr.AttributeClass.ContainingNamespace.ToDisplayString() == "eQuantic.UI.Web.Styling");
    }

    public CompileTimeEvaluateConfig GetEvaluationConfig(ITypeSymbol typeSymbol)
    {
        var attr = typeSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "CompileTimeEvaluateAttribute");

        if (attr == null) return null;

        return new CompileTimeEvaluateConfig
        {
            WarnOnFailure = attr.NamedArguments
                .FirstOrDefault(a => a.Key == "WarnOnEvaluationFailure")
                .Value.Value as bool? ?? true,
            
            FallbackBehavior = attr.NamedArguments
                .FirstOrDefault(a => a.Key == "FallbackBehavior")
                .Value.Value as string ?? "EmitRuntimeCode"
        };
    }
}

public class CompileTimeEvaluateConfig
{
    public bool WarnOnFailure { get; set; }
    public string FallbackBehavior { get; set; }  // "EmitRuntimeCode", "Error", "EmitNull"
}
```

## Step 2: Create Expression Evaluator

**File:** `src/eQuantic.UI.Compiler/Evaluation/ExpressionEvaluator.cs`

```csharp
public class ExpressionEvaluator
{
    private readonly SemanticModel _semanticModel;
    private readonly TypeAnalyzer _typeAnalyzer;

    public string? TryEvaluate(ExpressionSyntax expression)
    {
        var typeInfo = _semanticModel.GetTypeInfo(expression);
        if (typeInfo.Type == null || !_typeAnalyzer.IsCompileTimeEvaluatable(typeInfo.Type))
            return null;

        // Try different evaluation strategies
        return EvaluateMemberAccess(expression)
            ?? EvaluateMethodCall(expression)
            ?? EvaluateBinaryExpression(expression)
            ?? EvaluateConversion(expression);
    }

    private string? EvaluateMemberAccess(ExpressionSyntax expression)
    {
        if (expression is not MemberAccessExpressionSyntax memberAccess)
            return null;

        var symbol = _semanticModel.GetSymbolInfo(memberAccess).Symbol;
        
        // Handle static readonly fields: TW.Display.Flex
        if (symbol is IFieldSymbol { IsReadOnly: true, IsStatic: true } fieldSymbol)
        {
            return ExtractFieldValue(fieldSymbol);
        }

        // Handle properties with constant getters
        if (symbol is IPropertySymbol { IsStatic: true } propSymbol)
        {
            return ExtractPropertyValue(propSymbol);
        }

        return null;
    }

    private string? ExtractFieldValue(IFieldSymbol fieldSymbol)
    {
        // Get field declaration
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

    private string? EvaluateMethodCall(ExpressionSyntax expression)
    {
        if (expression is not InvocationExpressionSyntax invocation)
            return null;

        var symbol = _semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        if (symbol == null || !symbol.IsStatic) return null;

        // Evaluate all arguments
        var argValues = new List<object>();
        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            var constValue = _semanticModel.GetConstantValue(arg.Expression);
            if (!constValue.HasValue) return null;  // Cannot evaluate
            
            argValues.Add(constValue.Value);
        }

        // Special handling for common patterns
        return EvaluateKnownMethod(symbol, argValues);
    }

    private string? EvaluateKnownMethod(IMethodSymbol method, List<object> args)
    {
        // Pattern: public static TailwindClass P(int size) => new($"p-{size}");
        if (method.Name == "P" && args.Count == 1 && args[0] is int size)
        {
            return $"p-{size}";
        }

        // Pattern: public static TailwindClass Gap(int size) => new($"gap-{size}");
        if (method.Name == "Gap" && args.Count == 1 && args[0] is int gapSize)
        {
            return $"gap-{gapSize}";
        }

        // Generic pattern: Get method body and evaluate
        return EvaluateMethodBody(method, args);
    }

    private string? EvaluateMethodBody(IMethodSymbol method, List<object> args)
    {
        // Get method syntax
        var syntaxRef = method.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null) return null;

        var methodDecl = syntaxRef.GetSyntax() as MethodDeclarationSyntax;
        if (methodDecl == null) return null;

        // For expression-bodied methods: => new($"...")
        if (methodDecl.ExpressionBody != null)
        {
            return EvaluateWithArguments(methodDecl.ExpressionBody.Expression, method, args);
        }

        // For block bodies: { return new($"..."); }
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

            // Simulate TailwindClass.operator+
            if (string.IsNullOrEmpty(leftValue)) return rightValue;
            if (string.IsNullOrEmpty(rightValue)) return leftValue;
            
            return $"{leftValue} {rightValue}";
        }

        return null;
    }

    private string? EvaluateConversion(ExpressionSyntax expression)
    {
        // Handle object creation: new TailwindClass("flex")
        if (expression is ObjectCreationExpressionSyntax creation)
        {
            if (creation.ArgumentList?.Arguments.Count == 1)
            {
                var arg = creation.ArgumentList.Arguments[0].Expression;
                var constValue = _semanticModel.GetConstantValue(arg);
                
                if (constValue.HasValue && constValue.Value is string str)
                {
                    return str;
                }
            }
        }

        // Handle implicit string conversion
        var typeInfo = _semanticModel.GetTypeInfo(expression);
        if (typeInfo.ConvertedType?.SpecialType == SpecialType.System_String)
        {
            // The expression will be implicitly converted to string
            return TryEvaluate(expression);
        }

        return null;
    }
}
```

## Step 3: Integrate into Code Generator

**File:** `src/eQuantic.UI.Compiler/CodeGen/TypeScriptEmitter.cs`

```csharp
public class TypeScriptEmitter
{
    private readonly ExpressionEvaluator _evaluator;
    private readonly TypeAnalyzer _typeAnalyzer;

    public void EmitPropertyValue(string propertyName, ExpressionSyntax valueExpression)
    {
        var typeInfo = _semanticModel.GetTypeInfo(valueExpression);
        
        // Check if type is marked with [CompileTimeEvaluate]
        if (typeInfo.Type != null && _typeAnalyzer.IsCompileTimeEvaluatable(typeInfo.Type))
        {
            var config = _typeAnalyzer.GetEvaluationConfig(typeInfo.Type);
            var evaluatedValue = _evaluator.TryEvaluate(valueExpression);

            if (evaluatedValue != null)
            {
                // Success: emit constant string
                _writer.Write($"{ToCamelCase(propertyName)}: \"{evaluatedValue}\"");
                return;
            }

            // Evaluation failed - handle according to configuration
            HandleEvaluationFailure(propertyName, valueExpression, config);
            return;
        }

        // Not compile-time evaluatable - emit normal code
        EmitExpression(valueExpression);
    }

    private void HandleEvaluationFailure(string propertyName, ExpressionSyntax expr, CompileTimeEvaluateConfig config)
    {
        switch (config.FallbackBehavior)
        {
            case "EmitRuntimeCode":
                // Default: transpile to JavaScript
                EmitExpression(expr);
                break;

            case "Error":
                // Strict mode: fail compilation
                throw new CompilationException(
                    $"Cannot evaluate compile-time expression for property '{propertyName}' at {expr.GetLocation()}");

            case "EmitNull":
                // Emit empty string
                _writer.Write($"{ToCamelCase(propertyName)}: \"\"");
                break;

            default:
                if (config.WarnOnFailure)
                {
                    _diagnostics.ReportWarning(
                        $"Could not evaluate compile-time expression for '{propertyName}', falling back to runtime code",
                        expr.GetLocation());
                }
                EmitExpression(expr);
                break;
        }
    }
}
```

## Step 4: Add Unit Tests

**File:** `tests/eQuantic.UI.Compiler.Tests/CompileTimeEvaluationTests.cs`

```csharp
public class CompileTimeEvaluationTests
{
    [Fact]
    public void StaticField_EmitsConstantString()
    {
        var code = @"
            public class TestComponent : StatefulComponent
            {
                public override IComponent Build(RenderContext context)
                {
                    return new Box { ClassName = TW.Display.Flex };
                }
            }
        ";

        var output = CompileToTypeScript(code);

        Assert.Contains("className: \"flex\"", output);
        Assert.DoesNotContain("import { TW }", output);
    }

    [Fact]
    public void MethodCall_WithConstantArg_EmitsConstantString()
    {
        var code = @"
            return new Box { ClassName = TW.P(6) };
        ";

        var output = CompileToTypeScript(code);

        Assert.Contains("className: \"p-6\"", output);
    }

    [Fact]
    public void BinaryExpression_EmitsConstantString()
    {
        var code = @"
            return new Box { ClassName = TW.Display.Flex + TW.Gap(4) + TW.P(6) };
        ";

        var output = CompileToTypeScript(code);

        Assert.Contains("className: \"flex gap-4 p-6\"", output);
    }

    [Fact]
    public void NestedClass_EmitsConstantString()
    {
        var code = @"
            return new Box { ClassName = TW.Display.Flex };
        ";

        var output = CompileToTypeScript(code);

        Assert.Contains("className: \"flex\"", output);
    }

    [Fact]
    public void HelperMethod_EmitsConstantString()
    {
        var code = @"
            return new Box { ClassName = TW.Dark(TW.Bg.Zinc900) };
        ";

        var output = CompileToTypeScript(code);

        Assert.Contains("className: \"dark:bg-zinc-900\"", output);
    }

    [Fact]
    public void RuntimeValue_FallsBackToRuntimeCode()
    {
        var code = @"
            int size = GetSize();
            return new Box { ClassName = TW.P(size) };
        ";

        var output = CompileToTypeScript(code);

        // Should emit runtime code, not constant
        Assert.Contains("TW.P(size)", output);
    }

    [Theory]
    [InlineData("Error", typeof(CompilationException))]
    [InlineData("EmitNull", "className: \"\"")]
    public void FallbackBehavior_WorksCorrectly(string behavior, object expected)
    {
        // Test different fallback behaviors
    }
}
```

## Step 5: Integration Checklist

- [ ] Add `TypeAnalyzer.IsCompileTimeEvaluatable()` method
- [ ] Create `ExpressionEvaluator` class
- [ ] Implement evaluation for:
  - [ ] Static readonly fields
  - [ ] Static method calls with constant args
  - [ ] Binary expressions (+, etc.)
  - [ ] Nested class access (TW.Display.Flex)
  - [ ] Helper methods (Dark, Hover, etc.)
- [ ] Integrate into `TypeScriptEmitter`
- [ ] Add fallback behavior handling
- [ ] Add diagnostics/warnings
- [ ] Write comprehensive unit tests
- [ ] Update compiler documentation

## Performance Considerations

### Caching

Cache evaluation results to avoid re-evaluating the same expressions:

```csharp
private readonly Dictionary<string, string> _evaluationCache = new();

public string? TryEvaluate(ExpressionSyntax expression)
{
    var key = expression.ToString();
    if (_evaluationCache.TryGetValue(key, out var cached))
        return cached;

    var result = EvaluateInternal(expression);
    _evaluationCache[key] = result;
    return result;
}
```

### Lazy Evaluation

Only evaluate when needed (property assignments):

```csharp
if (propertyName != "ClassName" && propertyName != "Class")
{
    // Skip compile-time evaluation for non-class properties
    EmitExpression(valueExpression);
    return;
}
```

## Debugging Tips

### Enable Verbose Logging

```csharp
if (_options.VerboseCompileTimeEvaluation)
{
    Console.WriteLine($"[CompileTime] Evaluating: {expression}");
    Console.WriteLine($"[CompileTime] Result: {result ?? "FAILED"}");
}
```

### Source Map Integration

Ensure source maps still point to original C# code:

```csharp
_sourceMapGenerator.AddMapping(
    generatedLine: _writer.CurrentLine,
    generatedColumn: _writer.CurrentColumn,
    originalFile: expression.SyntaxTree.FilePath,
    originalLine: expression.GetLocation().GetLineSpan().StartLinePosition.Line,
    originalColumn: expression.GetLocation().GetLineSpan().StartLinePosition.Character
);
```

## Common Pitfalls

### 1. Forgetting String Conversion

```csharp
// ❌ Wrong: Returns TailwindClass, not string
var result = TryEvaluate(expression);

// ✅ Correct: Extract string value
var tailwindClass = TryEvaluate(expression);
var stringValue = tailwindClass.ToString();  // or use implicit conversion
```

### 2. Not Handling Nested Expressions

```csharp
// Expression: TW.Dark(TW.Bg.Zinc900)
// Must evaluate inner expression first: TW.Bg.Zinc900 → "bg-zinc-900"
// Then evaluate outer: TW.Dark("bg-zinc-900") → "dark:bg-zinc-900"
```

### 3. Missing Type Checks

```csharp
// Always check if type is marked before evaluating
if (!_typeAnalyzer.IsCompileTimeEvaluatable(typeInfo.Type))
{
    EmitExpression(expression);
    return;
}
```

## Summary

This implementation allows the compiler to evaluate CSS class expressions at compile-time for any framework marked with `[CompileTimeEvaluate]`, without framework-specific hardcoding.

**Key Points:**
- Generic, extensible solution
- Works with any CSS framework (Tailwind, Bootstrap, Material, custom)
- Transparent fallback to runtime code when needed
- Configurable behavior
- Well-tested and maintainable
