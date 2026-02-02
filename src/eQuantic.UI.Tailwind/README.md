# eQuantic.UI.Tailwind

Type-safe Tailwind CSS helpers for eQuantic.UI.

## Features

- ✅ **Type-safe** - IntelliSense autocomplete for all Tailwind classes
- ✅ **No magic strings** - Compile-time errors for typos
- ✅ **Fluent API** - Chainable builder for complex class combinations
- ✅ **Zero overhead** - Constants compiled away at build time
- ✅ **Complete coverage** - Layout, spacing, colors, typography, effects, and more

## Installation

```xml
<PackageReference Include="eQuantic.UI.Tailwind" Version="0.1.2" />
```

## Usage

### 1. Basic Constants

Instead of magic strings, use type-safe constants:

```csharp
// ❌ Before (string-based, error-prone)
ClassName = "flex items-center gap-4 p-6 bg-white rounded-lg shadow-md"

// ✅ After (type-safe, IntelliSense-friendly)
using eQuantic.UI.Tailwind;

ClassName = TW.Join(
    TW.Display.Flex,
    TW.Flex.ItemsCenter,
    TW.Gap[4],
    TW.P[6],
    TW.Bg.White,
    TW.Rounded.Lg,
    TW.Shadow.Md
)
```

### 2. Fluent Builder API

Build complex class combinations with a fluent API:

```csharp
using eQuantic.UI.Tailwind;

var buttonClasses = TW.Build()
    .Add(TW.Display.Flex, TW.Flex.ItemsCenter, TW.Gap[2])
    .Add(TW.P[4], TW.Rounded.Lg)
    .Add(TW.Font.Semibold, TW.Text.White)
    .Add(TW.Bg.Blue600, TW.Shadow.Md)
    .Hover(TW.Bg.Blue700, TW.Shadow.Lg)
    .Dark(TW.Bg.Zinc800)
    .ToString();

// Result: "flex items-center gap-2 p-4 rounded-lg font-semibold text-white bg-blue-600 shadow-md hover:bg-blue-700 hover:shadow-lg dark:bg-zinc-800"
```

### 3. Conditional Classes

Add classes conditionally:

```csharp
var cardClasses = TW.Build()
    .Add(TW.P[6], TW.Rounded.Xl, TW.Shadow.Lg)
    .When(isActive, TW.Bg.Blue50, TW.Border.Blue500)
    .When(!isActive, TW.Bg.White)
    .ToString();
```

### 4. Responsive Classes

Add responsive breakpoints easily:

```csharp
var gridClasses = TW.Build()
    .Add(TW.Display.Grid)
    .Add(TW.Grid.Cols1)          // Mobile: 1 column
    .Md(TW.Grid.Cols2)            // Tablet: 2 columns
    .Lg(TW.Grid.Cols3)            // Desktop: 3 columns
    .Xl(TW.Grid.Cols4)            // Large: 4 columns
    .Add(TW.Gap[6])
    .ToString();

// Result: "grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6"
```

### 5. State Variants

Apply hover, focus, active, and other state variants:

```csharp
var inputClasses = TW.Build()
    .Add(TW.W.Full, TW.P[3], TW.Rounded.Md)
    .Add(TW.Border.Border2, TW.Border.Gray300)
    .Focus(TW.Border.Blue500, TW.Shadow.Blue500_20)
    .Dark(TW.Bg.Zinc900, TW.Border.Zinc700)
    .ToString();
```

### 6. Gradients

Create beautiful gradients type-safely:

```csharp
var gradientClasses = TW.Build()
    .Add(TW.Bg.GradientToR)
    .Add(TW.From.Blue600, TW.Via.Purple600, TW.To.Pink600)
    .ToString();

// Or for the TodoListApp example:
var backgroundClasses = TW.Build()
    .Add(TW.Size.MinHScreen)
    .Add(TW.Bg.GradientToBr)
    .Add(TW.From.Blue50, TW.Via.Indigo50, TW.To.Purple50)
    .Dark(TW.From.Zinc950, TW.Via.Zinc900, TW.To.Zinc950)
    .Add(TW.P[8], TW.Grid.PlaceItemsCenter)
    .ToString();
```

## API Reference

### Layout & Display

```csharp
TW.Display.Block          // "block"
TW.Display.Flex           // "flex"
TW.Display.Grid           // "grid"
TW.Display.Hidden         // "hidden"

TW.Flex.Row               // "flex-row"
TW.Flex.Col               // "flex-col"
TW.Flex.ItemsCenter       // "items-center"
TW.Flex.JustifyBetween    // "justify-between"

TW.Grid.Cols3             // "grid-cols-3"
TW.Grid.Gap(4)            // "gap-4"
```

### Spacing

```csharp
TW.P[6]                   // "p-6"
TW.Spacing.Px(4)          // "px-4"
TW.Spacing.Py(2)          // "py-2"

TW.M[4]                   // "m-4"
TW.M.Auto                 // "m-auto"
TW.M.XAuto                // "mx-auto"

TW.Gap[4]                 // "gap-4"
TW.Gap.X(2)               // "gap-x-2"
```

### Sizing

```csharp
TW.W[64]                  // "w-64"
TW.W.Full                 // "w-full"
TW.W.Screen               // "w-screen"

TW.H[32]                  // "h-32"
TW.H.Full                 // "h-full"

TW.Size.MaxW2xl           // "max-w-2xl"
TW.Size.MinHScreen        // "min-h-screen"
```

### Colors

