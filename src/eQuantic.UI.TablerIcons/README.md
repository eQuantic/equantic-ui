# eQuantic.UI.TablerIcons

Tabler Icons implementation for the eQuantic.UI framework. This package provides over 5,000 highly customizable icons as native C# components.

## Installation

Add a reference to the `eQuantic.UI.TablerIcons` package in your project:

```xml
<ProjectReference Include="..\eQuantic.UI.TablerIcons\eQuantic.UI.TablerIcons.csproj" />
```

## Setup

Register the Tabler Icons in your `Program.cs`:

```csharp
using eQuantic.UI.TablerIcons;

builder.Services.AddTablerIconsIcons();
```

## Usage

### Fluent Static Access

The `TablerIconsIcons` static class provides all icons as methods.

```csharp
using eQuantic.UI.TablerIcons;

// Default usage
var icon = TablerIconsIcons.Activity();

// With customization
var boldIcon = TablerIconsIcons.Adjustments(
    size: 32,
    strokeWidth: 3,
    color: "blue"
);
```

## License

MIT / Tabler Icons
