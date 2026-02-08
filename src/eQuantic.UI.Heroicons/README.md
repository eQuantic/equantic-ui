# eQuantic.UI.Heroicons

Heroicons implementation for the eQuantic.UI framework. This package provides over 280 high-quality, flexible icons as native C# components, including both Solid and Outline variants.

## Installation

Add a reference to the `eQuantic.UI.Heroicons` package in your project:

```xml
<ProjectReference Include="..\eQuantic.UI.Heroicons\eQuantic.UI.Heroicons.csproj" />
```

## Setup

Register the Heroicons in your `Program.cs`:

```csharp
using eQuantic.UI.Heroicons;

builder.Services.AddHeroiconsIcons();
```

## Usage

### Fluent Static Access

The `HeroiconsIcons` static class provides all icons as methods.

```csharp
using eQuantic.UI.Heroicons;

// Default usage
var icon = HeroiconsIcons.AcademicCap();

// With customization
var redIcon = HeroiconsIcons.AdjustmentsHorizontal(
    size: 32,
    color: "red"
);
```

### Properties

| Property    | Type     | Default          | Description                                  |
| :---------- | :------- | :--------------- | :------------------------------------------- |
| `Size`      | `int`    | `24`             | Width and height in pixels.                  |
| `Color`     | `string` | `"currentColor"` | Strokes/Fill color (via CSS color property). |
| `ClassName` | `string` | `null`           | Additional CSS classes for the SVG element.  |

## License

MIT / Heroicons
