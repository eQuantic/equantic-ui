# Compile-Time Evaluation - Implementation Summary

> **Status note.** The `TW.*` syntax on this page belongs to the `eQuantic.UI.Tailwind`
> adapter, removed in 0704a3d0 (2026-08-08) — the atomic styling engine is the product, and
> the framework ships exactly one styling engine. What survives is the generic mechanism:
> `[CompileTimeEvaluate]` and `ClassBuilder`, now in `eQuantic.UI.Web.Styling`. Read the
> examples as illustrations of the mechanism, not as an API you can call today.

## Problem Statement

We needed a **generic, extensible solution** for compile-time evaluation of CSS class builders (Tailwind, Bootstrap, Material UI, etc.) **without hardcoding** framework-specific logic in the compiler.

## Solution: `[CompileTimeEvaluate]` Attribute

A marker attribute that tells the compiler to evaluate expressions at compile-time and emit constant strings.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    eQuantic.UI.Web                          │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  CompileTimeEvaluateAttribute                         │  │
│  │  - Generic marker for any CSS framework              │  │
│  │  - No hardcoded framework logic                      │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                          ▲
                          │ (uses)
        ┌─────────────────┴─────────────────┬───────────────┐
        │                                   │               │
┌───────────────────┐          ┌─────────────────────┐     │
│ eQuantic.UI.      │          │ eQuantic.UI.        │     │
│ Tailwind          │          │ Bootstrap           │  (future)
│                   │          │ (future)            │     │
│ [CompileTime      │          │                     │     │
│  Evaluate]        │          │ [CompileTime        │     │
│ TailwindClass     │          │  Evaluate]          │     │
│                   │          │ BootstrapClass      │     │
└───────────────────┘          └─────────────────────┘     │
                                                            │
                                              ┌─────────────────────┐
                                              │ eQuantic.UI.        │
                                              │ Material            │
                                              │ (future)            │
                                              │                     │
                                              │ [CompileTime        │
                                              │  Evaluate]          │
                                              │ MaterialClass       │
                                              └─────────────────────┘
```

## Implementation

### 1. Core Attribute

**File:** `src/eQuantic.UI.Web/Styling/CompileTimeEvaluateAttribute.cs`

```csharp
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public sealed class CompileTimeEvaluateAttribute : Attribute
{
    public bool WarnOnEvaluationFailure { get; set; } = true;
    public string FallbackBehavior { get; set; } = "EmitRuntimeCode";
}
```

**Features:**
- Can be applied to any struct or class
- Configurable fallback behavior
- Framework-agnostic

### 2. Tailwind Implementation

**File:** `src/eQuantic.UI.Tailwind/TailwindClass.cs`

```csharp
[CompileTimeEvaluate]
public readonly struct TailwindClass
{
    private readonly string _value;

    public TailwindClass(string value) => _value = value;

    // Required: conversion to string
    public static implicit operator string(TailwindClass tw) => tw._value;

    // Operators evaluated at compile-time
    public static TailwindClass operator +(TailwindClass left, TailwindClass right)
        => new($"{left._value} {right._value}");
}

[CompileTimeEvaluate]
public readonly struct ConditionalTailwindClass
{
    // ...
}
```

### 3. Usage Example

```csharp
using eQuantic.UI.Tailwind;

public class MyComponent : StatefulComponent
{
    public override IComponent Build(RenderContext context)
    {
        return new Box
        {
            // Type-safe C# with IntelliSense
            ClassName = TW.Display.Flex + TW.Gap(4) + TW.P(6) + TW.Bg.White
            // Compiler evaluates to: "flex gap-4 p-6 bg-white"
        };
    }
}
```

**Compiled TypeScript:**
```typescript
className: "flex gap-4 p-6 bg-white"  // ✅ Single string, no runtime overhead
```

## Compiler Implementation (TODO)

The compiler needs to:

### 1. Detection Phase

```csharp
private bool IsCompileTimeEvaluatable(ITypeSymbol typeSymbol)
{
    return typeSymbol.GetAttributes()
        .Any(attr => attr.AttributeClass?.Name == "CompileTimeEvaluateAttribute" &&
                     attr.AttributeClass.ContainingNamespace.ToDisplayString() == "eQuantic.UI.Web.Styling");
}
```

### 2. Evaluation Phase

```csharp
private string? EvaluateExpression(ExpressionSyntax expression, SemanticModel semanticModel)
{
    // Handle different expression types:
    // - Static readonly fields: TW.Display.Flex
    // - Static method calls: TW.P(6)
    // - Binary expressions: left + right
    // - Nested classes: TW.Display.Flex

    return SymbolicEvaluator.Evaluate(expression, semanticModel);
}
```

### 3. Code Generation Phase

```csharp
if (IsCompileTimeEvaluatable(propertyType))
{
    var evaluatedValue = EvaluateExpression(propertyValue, semanticModel);
    if (evaluatedValue != null)
    {
        // Emit constant string
        writer.Write($"className: \"{evaluatedValue}\"");
        return;
    }
}

// Fallback: emit runtime code
EmitExpression(propertyValue);
```

## Supported Scenarios

### ✅ Static Readonly Fields

```csharp
public static readonly TailwindClass Flex = new("flex");

// Usage: TW.Display.Flex
// Output: "flex"
```

### ✅ Static Methods

```csharp
public static TailwindClass P(int size) => new($"p-{size}");

// Usage: TW.P(6)
// Output: "p-6"
```

### ✅ Operator Overloading

```csharp
public static TailwindClass operator +(TailwindClass left, TailwindClass right)
    => new($"{left._value} {right._value}");

