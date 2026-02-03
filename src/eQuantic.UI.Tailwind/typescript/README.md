# eQuantic.UI Tailwind TypeScript Runtime

This package provides runtime TypeScript helpers for Tailwind CSS classes when compile-time evaluation is not possible.

## Architecture

Following eQuantic.UI's self-contained package architecture:

```
eQuantic.UI.Tailwind/
├── TWTyped.cs                    # C# compile-time helpers (TW.*)
├── TailwindClass.cs              # C# TailwindClass struct with [CompileTimeEvaluate]
├── typescript/                   # TypeScript runtime (packaged separately)
│   ├── src/index.ts              # Runtime implementations
│   ├── dist/index.js             # Compiled output
│   └── package.json
└── eQuantic.UI.Tailwind.csproj   # Packages dist/index.js as tools/runtime/tailwind.js
```

**Package Flow:**
1. TypeScript compiled to `typescript/dist/index.js`
2. NuGet package includes it at `tools/runtime/tailwind.js`
3. SDK copies it to `wwwroot/_equantic/tailwind.js`
4. HTML shell imports it via `@equantic/tailwind` module

## Available Runtime Helpers

### `TW.When(condition, whenTrue, whenFalse?)`

Conditionally applies Tailwind classes based on a boolean condition.

```csharp
// C# (when condition is runtime variable)
var className = $"{TW.When(todo.IsCompleted, 'line-through', 'font-bold')}";
```

```typescript
// Compiled JavaScript
TW.When(todo.IsCompleted, 'line-through', 'font-bold')
```

### `TW.Dark(classes)`

Applies dark mode variant classes (prefixes with `dark:`).

```csharp
// C# - can be evaluated at compile-time if argument is constant
var className = TW.Dark(TW.Bg.Gray900); // Compile-time: "dark:bg-gray-900"

// C# - runtime evaluation if dynamic
var className = $"{TW.Dark(computedBg)}"; // Runtime
```

### `TW.Hover(classes)`, `TW.Focus(classes)`, `TW.GroupHover(classes)`

Similar to `TW.Dark()`, applies state variant prefixes.

### `TW.WithOpacity(className, opacity)`

Adds opacity to a Tailwind class.

```csharp
TW.WithOpacity("bg-white", 80) // "bg-white/80"
```

## Compile-Time vs Runtime

The compiler attempts to evaluate TailwindClass expressions at compile-time:

- ✅ **Compile-time**: Simple expressions with constant values
  ```csharp
  TW.Py(8) + TW.Text.Center  // Evaluated to: "py-8 text-center"
  ```

- ❌ **Runtime fallback**: Complex expressions or runtime conditions
  ```csharp
  TW.When(todo.IsCompleted, ...)  // Requires runtime value
  TW.Dark(computedBg)             // Dynamic value
  ```

When compile-time evaluation fails, the expression falls back to string interpolation in C#, or can emit TypeScript code that imports from `@equantic/tailwind`.

## Future

This architecture enables multiple styling libraries to coexist:

- `eQuantic.UI.Tailwind` → `@equantic/tailwind`
- `eQuantic.UI.Bootstrap` (future) → `@equantic/bootstrap`
- `eQuantic.UI.MaterialUI` (future) → `@equantic/material`

Each styling package manages its own TypeScript runtime, and the SDK loads the appropriate module based on project references.
