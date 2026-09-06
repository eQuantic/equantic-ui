# Compile-Time Evaluation in eQuantic.UI Compiler

> **Status note.** The `TW.*` syntax on this page belongs to the `eQuantic.UI.Tailwind`
> adapter, removed in 0704a3d0 (2026-08-08) — the atomic styling engine is the product, and
> the framework ships exactly one styling engine. What survives is the generic mechanism:
> `[CompileTimeEvaluate]` and `ClassBuilder`, now in `eQuantic.UI.Web.Styling`. Read the
> examples as illustrations of the mechanism, not as an API you can call today.

## Overview

The eQuantic.UI compiler supports **compile-time evaluation** of expressions involving types marked with the `[CompileTimeEvaluate]` attribute. This allows CSS class builders and similar constructs to be evaluated during compilation, producing optimized JavaScript output.

## The Problem

**Before Compile-Time Evaluation:**

```csharp
// C# Component
ClassName = TW.Display.Flex + TW.Gap(4) + TW.P(6)

// Generated TypeScript (INCORRECT)
import { TW } from "./TW";  // ❌ File doesn't exist
className: TW.Display.Flex + TW.Gap(4) + TW.P(6)  // ❌ Runtime overhead
```

**After Compile-Time Evaluation:**

```csharp
// C# Component (same code)
ClassName = TW.Display.Flex + TW.Gap(4) + TW.P(6)

// Generated TypeScript (CORRECT)
className: "flex gap-4 p-6"  // ✅ Pre-evaluated at compile time
```

## How It Works

### 1. Mark Type with Attribute

```csharp
using eQuantic.UI.Web.Styling;

[CompileTimeEvaluate]
public readonly struct TailwindClass
{
    private readonly string _value;

    public TailwindClass(string value) => _value = value;

    // Must have conversion to string
    public static implicit operator string(TailwindClass tw) => tw._value;

    // Operators are evaluated at compile-time
    public static TailwindClass operator +(TailwindClass left, TailwindClass right)
        => new($"{left._value} {right._value}");
}
```

### 2. Compiler Detection

The compiler should:

1. **Detect the attribute** when parsing component classes
2. **Identify expressions** that return types marked with `[CompileTimeEvaluate]`
3. **Evaluate the expression** using Roslyn's semantic model
4. **Extract the string value** via the implicit/explicit `string` conversion
5. **Emit the constant string** instead of transpiling the expression

### 3. Implementation Strategy

#### Phase 1: Detection

```csharp
// In ComponentParser or PropertyAnalyzer
private bool IsCompileTimeEvaluatable(ITypeSymbol typeSymbol)
{
    return typeSymbol.GetAttributes()
        .Any(attr => attr.AttributeClass?.Name == "CompileTimeEvaluateAttribute" &&
                     attr.AttributeClass.ContainingNamespace.ToDisplayString() == "eQuantic.UI.Web.Styling");
}
```

#### Phase 2: Evaluation

```csharp
private string? EvaluateExpression(ExpressionSyntax expression, SemanticModel semanticModel)
{
    // Use Roslyn's constant value evaluation
    var constantValue = semanticModel.GetConstantValue(expression);
    if (constantValue.HasValue && constantValue.Value is string str)
    {
        return str;
    }

    // For more complex expressions, use symbolic execution
    // This is where TW.Display.Flex + TW.Gap(4) would be evaluated
    return TrySymbolicEvaluation(expression, semanticModel);
}

private string? TrySymbolicEvaluation(ExpressionSyntax expression, SemanticModel semanticModel)
{
    var typeInfo = semanticModel.GetTypeInfo(expression);
    if (typeInfo.Type == null || !IsCompileTimeEvaluatable(typeInfo.Type))
        return null;

    // For member access: TW.Display.Flex
    if (expression is MemberAccessExpressionSyntax memberAccess)
    {
        var symbol = semanticModel.GetSymbolInfo(memberAccess).Symbol;
        if (symbol is IFieldSymbol fieldSymbol && fieldSymbol.IsReadOnly && fieldSymbol.IsStatic)
        {
            // Get the constant initializer value
            return GetFieldInitializerValue(fieldSymbol);
        }
    }

    // For binary expressions: left + right
    if (expression is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression))
    {
        var leftValue = TrySymbolicEvaluation(binary.Left, semanticModel);
        var rightValue = TrySymbolicEvaluation(binary.Right, semanticModel);

        if (leftValue != null && rightValue != null)
        {
            // Simulate the + operator behavior
            if (string.IsNullOrEmpty(leftValue)) return rightValue;
            if (string.IsNullOrEmpty(rightValue)) return leftValue;
            return $"{leftValue} {rightValue}";
        }
    }

    // For method calls: TW.Gap(4)
    if (expression is InvocationExpressionSyntax invocation)
    {
        return TryEvaluateMethodCall(invocation, semanticModel);
    }

    return null;
}
```

