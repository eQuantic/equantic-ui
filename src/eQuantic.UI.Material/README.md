# eQuantic.UI.Material

Material Design 3 theme implementation for eQuantic.UI framework.

## Installation

Add a reference to the `eQuantic.UI.Material` package:

```xml
<PackageReference Include="eQuantic.UI.Material" Version="1.0.0" />
```

## Setup

Register the Material theme in your `Program.cs`:

```csharp
using eQuantic.UI.Material;

var builder = WebApplication.CreateBuilder(args);

// Add Material Design 3 theme
builder.Services.AddMaterialTheme();

// Or with custom configuration
builder.Services.AddMaterialTheme(options => {
    options.SourceColor = "#6750A4"; // Custom primary color
    options.DarkMode = true;         // Default to dark mode
    options.FontFamily = "Inter";    // Custom font
});
```

## Usage with Components

Components automatically use the registered theme:

```csharp
// Button uses MaterialButtonTheme classes
new Button {
    Text = "Click me",
    Variant = Variant.Primary,  // Uses md-button--filled
    Size = SizeVariant.Large           // Uses md-button--large
}

// Card uses MaterialCardTheme classes
new Card {
    Variant = CardVariant.Elevated,  // Uses md-card--elevated
    Children = {
        new Text("Card content")
    }
}
```

## M3 Static Classes

Use `M3` for direct class access with IntelliSense:

```csharp
using eQuantic.UI.Material;

// Typography
new Box { ClassName = M3.Typography.HeadlineLarge }

// Button variants
new Box { ClassName = $"{M3.Button.Base} {M3.Button.Filled}" }

// Cards
new Box { ClassName = $"{M3.Card.Base} {M3.Card.Elevated}" }
```

## Dark Mode

The theme script automatically handles dark mode:

```html
<!-- Add to your layout -->
<script src="/_equantic/material-theme.js"></script>
```

### JavaScript API

```javascript
// Toggle dark mode
eQuantic.Material.toggleTheme();

// Set specific theme
eQuantic.Material.setTheme("dark"); // 'light' | 'dark' | 'system'

// Check current state
const isDark = eQuantic.Material.isDarkMode();

// Apply custom source color (generates M3 palette)
eQuantic.Material.applySourceColor("#6750A4");
```

## CSS Custom Properties

All M3 tokens are available as CSS custom properties:

```css
/* Color roles */
var(--md-sys-color-primary)
var(--md-sys-color-on-primary)
var(--md-sys-color-surface)
var(--md-sys-color-on-surface)

/* Typography */
var(--md-sys-typescale-body-large-font-size)
var(--md-sys-typescale-headline-medium-line-height)

/* Shape */
var(--md-sys-shape-corner-medium)
var(--md-sys-shape-corner-full)

/* Elevation */
var(--md-sys-elevation-level1)
var(--md-sys-elevation-level3)
```

## Component Reference

| Component | CSS Classes     | Variants                                |
| --------- | --------------- | --------------------------------------- |
| Button    | `md-button`     | filled, outlined, text, tonal, elevated |
| Card      | `md-card`       | elevated, filled, outlined              |
| Dialog    | `md-dialog`     | standard, fullscreen                    |
| TextField | `md-text-field` | filled, outlined                        |
| Checkbox  | `md-checkbox`   | checked, indeterminate                  |
| Switch    | `md-switch`     | standard                                |
| Tabs      | `md-tabs`       | primary, secondary                      |
| Badge     | `md-badge`      | primary, secondary, dot                 |
| Alert     | `md-alert`      | inline, error, warning, success         |
| Select    | `md-select`     | standard                                |

## License

MIT
