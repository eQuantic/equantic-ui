<h1 align="center">eQuantic.UI</h1>

<p align="center">
  <strong>Build fast web apps with C# — No WASM, No Compromise</strong>
</p>

<p align="center">
  A Flutter-inspired UI framework that compiles C# directly to optimized JavaScript.<br/>
  Type-safe. Lightweight. Zero external dependencies.
</p>

<p align="center">
  <a href="https://img.shields.io/github/actions/workflow/status/equantic/equantic-ui/ci.yml?branch=main"><img src="https://img.shields.io/github/actions/workflow/status/equantic/equantic-ui/ci.yml?branch=main" alt="Build Status" /></a>
  <a href="https://github.com/equantic/equantic-ui/blob/main/LICENSE"><img src="https://img.shields.io/github/license/equantic/equantic-ui" alt="License" /></a>
</p>

<p align="center">
  <a href="#quick-start">Quick Start</a> •
  <a href="#why-equanticui">Why eQuantic.UI</a> •
  <a href="#features">Features</a> •
  <a href="#how-it-works">How It Works</a> •
  <a href="#roadmap">Roadmap</a>
</p>

---

> **⚠️ Development Preview**
>
> eQuantic.UI is currently in active development. The NuGet packages are **not yet published** to nuget.org.
> To try it out, clone the repository and build from source (see [Contributing](#contributing)).
> We welcome early adopters and feedback!

---

## Why eQuantic.UI?

| Challenge | Blazor WASM | JavaScript Frameworks | **eQuantic.UI** |
|-----------|-------------|----------------------|-----------------|
| **Bundle size** | ~2MB+ (runtime) | Varies (~100KB-500KB) | **<30KB** runtime |
| **Language** | C# | JavaScript/TypeScript | **C#** |
| **Type safety** | At runtime | Optional (TS) | **Compile-time** |
| **Server actions** | SignalR setup | REST/GraphQL setup | **Built-in RPC** |
| **Learning curve** | Razor syntax | New ecosystem | **.NET familiar** |
| **External deps** | None | Node.js, npm | **None** |

**eQuantic.UI** gives you the best of both worlds: write in C#, deploy optimized JavaScript.

---

## Quick Start

### Prerequisites

- .NET 8.0 SDK (that's it — no Node.js, no npm, nothing else)

### 1. Create a new project

```bash
dotnet new web -n MyApp
cd MyApp
```

### 2. Add eQuantic.UI SDK

Update your `.csproj`:

```xml
<Project Sdk="eQuantic.UI.Sdk/0.1.1">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="eQuantic.UI.Core" Version="0.1.1" />
    <PackageReference Include="eQuantic.UI.Components" Version="0.1.1" />
    <PackageReference Include="eQuantic.UI.Server" Version="0.1.1" />
  </ItemGroup>

</Project>
```

### 3. Create your first component

```csharp
// Pages/Counter.cs
using eQuantic.UI.Core;
using eQuantic.UI.Components;
using eQuantic.UI.Core.Theme.Types;

[Page("/")]
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
            ClassName = "p-8 max-w-md mx-auto",
            Children =
            {
                new Heading($"Count: {_count}", 1),
                new Row
                {
                    Gap = "8px",
                    Children =
                    {
                        new Button
                        {
                            Text = "-",
                            Variant = Variant.Secondary,
                            OnClick = () => SetState(() => _count--)
                        },
                        new Button
                        {
                            Text = "+",
                            Variant = Variant.Primary,
                            OnClick = () => SetState(() => _count++)
                        }
                    }
                }
            }
        };
    }
}
```

### 4. Configure and run

```csharp
// Program.cs
using eQuantic.UI.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddUI(options =>
{
    options.ScanAssembly(typeof(Program).Assembly);
});

var app = builder.Build();
app.UseStaticFiles();
app.UseServerActions();
app.MapUI();
app.Run();
```

```bash
dotnet run
```

Your app is now running with a fully reactive counter — no JavaScript written.

---

## Features

### Component Model

Build UIs using familiar patterns inspired by Flutter and React:

```csharp
// Stateless - Pure functions of props
public class Greeting : StatelessComponent
{
    public string? Name { get; set; }

    public override IComponent Build(RenderContext context)
        => new Text($"Hello, {Name}!");
}

// Stateful - Internal state with reactive updates
public class Counter : StatefulComponent
{
    public override ComponentState CreateState() => new CounterState();
}

public class CounterState : ComponentState<Counter>
{
    private int _count = 0;

    public override IComponent Build(RenderContext context)
        => new Button
        {
            Text = $"Clicked {_count} times",
            OnClick = () => SetState(() => _count++)
        };
}
```

### Server Actions

Call server-side C# methods directly from your components — no REST endpoints, no serialization boilerplate:

```csharp
[Page("/todos")]
public class TodoList : StatefulComponent
{
    private readonly ITodoService _todoService;

    public TodoList(ITodoService todoService)
    {
        _todoService = todoService;
    }

    [ServerAction]
    public async Task<List<Todo>> GetTodos()
    {
        // Runs on the server with full .NET capabilities (DI, EF Core, etc.)
        return await _todoService.GetTodosAsync();
    }

    [ServerAction]
    [Authorize(Roles = "Admin")]
    public async Task DeleteTodo(Guid id)
    {
        // Authorization is enforced server-side
        await _todoService.DeleteTodoAsync(id);
    }
}
```

### Theming System

Consistent styling with type-safe variants:

```csharp
new Button
{
    Text = "Submit",
    Variant = Variant.Primary,    // Primary, Secondary, Destructive, Outline, Ghost, Link...
    Size = Size.Large             // Small, Medium, Large, XLarge
}
```

### Tailwind CSS Integration (Optional)

First-class Tailwind support with automatic CSS generation:

```xml
<PackageReference Include="eQuantic.UI.Tailwind" Version="0.1.1" />
```

```csharp
new Container
{
    ClassName = "flex items-center gap-4 p-6 bg-white rounded-lg shadow-md",
    Children = { /* ... */ }
}
```

---

## How It Works

```text
┌─────────────────────────────────────────────────────────────────┐
│                        BUILD TIME                                │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│   Counter.cs ──► Roslyn Parser ──► TypeScript ──► JavaScript    │
│                                                                  │
│   • Type checking at compile time                               │
│   • Tree-shaking removes unused code                            │
│   • Code splitting per page/route                               │
│   • Source maps for C# debugging in browser                     │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                        RUNTIME (<30KB)                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│   • Virtual DOM with keyed reconciliation                       │
│   • Event delegation and state management                       │
│   • Server Actions RPC bridge                                   │
│   • SSR hydration support                                       │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Zero External Dependencies

The entire toolchain is embedded in NuGet packages:

- **No Node.js** required on dev machine or CI
- **No npm** packages to manage
- **No global tools** to install
- Just `dotnet build` — everything works

---

## Supported C# Features

The compiler supports modern C# constructs:

| Category | Supported |
|----------|-----------|
| **Expressions** | Arithmetic, logical, ternary, string interpolation, `??`, `?.`, `?[]`, `^n` (index from end) |
| **Control Flow** | `if`, `switch`, `for`, `foreach`, `while`, `do-while`, `break`, `continue`, `throw` |
| **Pattern Matching** | Type, property, positional, relational patterns (C# 9-12) |
| **LINQ** | `Select`, `SelectMany`, `Where`, `First`, `Last`, `Single`, `Any`, `All`, `Count`, `Sum`, `Average`, `Min`, `Max`, `OrderBy`, `Skip`, `Take`, `Distinct`, `Contains`, `Reverse` |
| **Async/Await** | `Task<T>` → `Promise<T>` |
| **Resources** | `using` statements and declarations |
| **Exceptions** | `try-catch-finally`, `throw` (Exception → Error) |

---

## Project Structure

```text
src/
├── eQuantic.UI.Core/        # Core abstractions (IComponent, HtmlElement)
├── eQuantic.UI.Components/  # Standard components (Button, Input, Container...)
├── eQuantic.UI.Compiler/    # Roslyn-based C# → JavaScript transpiler
├── eQuantic.UI.Sdk/         # MSBuild SDK for project integration
├── eQuantic.UI.Server/      # ASP.NET Core integration & Server Actions
├── eQuantic.UI.Runtime/     # Browser runtime (TypeScript)
├── eQuantic.UI.Tailwind/    # Tailwind CSS integration
└── eQuantic.UI.CLI/         # Developer tools
```

---

## Documentation

- [Build Flow](https://github.com/equantic/equantic-ui/wiki/BuildFlow) - How the compilation pipeline works
- [CLAUDE.md](CLAUDE.md) - Technical reference for contributors

---

## Roadmap

### Completed

| Phase | Description |
|-------|-------------|
| ✅ Core Architecture | Component model, Virtual DOM, HtmlNode abstraction |
| ✅ Compiler & SDK | Roslyn-based C# → TypeScript → JavaScript transpilation |
| ✅ Runtime & State | Keyed reconciliation, WeakMap event tracking, state management |
| ✅ Server Actions | RPC bridge with `[Authorize]` and payload validation |
| ✅ SSR & Hydration | Server-side rendering with client hydration |
| ✅ Theming System | `StyleBuilder` (CVA pattern), `Variant`/`Size` enums, `IAppTheme` |
| ✅ Developer Experience | Source Maps for C# debugging, HMR support |

### In Progress

| Feature | Description |
|---------|-------------|
| 🚧 E2E Testing | Playwright tests for `TodoListApp` sample |
| 🚧 Component Playground | Interactive showcase of all components |
| 🚧 Documentation | Comprehensive guides and API reference |

### Planned

| Feature | Description |
|---------|-------------|
| 📋 NuGet Publishing | Publish packages to nuget.org |
| 📋 DataGrid Pro | Enterprise-grade data grid with pagination and editing |
| 📋 Dynamic Themes | Runtime Dark Mode switching |
| 📋 eQuantic DevTools | Browser extension to inspect component tree and state |
| 📋 Material Components | `eQuantic.UI.Material` component library |
| 📋 Online Playground | WASM-based online editor |

See the [full roadmap](https://github.com/equantic/equantic-ui/wiki/Roadmap) for more details.

---

## Contributing

We welcome contributions! See our [Contributing Guide](CONTRIBUTING.md) for details.

- [Code of Conduct](CODE_OF_CONDUCT.md)
- [Technical Reference](CLAUDE.md)

---

## License

MIT © [eQuantic](https://github.com/eQuantic)

---

<p align="center">
  <sub>Built with C# and a lot of ☕ by the eQuantic team</sub>
</p>