#### Phase 3: Code Generation

```csharp
// In TypeScript emitter
if (IsCompileTimeEvaluatable(propertyType))
{
    var evaluatedValue = EvaluateExpression(propertyValue, semanticModel);
    if (evaluatedValue != null)
    {
        // Emit as string literal
        writer.Write($"className: \"{evaluatedValue}\"");
        return;
    }
}

// Fallback: emit runtime code
EmitExpression(propertyValue);
```

## Supported Scenarios

### Static Readonly Fields

```csharp
public static class TW
{
    public static readonly TailwindClass Empty = new("");
    public static readonly TailwindClass Flex = new("flex");
}

// Usage
ClassName = TW.Flex
// Output: className: "flex"
```

### Static Methods

```csharp
public static TailwindClass P(int size) => new($"p-{size}");

// Usage
ClassName = TW.P(6)
// Output: className: "p-6"
```

### Operator Overloading

```csharp
public static TailwindClass operator +(TailwindClass left, TailwindClass right)
    => new($"{left._value} {right._value}");

// Usage
ClassName = TW.Flex + TW.Gap(4) + TW.P(6)
// Output: className: "flex gap-4 p-6"
```

### Nested Classes

```csharp
public static class Display
{
    public static readonly TailwindClass Flex = new("flex");
    public static readonly TailwindClass Grid = new("grid");
}

// Usage
ClassName = TW.Display.Flex
// Output: className: "flex"
```

### Conditional Expressions

```csharp
[CompileTimeEvaluate]
public readonly struct ConditionalTailwindClass
{
    private readonly bool _condition;
    private readonly string _value;

    public static implicit operator TailwindClass(ConditionalTailwindClass conditional)
        => new(conditional._condition ? conditional._value : "");
}

// Usage
ClassName = TW.When(isActive, TW.Bg.Blue600)
// If isActive is a constant: className: "bg-blue-600" or className: ""
// If isActive is runtime: Fall back to conditional logic
```

## Limitations

### Runtime Values Cannot Be Evaluated

```csharp
// ❌ Cannot evaluate at compile-time (count is runtime value)
ClassName = TW.P(count)

// Compiler should emit warning and fall back to runtime code
// Output: className: TW.P(count)
```

### External Dependencies

```csharp
// ❌ Cannot evaluate if depends on external state
private int _padding = 6;
ClassName = TW.P(_padding)

// Fallback to runtime
```

### Complex Control Flow

```csharp
// ❌ Cannot evaluate complex logic
ClassName = todos.Any() ? TW.Flex : TW.Grid

// Fallback to runtime ternary
```

## Fallback Behavior

The `[CompileTimeEvaluate]` attribute supports configuration:

```csharp
[CompileTimeEvaluate(FallbackBehavior = "EmitRuntimeCode")]  // Default
public readonly struct TailwindClass { }

[CompileTimeEvaluate(FallbackBehavior = "Error")]  // Strict mode
public readonly struct BootstrapClass { }

[CompileTimeEvaluate(FallbackBehavior = "EmitNull")]  // Emit empty string
public readonly struct MaterialClass { }
```

**Behaviors:**
- `EmitRuntimeCode`: Transpile the expression to JavaScript (default, safe)
- `Error`: Fail compilation with error message
- `EmitNull`: Emit `className: ""` or `className: null`

## Benefits

### 1. Zero Runtime Overhead

