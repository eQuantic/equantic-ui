# eQuantic.UI.Components

Standard component library for the **eQuantic.UI** framework.

## Overview

This package contains a rich set of pre-built components designed for productivity and consistency:

- **Layout**: `Container`, `Flex`, `Stack`, `Grid`.
- **Typography**: `Text`, `Heading`.
- **Forms**: `Input`, `Button`, `Checkbox`, `Select`.
- **Navigation**: `Link`, `Sidebar`, `Navbar`.

All components are type-safe and map directly to optimized HTML element structures.

## Installation

```bash
dotnet add package eQuantic.UI.Components
```

## Usage

```csharp
using eQuantic.UI.Components;

var btn = new Button
{
    Variant = ButtonVariant.Primary,
    OnClick = () => Console.WriteLine("Clicked!"),
    Children = { new Text("Submit") }
};
```