```csharp
// Backgrounds
TW.Bg.White               // "bg-white"
TW.Bg.Blue600             // "bg-blue-600"
TW.Bg.Zinc900             // "bg-zinc-900"

// Text colors
TW.Text.Gray700           // "text-gray-700"
TW.Text.Blue600           // "text-blue-600"
TW.Text.White             // "text-white"

// Gradients
TW.Bg.GradientToR         // "bg-gradient-to-r"
TW.From.Blue600           // "from-blue-600"
TW.To.Purple600           // "to-purple-600"
```

### Typography

```csharp
// Font sizes
TW.Text.Xs                // "text-xs"
TW.Text.Sm                // "text-sm"
TW.Text.Base              // "text-base"
TW.Text.Lg                // "text-lg"
TW.Text.Xl4               // "text-4xl"

// Font weights
TW.Font.Semibold          // "font-semibold"
TW.Font.Bold              // "font-bold"

// Text alignment
TW.Text.Center            // "text-center"
TW.Text.Left              // "text-left"
```

### Borders & Rounded

```csharp
TW.Border.Border2         // "border-2"
TW.Border.Gray300         // "border-gray-300"
TW.Border.Dashed          // "border-dashed"

TW.Rounded.Lg             // "rounded-lg"
TW.Rounded.Xl             // "rounded-xl"
TW.Rounded.Full           // "rounded-full"
```

### Effects & Transitions

```csharp
TW.Shadow.Md              // "shadow-md"
TW.Shadow.Lg              // "shadow-lg"
TW.Shadow.Blue500_20      // "shadow-blue-500/20"

TW.Opacity.Op50           // "opacity-50"
TW.Opacity.Op100          // "opacity-100"

TW.Backdrop.BlurXl        // "backdrop-blur-xl"

TW.Transition.All         // "transition-all"
TW.Transition.Duration300 // "duration-300"
```

### Transforms

```csharp
TW.Transform.Scale110     // "scale-110"
TW.Transform.Rotate90     // "rotate-90"
TW.Transform.TranslateX(4) // "translate-x-4"
```

## Real-World Examples

### Modern Card Component

```csharp
new Card
{
    ClassName = TW.Build()
        .Add(TW.W.Full, TW.Size.MaxW2xl)
        .Add(TW.Bg.White, TW.Shadow.Xl)
        .Add(TW.Rounded.Xl2, TW.P[6])
        .Add(TW.Backdrop.Blur2xl)
        .Dark(TW.Bg.Zinc900, TW.Border.Zinc800)
        .ToString()
}
```

### Glassmorphism Effect

```csharp
new Box
{
    ClassName = TW.Build()
        .Add("bg-white/95 dark:bg-zinc-900/95")
        .Add(TW.Backdrop.Blur2xl)
        .Add("border border-white/20 dark:border-zinc-800/50")
        .Add(TW.Shadow.Xl2)
        .Raw("shadow-blue-500/10 dark:shadow-purple-500/10")
        .ToString()
}
```

### Animated Button

```csharp
new Button
{
    ClassName = TW.Build()
        .Add(TW.Display.Flex, TW.Flex.ItemsCenter, TW.Gap[2])
        .Add(TW.P[4], TW.Rounded.Lg)
        .Add(TW.Font.Semibold, TW.Text.White)
        .Add(TW.Bg.GradientToR, TW.From.Blue600, TW.To.Purple600)
        .Add(TW.Shadow.Lg, TW.Shadow.Blue500_30)
        .Add(TW.Transition.All, TW.Transition.Duration300)
        .Hover(TW.Transform.Scale105, TW.From.Blue700, TW.To.Purple700)
        .ToString()
}
```

### Responsive Grid Layout

```csharp
new Box
{
    ClassName = TW.Build()
        .Add(TW.Display.Grid)
        .Add(TW.Grid.Cols1)
        .Sm(TW.Grid.Cols2)
        .Md(TW.Grid.Cols3)
        .Lg(TW.Grid.Cols4)
        .Add(TW.Gap[6], TW.P[4])
        .ToString()
}
```

## Benefits

### IntelliSense Support
Type `TW.` and get instant autocomplete for all available utilities.

### Refactoring Safety
Rename or find all usages of a specific class across your codebase.

### Compile-Time Errors
Typos are caught at compile time instead of runtime.

### No Runtime Overhead
All constants are resolved at compile time. Zero performance impact.

### Consistent Naming
Follow Tailwind's naming conventions with C# syntax.

## Comparison

```csharp
// Traditional (string-based)
ClassName = "flex items-center gap-4 p-6 bg-white rounded-lg shadow-md hover:bg-gray-100 dark:bg-zinc-900"

// With TW helpers (type-safe)
ClassName = TW.Build()
    .Add(TW.Flex.Row, TW.Flex.ItemsCenter, TW.Gap[4], TW.P[6])
    .Add(TW.Bg.White, TW.Rounded.Lg, TW.Shadow.Md)
    .Hover(TW.Bg.Gray100)
    .Dark(TW.Bg.Zinc900)
    .ToString()
```

## Tips

1. **Use aliases for common patterns:**
   ```csharp
   var FlexCenter = TW.Join(TW.Display.Flex, TW.Flex.ItemsCenter, TW.Flex.JustifyCenter);
   ```

2. **Combine with StyleBuilder for themes:**
   ```csharp
   ClassName = StyleBuilder.Create(theme.Base)
       .Add(TW.Build().Add(TW.P[4], TW.Rounded.Lg).ToString())
       .Build()
   ```

3. **Use Raw() for arbitrary values:**
   ```csharp
   TW.Build()
       .Add(TW.W[64])
       .Raw("w-[calc(100%-2rem)]")  // Arbitrary value
       .ToString()
   ```

## License

MIT © [eQuantic](https://github.com/eQuantic)