```csharp
// C#
ClassName = TW.Display.Flex + TW.Gap(4) + TW.P(6) + TW.Bg.White

// TypeScript (optimized)
className: "flex gap-4 p-6 bg-white"  // Single string, no computation
```

### 2. Framework Agnostic

```csharp
// Tailwind
[CompileTimeEvaluate]
public readonly struct TailwindClass { }

// Bootstrap
[CompileTimeEvaluate]
public readonly struct BootstrapClass { }

// Material UI
[CompileTimeEvaluate]
public readonly struct MaterialClass { }

// Custom CSS Framework
[CompileTimeEvaluate]
public readonly struct MyCustomClass { }
```

All work the same way - **no compiler changes needed** for new frameworks!

### 3. Type Safety + Performance

```csharp
// Type-safe C# with IntelliSense
ClassName = TW.Display.Flex + TW.Gap(4)

// Optimized runtime JavaScript
className: "flex gap-4"
```

### 4. Better Developer Experience

- ✅ IntelliSense autocomplete in C#
- ✅ Compile-time type checking
- ✅ Refactoring support (rename, find usages)
- ✅ Zero runtime overhead
- ✅ Smaller JavaScript bundles

## Implementation Checklist

- [ ] Add attribute detection to `ComponentParser`
- [ ] Implement `IsCompileTimeEvaluatable(ITypeSymbol)` check
- [ ] Create `ExpressionEvaluator` class with symbolic execution
- [ ] Handle static readonly fields
- [ ] Handle static method calls
- [ ] Handle operator overloading (`+`, etc.)
- [ ] Handle nested classes (e.g., `TW.Display.Flex`)
- [ ] Handle conditional expressions with constant conditions
- [ ] Implement fallback behavior configuration
- [ ] Add warning/error diagnostics
- [ ] Write unit tests for all scenarios
- [ ] Document in compiler README

## Testing Strategy

### Unit Tests

```csharp
[Fact]
public void CompileTimeEvaluation_StaticField_EmitsConstantString()
{
    var code = @"
        ClassName = TW.Display.Flex
    ";

    var output = Compile(code);

    Assert.Equal("className: \"flex\"", output);
}

[Fact]
public void CompileTimeEvaluation_MethodCall_EmitsConstantString()
{
    var code = @"
        ClassName = TW.P(6)
    ";

    var output = Compile(code);

    Assert.Equal("className: \"p-6\"", output);
}

[Fact]
public void CompileTimeEvaluation_OperatorOverload_EmitsConstantString()
{
    var code = @"
        ClassName = TW.Display.Flex + TW.Gap(4) + TW.P(6)
    ";

    var output = Compile(code);

    Assert.Equal("className: \"flex gap-4 p-6\"", output);
}
```

## Examples from Real Projects

### The Tailwind sample, as it was written

```csharp
// Before (string - no type safety)
ClassName = "min-h-screen bg-gradient-to-br from-blue-50 p-8 grid place-items-center"

// After (typed - with compile-time evaluation)
ClassName = TW.Size.MinHScreen + TW.Bg.GradientToBr + TW.From.Blue50 +
            TW.P(8) + TW.Display.Grid + TW.Grid.PlaceItemsCenter

// Compiled TypeScript
className: "min-h-screen bg-gradient-to-br from-blue-50 p-8 grid place-items-center"
```

### Bootstrap Example (Future)

```csharp
using eQuantic.UI.Bootstrap;

ClassName = BS.Display.Flex + BS.JustifyContent.Between + BS.AlignItems.Center + BS.P(3)

// Compiled
className: "d-flex justify-content-between align-items-center p-3"
```

### Material UI Example (Future)

```csharp
using eQuantic.UI.Material;

ClassName = MUI.Display.Flex + MUI.Spacing.P(2) + MUI.Color.Primary

// Compiled
className: "MuiBox-root css-flex css-p-2 css-primary"
```

## Conclusion

The `[CompileTimeEvaluate]` attribute provides a **generic, extensible mechanism** for compile-time optimization of CSS class builders and similar constructs, without requiring framework-specific hardcoding in the compiler.

This enables:
- Type-safe CSS class generation in C#
- Zero runtime overhead
- Framework-agnostic compiler implementation
- Better developer experience with IntelliSense and refactoring support