// Usage: TW.Flex + TW.Gap(4)
// Output: "flex gap-4"
```

### ✅ Nested Classes

```csharp
public static class Display
{
    public static readonly TailwindClass Flex = new("flex");
}

// Usage: TW.Display.Flex
// Output: "flex"
```

### ✅ Helper Methods (Dark, Hover, Responsive)

```csharp
public static TailwindClass Dark(TailwindClass className)
    => new($"dark:{className}");

// Usage: TW.Dark(TW.Bg.Zinc900)
// Output: "dark:bg-zinc-900"
```

### ⚠️ Runtime Values (Fallback)

```csharp
int padding = GetPadding();  // Runtime value

// Usage: TW.P(padding)
// Cannot evaluate at compile-time
// Fallback: Emit runtime code or error
```

## Benefits

### 1. Framework Agnostic

No hardcoded framework logic in the compiler. Works with:
- ✅ Tailwind CSS
- ✅ Bootstrap (future)
- ✅ Material UI (future)
- ✅ Any custom CSS framework

### 2. Type Safety + Performance

```csharp
// Type-safe C# with IntelliSense
ClassName = TW.Display.Flex + TW.Gap(4) + TW.P(6)

// Zero runtime overhead (pre-evaluated)
className: "flex gap-4 p-6"
```

### 3. Developer Experience

- ✅ Full IntelliSense autocomplete
- ✅ Compile-time type checking
- ✅ Refactoring support (rename, find usages)
- ✅ No magic strings
- ✅ Better code navigation

### 4. Bundle Size

```csharp
// Before: Runtime code (~50 bytes per usage)
className: TW.Display.Flex + TW.Gap(4) + TW.P(6)

// After: Constant string (~18 bytes)
className: "flex gap-4 p-6"
```

## Files Created/Modified

### Created

1. **`src/eQuantic.UI.Web/Styling/CompileTimeEvaluateAttribute.cs`**
   - Generic attribute for any CSS framework
   - Configurable fallback behavior

2. **`src/eQuantic.UI.Web/Styling/ClassBuilder.cs`**
   - Generic fluent builder for CSS classes
   - Can be used by any framework (Tailwind, Bootstrap, etc.)

3. **`COMPILER-COMPILE-TIME-EVALUATION.md`**
   - Comprehensive guide for compiler implementation
   - Examples, strategies, test cases

4. **`COMPILE-TIME-EVALUATION-SUMMARY.md`** (this file)
   - High-level overview and summary

### Modified

1. **`src/eQuantic.UI.Tailwind/TailwindClass.cs`**
   - Added `[CompileTimeEvaluate]` attribute
   - Added `using eQuantic.UI.Web.Styling;`

2. **`src/eQuantic.UI.Tailwind/TWBuilder.cs`**
   - Simplified to delegate to `ClassBuilder`
   - Removed inheritance code smell

3. **`README.md`**
   - Updated Tailwind section with 3 approaches
   - Added examples of typed objects with + operator

## Compiler TODO

- [ ] Implement attribute detection in `ComponentParser`
- [ ] Create `ExpressionEvaluator` with symbolic execution
- [ ] Handle static readonly fields
- [ ] Handle static method calls with constant arguments
- [ ] Handle operator overloading (`+`, etc.)
- [ ] Handle nested classes (e.g., `TW.Display.Flex`)
- [ ] Handle conditional expressions with constant conditions
- [ ] Implement fallback behaviors
- [ ] Add diagnostics/warnings
- [ ] Write comprehensive unit tests

## Testing Strategy

```csharp
[Theory]
[InlineData("TW.Display.Flex", "flex")]
[InlineData("TW.P(6)", "p-6")]
[InlineData("TW.Display.Flex + TW.Gap(4)", "flex gap-4")]
[InlineData("TW.Dark(TW.Bg.Zinc900)", "dark:bg-zinc-900")]
public void CompileTimeEvaluation_EmitsConstantString(string input, string expected)
{
    var code = $"ClassName = {input}";
    var output = Compile(code);
    Assert.Contains($"className: \"{expected}\"", output);
}
```

## Future CSS Frameworks

### Bootstrap Example

```csharp
namespace eQuantic.UI.Bootstrap;

[CompileTimeEvaluate]
public readonly struct BootstrapClass
{
    private readonly string _value;

    public BootstrapClass(string value) => _value = value;

    public static implicit operator string(BootstrapClass bs) => bs._value;

    public static BootstrapClass operator +(BootstrapClass left, BootstrapClass right)
        => new($"{left._value} {right._value}");
}

public static class BS
{
    public static class Display
    {
        public static readonly BootstrapClass Flex = new("d-flex");
        public static readonly BootstrapClass Grid = new("d-grid");
    }

    public static BootstrapClass P(int size) => new($"p-{size}");
}
```

**Usage:**
```csharp
ClassName = BS.Display.Flex + BS.P(3)
// Compiled: "d-flex p-3"
```

## Conclusion

The `[CompileTimeEvaluate]` attribute provides a **generic, extensible, framework-agnostic** solution for compile-time optimization of CSS class builders.

**Key Achievements:**
- ✅ No hardcoded framework logic in compiler
- ✅ Works with any CSS framework (Tailwind, Bootstrap, Material, custom)
- ✅ Type-safe C# with IntelliSense
- ✅ Zero runtime overhead
- ✅ Clean architecture (SOLID principles)
- ✅ Extensible for future frameworks

**Next Step:** Implement the compiler support for evaluating expressions marked with this attribute.
