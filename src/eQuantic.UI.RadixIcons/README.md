# eQuantic.UI.RadixIcons

Radix Icons implementation for the eQuantic.UI framework. This package provides the full set of Radix Icons as native C# components.

## Installation

Add a reference to the `eQuantic.UI.RadixIcons` package in your project:

```xml
<ProjectReference Include="..\eQuantic.UI.RadixIcons\eQuantic.UI.RadixIcons.csproj" />
```

## Setup

Register the Radix Icons in your `Program.cs`:

```csharp
using eQuantic.UI.RadixIcons;

builder.Services.AddRadixIconsIcons();
```

## Usage

### Fluent Static Access

The `RadixIconsIcons` static class provides all icons as methods.

```csharp
using eQuantic.UI.RadixIcons;

// Default usage
var icon = RadixIconsIcons.Accessibility();

// With customization
var purpleIcon = RadixIconsIcons.Archive(
    size: 20,
    color: "#6750A4"
);
```

## License

MIT / Radix UI
