# eQuantic.UI.Lucide

Lucide icon set implementation for the eQuantic.UI framework. This package provides more than 1,000 high-quality, flexible icons as native C# components.

## Installation

Add a reference to the `eQuantic.UI.Lucide` package in your project:

```xml
<ProjectReference Include="..\eQuantic.UI.Lucide\eQuantic.UI.Lucide.csproj" />
```

## Setup

Register the Lucide icons in your `Program.cs` (optional, for future configurations):

```csharp
using eQuantic.UI.Lucide;

builder.Services.AddLucideIcons();
```

### 🎨 Visual Bug Fixes (Buttons)

Resolved issues with button variants in the Tailwind theme:

- **Semantic Colors**: Added support for `Success`, `Warning`, and `Info` variants, which previously defaulted to the Primary color.
- **Outline Variant**: Fixed the `Outline` variant by adding the missing border class, ensuring it's clearly distinguishable from the `Ghost` variant.
- **Dark Mode Transparency**: Resolved an issue where nested `Text` components applied dark text colors inside colored buttons. This was fixed by making the `Text` component's base style transparent and setting the default color at the layout level (`DashboardShell`), ensuring perfect readability in both light and dark modes.

![Buttons Dark Mode Fix](/Users/admin.edgar.a.mesquita/.gemini/antigravity/brain/86bdaa3b-a0cf-4eb6-92e2-ddbde8627d2e/buttons_dark_mode_verification_1770573731107.png)

## Verification Results

## Usage

There are two primary ways to consume icons in your components.

### 1. Fluent Static Access (Recommended)

The `Lucide` static class provides all icons as methods. This is the most convenient and type-safe way to use them, with full IntelliSense support.

```csharp
using eQuantic.UI.Lucide;

// Default usage
var icon = Lucide.Check();

// With customization
var redActivity = Lucide.Activity(
    size: 32,
    strokeWidth: 2.5,
    color: "red",
    className: "my-custom-class"
);
```

### 2. Direct Component Usage

You can also use the `LucideIcon` component directly, which is useful for dynamic icon selection.

```csharp
using eQuantic.UI.Lucide;

new LucideIcon
{
    Name = "arrow-right",
    Size = 24,
    Color = "#6750A4",
    Content = Lucide.ArrowRight().Content
}
```

## Integration with Components

Lucide icons integrate seamlessly with other eQuantic.UI components like Buttons and Alerts.

### Buttons

```csharp
new Button
{
    Variant = Variant.Outline,
    Children =
    {
        Lucide.Plus(size: 16),
        new Text("Create Issue")
    }
}
```

### Alerts

```csharp
new Box
{
    ClassName = "flex items-center gap-2 text-red-500",
    Children =
    {
        Lucide.AlertCircle(size: 20),
        new Text("An error occurred!")
    }
}
```

## Customization

The icons are rendered as SVGs and support the following properties:

| Property      | Type     | Default          | Description                                    |
| :------------ | :------- | :--------------- | :--------------------------------------------- |
| `Size`        | `int`    | `24`             | Width and height in pixels.                    |
| `StrokeWidth` | `double` | `2`              | Width of the icon strokes.                     |
| `Color`       | `string` | `"currentColor"` | Strokes color (accepts CSS colors, hex, etc.). |
| `ClassName`   | `string` | `null`           | Additional CSS classes for the SVG element.    |

## License

MIT
