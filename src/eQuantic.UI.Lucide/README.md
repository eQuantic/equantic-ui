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
