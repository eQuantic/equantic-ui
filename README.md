# eQuantic.UI

> **Component-based UI Framework for .NET Web** - Type-safe, compiler-first, HTML-native

## Overview

eQuantic.UI is a Flutter-inspired UI framework for building web applications with C#. It uses the **Composite Pattern** with components that have native HTML characteristics, compiling C# code to optimized JavaScript.

### Key Features

- ✅ **Component-based** - Familiar web terminology, not "widgets"
- ✅ **HTML-native** - Every component has `id`, `className`, `style`, `data-*`, `aria-*`
- ✅ **CSS-in-C#** - Type-safe styles compiled to optimized CSS with hash classNames
- ✅ **Composite Pattern** - True tree composition with `Children` collection
- ✅ **Type-Safe** - Full C# IntelliSense, no runtime type errors
- ✅ **Minimal Runtime** - Target: <30kb bundle (vs Blazor WASM 2MB+)

## Project Structure

```
src/
├── eQuantic.UI.Core/        # Core abstractions (IComponent, HtmlElement, StyleClass)
├── eQuantic.UI.Components/  # Base components (Container, Text, Button, TextInput)
├── eQuantic.UI.Compiler/    # Roslyn-based C# to JS compiler
├── eQuantic.UI.Runtime/     # Browser runtime (TypeScript)
└── eQuantic.UI.CLI/         # CLI tool (eqx create/dev/build)
```

## Quick Example

```csharp
using eQuantic.UI;
using eQuantic.UI.Components;

public class Counter : StatefulComponent
{
    public override ComponentState CreateState() => new CounterState();
}

public class CounterState : ComponentState<Counter>
{
    private int _count = 0;
    private string _message = "";

    public override IComponent Build(RenderContext context)
    {
        return new Container
        {
            Id = "counter",
            StyleClass = AppStyles.Card,
            Children =
            {
                new TextInput
                {
                    Value = _message,
                    Placeholder = "Type something...",
                    OnChange = (v) => SetState(() => _message = v)
                },

                new Button
                {
                    OnClick = () => SetState(() => _count++),
                    Text = $"Clicked {_count} times"
                }
            }
        };
    }
}
```

## CSS-in-C#

```csharp
public static class AppStyles
{
    public static readonly StyleClass Card = new()
    {
        BackgroundColor = Colors.White,
        Padding = Spacing.All(24),
        BorderRadius = 8,
        BoxShadow = "0 2px 8px rgba(0,0,0,0.1)",

        Hover = new()
        {
            BoxShadow = "0 4px 12px rgba(0,0,0,0.15)"
        }
    };
}
```

Generates optimized CSS:

```css
.eqx-a3f2d1 {
  background-color: #ffffff;
  padding: 24px;
  border-radius: 8px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}
.eqx-a3f2d1:hover {
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
}
```

## Development

```bash
# Build
dotnet build

# Run tests
dotnet test

# Run CLI (coming soon)
dotnet run --project src/eQuantic.UI.CLI -- dev
```

## Status

🚧 **POC in Development** - Target: March 2026

See [IMPLEMENTATION_PLAN.md](./IMPLEMENTATION_PLAN.md) for detailed roadmap.

## License

MIT © eQuantic
