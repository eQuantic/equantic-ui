# eQuantic.UI

> **Component-based UI Framework for .NET Web** - Type-safe, compiler-first, HTML-native.

![Build Status](https://img.shields.io/github/actions/workflow/status/equantic/equantic-ui/ci.yml?branch=main)
![NuGet Version](https://img.shields.io/nuget/v/eQuantic.UI.Sdk)
![License](https://img.shields.io/github/license/equantic/equantic-ui)

## Overview

**eQuantic.UI** is a Flutter-inspired UI framework for building web applications with C#. Unlike Blazor, it compiles C# components directly to optimized JavaScript at build time, resulting in zero-overhead runtime performance while maintaining the developer experience of a strongly-typed language.

### Key Features

- ⚛️ **Component-based** - Build complex UIs using the Composite Pattern.
- 🌐 **HTML-native** - Components map directly to HTML elements with standard attributes (`id`, `className`, `style`).
- 🎨 **Theme Maturity** - Standardized `Variant` and `Size` enums for consistent look & feel.
- 🛠️ **StyleBuilder** - Type-safe, CVA-inspired utility for managing conditional CSS classes.
- 🚀 **Compiler-First** - C# code is transpiled to efficient JavaScript via a Roslyn-based compiler.
- 📦 **SDK Integration** - Seamless integration via `eQuantic.UI.Sdk` and standard `.csproj` files.
- 📉 **Minimal Runtime** - Ultra-lightweight runtime (<30kb) compared to WASM-based solutions.

---

## Project Structure

```bash
src/
├── eQuantic.UI.Core/        # Core abstractions (IComponent, HtmlElement)
├── eQuantic.UI.Components/  # Base library (Container, Text, Button, Input)
├── eQuantic.UI.Compiler/    # Roslyn-based C# to JavaScript transpiler
├── eQuantic.UI.Sdk/         # MSBuild SDK for seamless integration
├── eQuantic.Build/          # MSBuild Tasks for build pipeline
├── eQuantic.UI.Runtime/     # TypeScript browser runtime
└── eQuantic.UI.CLI/         # Developer tools
```

---

## Getting Started

### Prerequisites

- .NET 8.0 SDK

### Installation

Add the SDK to your project file:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Sdk Name="eQuantic.UI.Sdk" Version="0.1.0" />

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>
```

### Example Component

Simply create a `.cs` file and define your component:

```csharp
using eQuantic.UI;
using eQuantic.UI.Components;

[Component]
public class Counter : StatefulComponent
{
    public override ComponentState CreateState() => new CounterState();
}

public class CounterState : ComponentState<Counter>
{
    private int _count = 0;

    public override IComponent Build(RenderContext context)
    {
        return new Container
        {
            ClassName = "p-4 border rounded",
            Children =
            {
                new Text($"Count: {_count}"),
                new Button
                {
                    ClassName = "btn btn-primary ml-2",
                    OnClick = () => SetState(() => _count++),
                    Children = { new Text("Increment") }
                }
            }
        };
    }
}
```

---

## Roadmap

We are currently in **Beta** release.

- ✅ Core Architecture & Foundation
- ✅ Compiler & SDK Implementation
- ✅ CI/CD & Packaging
- ✅ Runtime & State Management
- 🚧 Ecosystem Maturity (Current)

## Community

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

- [Code of Conduct](CODE_OF_CONDUCT.md)
- [License](LICENSE)

---

## License

MIT © [eQuantic](https://github.com/eQuantic)
