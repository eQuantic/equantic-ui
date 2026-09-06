# Compile-Time Evaluation - Documentation Index

## Overview

This directory contains comprehensive documentation for implementing **compile-time evaluation** of CSS class builders in the eQuantic.UI compiler. This feature enables type-safe, framework-agnostic CSS class generation with zero runtime overhead.

## Quick Navigation

### For Framework Users

- **[README.md](README.md)** - Main project documentation with Tailwind examples
- **TYPED-EXAMPLES.md** - shipped with the Tailwind adapter and removed with it (see *Tailwind Implementation* below)

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
| `src/eQuantic.UI.Web/Styling/CompileTimeEvaluateAttribute.cs` | Generic attribute for marking types |
| `src/eQuantic.UI.Web/Styling/ClassBuilder.cs` | Generic CSS class builder (framework-agnostic) |

Both lived in `eQuantic.UI.Core` until that assembly was dissolved into the ones that owned its parts
(#83); the DOM-side styling helpers went to **Web**.

### Tailwind Implementation (removed)

The `eQuantic.UI.Tailwind` project — `TailwindClass`, `TWTyped`, `TWBuilder` and `TYPED-EXAMPLES.md` —
was removed in 0704a3d0 (2026-08-08): the atomic styling engine is the product, and the framework
ships exactly one styling engine. The `TW.*` examples on this page describe that removed adapter; the
generic pieces (`ClassBuilder`, `[CompileTimeEvaluate]`) are what remains.

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
- [ ] End-to-end testing with the web sample (`samples/DefaultUIDashboard`)

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

The samples build against the framework PROJECTS — the SDK's `Sdk.props` switches to project
references beside a source tree — so there is no pack, no local feed and no cache step between an
edit and the sample:

1. Build and run the web sample:
   ```bash
   dotnet build samples/DefaultUIDashboard
   dotnet run --project samples/DefaultUIDashboard
   ```

2. Read the emitted JavaScript under `samples/DefaultUIDashboard/wwwroot/_equantic/`.

To validate the same change through the real consumer path (packages, `global.json`, restore), use
the local-feed recipe on the wiki's [BuildFlow](https://github.com/equantic/equantic-ui/wiki/BuildFlow)
page.

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

- **For usage questions:** `TYPED-EXAMPLES.md` went with the Tailwind adapter; the `ClassBuilder` tests are the living examples
- **For architecture questions:** Read `COMPILE-TIME-EVALUATION-SUMMARY.md`
- **For implementation questions:** Check `COMPILER-IMPLEMENTATION-GUIDE.md`
- **For detailed specs:** Consult `COMPILER-COMPILE-TIME-EVALUATION.md`

## Version History

- **v0.1.2**
  - Added `[CompileTimeEvaluate]` attribute
  - Implemented `TailwindClass` with typed objects
  - Created `ClassBuilder` in Core
  - Comprehensive documentation
  - Awaiting compiler implementation
- **Since then**
  - The Tailwind adapter was removed (0704a3d0, 2026-08-08)
  - `ClassBuilder` and `[CompileTimeEvaluate]` moved to `eQuantic.UI.Web` when Core was dissolved (#83)

---

**Next Step:** Implement compile-time evaluation support in the eQuantic.UI compiler (eqc).
