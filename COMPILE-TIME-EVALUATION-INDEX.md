# Compile-Time Evaluation - Documentation Index

## Overview

This directory contains comprehensive documentation for implementing **compile-time evaluation** of CSS class builders in the eQuantic.UI compiler. This feature enables type-safe, framework-agnostic CSS class generation with zero runtime overhead.

## Quick Navigation

### For Framework Users

- **[README.md](README.md)** - Main project documentation with Tailwind examples
- **[TYPED-EXAMPLES.md](src/eQuantic.UI.Tailwind/TYPED-EXAMPLES.md)** - Real-world examples using typed objects with + operator

### For Compiler Developers

1. **[COMPILE-TIME-EVALUATION-SUMMARY.md](COMPILE-TIME-EVALUATION-SUMMARY.md)** ⭐ **START HERE**
   - High-level overview
   - Problem statement
   - Architecture diagram
   - Implementation summary
   - Benefits and use cases

2. **[COMPILER-COMPILE-TIME-EVALUATION.md](COMPILER-COMPILE-TIME-EVALUATION.md)**
   - Detailed specification
   - Supported scenarios with examples
   - Evaluation strategies
   - Limitations and edge cases
   - Testing strategy

3. **[COMPILER-IMPLEMENTATION-GUIDE.md](COMPILER-IMPLEMENTATION-GUIDE.md)**
   - Step-by-step implementation guide
   - Code examples for each step
   - Integration checklist
   - Performance considerations
   - Debugging tips
   - Common pitfalls

## Key Files

### Core Infrastructure

| File | Description |
|------|-------------|
| `src/eQuantic.UI.Core/Styling/CompileTimeEvaluateAttribute.cs` | Generic attribute for marking types |
| `src/eQuantic.UI.Core/Styling/ClassBuilder.cs` | Generic CSS class builder (framework-agnostic) |

### Tailwind Implementation

| File | Description |
|------|-------------|
| `src/eQuantic.UI.Tailwind/TailwindClass.cs` | Typed value object with `[CompileTimeEvaluate]` |
| `src/eQuantic.UI.Tailwind/TWTyped.cs` | All Tailwind utilities as typed objects |
| `src/eQuantic.UI.Tailwind/TWBuilder.cs` | Fluent builder (delegates to ClassBuilder) |
| `src/eQuantic.UI.Tailwind/TYPED-EXAMPLES.md` | Real-world usage examples |

## Implementation Status

### ✅ Completed

- [x] `[CompileTimeEvaluate]` attribute in Core
- [x] `TailwindClass` struct with implicit operators
- [x] Complete typed object implementation (Display, Flex, Bg, Text, etc.)
- [x] Helper methods (Dark, Hover, Responsive, etc.)
- [x] `ClassBuilder` in Core (framework-agnostic)
- [x] Simplified `TWBuilder` (no inheritance code smell)
- [x] Comprehensive documentation
- [x] Examples and use cases

### ⏳ Pending (Compiler Work)

- [ ] Attribute detection in compiler
- [ ] Expression evaluator implementation
- [ ] Integration into code generator
- [ ] Unit tests for compiler
- [ ] End-to-end testing with TodoListApp

## Quick Example

### Before (String-based)

```csharp
ClassName = "flex items-center gap-4 p-6 bg-white rounded-lg shadow-md hover:bg-gray-100 dark:bg-zinc-900"
```

**Problems:**
- ❌ No IntelliSense
- ❌ Typos caught at runtime (or never)
- ❌ No refactoring support
- ❌ Hard to find usages

### After (Typed Objects - Pending Compiler Support)

```csharp
using eQuantic.UI.Tailwind;

ClassName = TW.Display.Flex + TW.Flex.ItemsCenter + TW.Gap(4) + TW.P(6) +
            TW.Bg.White + TW.Rounded.Lg + TW.Shadow.Md +
            TW.Hover(TW.Bg.Gray100) +
            TW.Dark(TW.Bg.Zinc900)
```

**Benefits:**
- ✅ Full IntelliSense autocomplete
- ✅ Compile-time type checking
- ✅ Refactoring support (rename, find usages)
- ✅ Zero runtime overhead (evaluated at compile-time to: `"flex items-center gap-4 p-6 bg-white rounded-lg shadow-md hover:bg-gray-100 dark:bg-zinc-900"`)

### Current Workaround (TWBuilder)

```csharp
ClassName = TW.Build()
    .Add(TW.Display.Flex, TW.Flex.ItemsCenter, TW.Gap(4), TW.P(6))
    .Add(TW.Bg.White, TW.Rounded.Lg, TW.Shadow.Md)
    .Hover(TW.Bg.Gray100)
    .Dark(TW.Bg.Zinc900)
    .Build()
```

Works today but less elegant than the + operator approach.

## Framework Extensibility

The solution is **framework-agnostic**. Future CSS frameworks can be added without compiler changes:

### Bootstrap (Future)

```csharp
[CompileTimeEvaluate]
public readonly struct BootstrapClass { /* ... */ }

public static class BS
{
    public static class Display
    {
        public static readonly BootstrapClass Flex = new("d-flex");
    }
}

// Usage
ClassName = BS.Display.Flex + BS.P(3)
// Compiled: "d-flex p-3"
```

### Material UI (Future)

```csharp
[CompileTimeEvaluate]
public readonly struct MaterialClass { /* ... */ }

public static class MUI
{
    public static readonly MaterialClass Flex = new("MuiBox-flex");
}

// Usage
ClassName = MUI.Flex + MUI.P(2)
// Compiled: "MuiBox-flex MuiBox-p-2"
```

## Testing

### Manual Testing (Current)

1. Build Core and Tailwind packages:
   ```bash
   dotnet pack src/eQuantic.UI.Core -c Release -o artifacts/packages
   dotnet pack src/eQuantic.UI.Tailwind -c Release -o artifacts/packages
   ```

2. Clear cache and restore:
   ```bash
   dotnet msbuild samples/TodoListApp -t:ClearEQuanticCache
   dotnet restore samples/TodoListApp --force-evaluate
   ```

3. Build and run:
   ```bash
   dotnet build samples/TodoListApp
   dotnet run --project samples/TodoListApp
   ```

### Automated Testing (Pending)

See `COMPILER-IMPLEMENTATION-GUIDE.md` for comprehensive unit test examples.

## Contributing

When implementing compile-time evaluation in the compiler:

1. Read `COMPILE-TIME-EVALUATION-SUMMARY.md` first for context
2. Follow the step-by-step guide in `COMPILER-IMPLEMENTATION-GUIDE.md`
3. Refer to `COMPILER-COMPILE-TIME-EVALUATION.md` for detailed specifications
4. Add tests following the examples in the implementation guide
5. Update this index if adding new documentation

## Questions?

- **For usage questions:** See examples in `TYPED-EXAMPLES.md`
- **For architecture questions:** Read `COMPILE-TIME-EVALUATION-SUMMARY.md`
- **For implementation questions:** Check `COMPILER-IMPLEMENTATION-GUIDE.md`
- **For detailed specs:** Consult `COMPILER-COMPILE-TIME-EVALUATION.md`

## Version History

- **v0.1.2** (Current)
  - Added `[CompileTimeEvaluate]` attribute
  - Implemented `TailwindClass` with typed objects
  - Created `ClassBuilder` in Core
  - Comprehensive documentation
  - Awaiting compiler implementation

---

**Next Step:** Implement compile-time evaluation support in the eQuantic.UI compiler (eqc).
