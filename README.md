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
| **Bundle size** | ~2MB+ (runtime) | Varies (~100KB-500KB) | **~57KB** runtime |
| **Language** | C# | JavaScript/TypeScript | **C#** |
| **Type safety** | At runtime | Optional (TS) | **Compile-time** |
| **Server actions** | SignalR setup | REST/GraphQL setup | **Built-in RPC** |
| **Learning curve** | Razor syntax | New ecosystem | **.NET familiar** |
| **External deps** | None | Node.js, npm | **None** |

**eQuantic.UI** gives you the best of both worlds: write in C#, deploy optimized JavaScript.

---

## Quick Start

### Prerequisites

- .NET 10.0 SDK (that's it — no Node.js, no npm, nothing else)

### 1. Create a new project

```bash
dotnet new web -n MyApp
cd MyApp
```

### 2. Add eQuantic.UI SDK

Update your `.csproj`:

```xml
<Project Sdk="eQuantic.UI.Sdk/0.1.6">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <!-- No manual package references needed - SDK includes everything automatically -->

</Project>
```

> The SDK automatically includes `eQuantic.UI.Core`, `eQuantic.UI.Components`, `eQuantic.UI.Server`, and `eQuantic.UI.Runtime` packages.

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

### Type-Safe Enum Operations

Full support for enum parsing, validation, and enumeration — perfect for dropdowns, filters, and status management:

```csharp
public enum OrderStatus { Pending, Processing, Shipped, Delivered, Cancelled }

[Page("/orders")]
public class OrderFilter : StatefulComponent
{
    private OrderStatus? _selectedStatus;

    public override IComponent Build(RenderContext context)
    {
        // Get all enum values for dropdown options
        var statusOptions = Enum.GetValues<OrderStatus>()
            .Select(s => new { Value = s, Label = s.ToString() });

        return new Container
        {
            Children =
            {
                new Select
                {
                    Options = statusOptions,
                    Value = _selectedStatus,
                    OnChange = (string value) =>
                    {
                        // Type-safe parsing with TryParse
                        if (Enum.TryParse<OrderStatus>(value, out var status))
                        {
                            SetState(() => _selectedStatus = status);
                        }
                    }
                },

                // Validate enum from user input
                new Input
                {
                    Placeholder = "Enter status",
                    OnBlur = (string input) =>
                    {
                        if (Enum.IsDefined(typeof(OrderStatus), input))
                        {
                            var status = Enum.Parse<OrderStatus>(input);
                            Console.WriteLine($"Valid status: {status}");
                        }
                    }
                }
            }
        };
    }
}
```

### Dictionary Operations

Full Dictionary support for state management, caching, and lookup tables:

```csharp
[Page("/settings")]
public class UserSettings : StatefulComponent
{
    private Dictionary<string, object> _settings = new();

    public override IComponent Build(RenderContext context)
    {
        return new Container
        {
            Children =
            {
                new Button
                {
                    Text = "Load Settings",
                    OnClick = async () =>
                    {
                        // Add settings
                        _settings.Add("theme", "dark");
                        _settings.Add("notifications", true);
                        _settings.Add("maxItems", 50);

                        // Check if key exists
                        if (_settings.ContainsKey("theme"))
                        {
                            var theme = _settings["theme"];
                            Console.WriteLine($"Current theme: {theme}");
                        }

                        // Safe retrieval with TryGetValue
                        if (_settings.TryGetValue("maxItems", out var max))
                        {
                            Console.WriteLine($"Max items: {max}");
                        }

                        // Iterate keys and values
                        foreach (var key in _settings.Keys)
                        {
                            Console.WriteLine($"{key} = {_settings[key]}");
                        }

                        SetState(() => { }); // Trigger re-render
                    }
                },

                new Button
                {
                    Text = "Clear Settings",
                    OnClick = () => SetState(() => _settings.Clear())
                }
            }
        };
    }
}
```

### Production-Ready Components

Components with enterprise-grade robustness matching shadcn/ui:

#### Compound Components Pattern

```csharp
new Card {
    Variant = CardVariant.Elevated,
    Children = {
        new CardHeader {
            Children = {
                new CardTitle { Text = "Q1 2026 Roadmap" },
                new CardDescription { Text = "Key initiatives" }
            }
        },
        new CardBody {
            Children = { new Text("Launch mobile app...") }
        },
        new CardFooter {
            Children = { new Button { Text = "View Details" } }
        }
    }
}
```

#### Loading States

```csharp
new Button {
    Text = "Save Changes",
    Loading = isSaving,  // Automatic spinner + disabled state
    OnClick = HandleSave
}
```

#### Form Validation

```csharp
new FormField {
    Label = "Email",
    Required = true,
    Error = validationError,
    HelperText = "We'll never share your email",
    Children = {
        new TextInput { Type = "email", DefaultValue = "" }
    }
}
```

#### Input Groups

```csharp
new InputGroup {
    Children = {
        new InputAddon { Text = "https://" },
        new TextInput { Placeholder = "example.com" },
        new Button { Text = "Go" }
    }
}
```

**Features:**

- ✅ 6 Card variants (Default, Outline, Elevated, Subtle, Ghost, and custom via theme)
- ✅ Controlled/Uncontrolled inputs (DefaultValue, DefaultChecked)
- ✅ FormField with error messages and helper text
- ✅ Loading spinners on buttons
- ✅ Input groups for composite inputs
- ✅ Data & ARIA attributes support
- ✅ Complete XML documentation

[Learn more about Component Robustness →](https://github.com/equantic/equantic-ui/wiki/ComponentRobustness)

### Theming System

Consistent styling with type-safe variants:

```csharp
new Button
{
    Text = "Submit",
    Variant = Variant.Primary,        // Primary, Secondary, Destructive, Outline, Ghost, Link...
    Size = SizeVariant.Large          // Small, Medium, Large, XLarge
}
```

### Tailwind CSS Integration (Optional)

First-class Tailwind support with three approaches for maximum flexibility:

```xml
<PackageReference Include="eQuantic.UI.Tailwind" Version="0.1.6" />
```

#### 1. Type-Safe Typed Objects with + Operator (Recommended)

```csharp
using eQuantic.UI.Tailwind;

new Container
{
    // Clean syntax with compile-time safety and IntelliSense
    ClassName = TW.Display.Flex + TW.Flex.ItemsCenter + TW.Gap(4) + TW.P(6) +
                TW.Bg.White + TW.Rounded.Lg + TW.Shadow.Md +
                TW.Hover(TW.Bg.Gray100) +
                TW.Dark(TW.Bg.Zinc900)
}
```

#### 2. Fluent Builder API

```csharp
new Container
{
    ClassName = TW.Build()
        .Add(TW.Display.Flex, TW.Flex.ItemsCenter, TW.Gap(4), TW.P(6))
        .Add(TW.Bg.White, TW.Rounded.Lg, TW.Shadow.Md)
        .Hover(TW.Bg.Gray100)
        .Dark(TW.Bg.Zinc900)
        .Build()
}
```

#### 3. Raw Strings (when needed)

```csharp
new Container
{
    ClassName = "flex items-center gap-4 p-6 bg-white rounded-lg shadow-md"
}
```

#### Key Benefits

- ✅ Full IntelliSense autocomplete
- ✅ Compile-time type checking
- ✅ Refactoring support (rename, find usages)
- ✅ Zero runtime overhead (value types)
- ✅ Mix typed objects with raw strings for arbitrary values
- ✅ Generic `ClassBuilder` in Core for any CSS framework

---

## How It Works

```text
┌─────────────────────────────────────────────────────────────────┐
│                        BUILD TIME                               │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│   Counter.cs ──► Roslyn Parser ──► TypeScript ──► JavaScript    │
│                                                                 │
│   • Type checking at compile time                               │
│   • Tree-shaking removes unused code                            │
│   • Code splitting per page/route                               │
│   • Source maps for C# debugging in browser                     │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                        RUNTIME (~57KB)                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│   • Virtual DOM with keyed reconciliation                       │
│   • Event delegation and state management                       │
│   • Server Actions RPC bridge                                   │
│   • SSR hydration support                                       │
│   • Development tools (logger, error overlay)                   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Zero External Dependencies

The entire toolchain is embedded in NuGet packages:

- **No Node.js** required on dev machine or CI
- **No npm** packages to manage
- **No global tools** to install
- Just `dotnet build` — everything works

### Self-Contained Package Architecture

eQuantic.UI uses a **self-contained package architecture** where each package manages its own artifacts:

- **Runtime Package** (`eQuantic.UI.Runtime`) contains `runtime.js` (~57KB)
- **Components Package** (`eQuantic.UI.Components`) contains C# source for type resolution
- **SDK Package** (`eQuantic.UI.Sdk`) orchestrates build via NuGet references

This design ensures:

- ✅ **No tight coupling** between packages
- ✅ **Independent versioning** (e.g. use Runtime 0.1.7 with SDK 0.1.6)
- ✅ **No artifact duplication** across packages
- ✅ **Flexible updates** without republishing all packages

[Learn more about the package architecture →](https://github.com/equantic/equantic-ui/wiki/PackageArchitecture)

---

## Supported C# Features

The compiler supports modern C# constructs:

Fidelity is enforced by a **conformance harness** (460+ cases) that runs each C# construct as both
transpiled JS and real .NET and asserts identical results. Every construct resolves to one of three
mechanisms: a native JS strategy, a faithful `$eq.*` compat helper, or a build error when it's
genuinely impossible.

| Category | Supported |
|----------|-----------|
| **Expressions** | Arithmetic, logical, ternary, string interpolation, `??`, `?.`, `?[]`, `^n` (index from end), `checked`/`unchecked` overflow |
| **Control Flow** | `if`, `switch`, `for`, `foreach`, `while`, `do-while`, `break`, `continue`, `throw`, local functions |
| **Pattern Matching** | Type, property, positional, relational patterns (C# 9-12) |
| **Numeric types** | `int`/`double`/`float`, **`decimal`** (exact base-10, wire-as-string), **`long`/`ulong`** (BigInt, exact), parsing & `Convert.ToX` |
| **Value types** | `record`/`struct`/value tuples — **structural** `==`/`Equals`/`Contains`/`Distinct`, `with` copies, deconstruction; records emit as **named JS classes** with their instance methods, inheritance & generics, restored after SSR hydration |
| **Date & Time** | `DateTime`, `DateTimeOffset`, `TimeSpan`, `DateOnly`, `TimeOnly` — tick-precise compat (formatting, arithmetic, comparison) |
| **Nullable** | `Nullable<T>` — `HasValue`/`Value`/`GetValueOrDefault`, lifted arithmetic/relational with null-propagation |
| **String / Text** | `Split`, `Replace`, `StartsWith`/`EndsWith`/`Contains` (+ `StringComparison`/IgnoreCase), `Substring`, `IndexOf`, `PadLeft/Right`, `Trim*`, `Join`, `Concat`, `Format` (F/X/N specifiers), `StringBuilder` |
| **Collections** | `List`, `Dictionary` (string/number keys → object; **record/struct/tuple keys → structural `valueMap`**), `HashSet`, `Queue`, `Stack` |
| **Dictionary** | `ContainsKey`, `TryGetValue`, `GetValueOrDefault`, `Add`, `Remove`, `Clear`, `Keys`, `Values`, `Count`, indexer get/set, `foreach` |
| **Enum** | `Enum.Parse<T>`, `Enum.TryParse<T>`, `Enum.GetValues<T>`, `Enum.GetNames<T>`, `Enum.IsDefined` |
| **LINQ** | `Select`/`SelectMany`/`Where` (+ indexed), `OrderBy`/`ThenBy` (stable composite), `GroupBy`, `Join`/`GroupJoin`/`ToLookup`, `ToDictionary`/`ToList`/`ToArray`, `Distinct(By)`/`Min(By)`/`Max(By)`, `Take(While)`/`Skip(While)`, `Aggregate`/`Sum`/`Average`/`Count`/`Any`/`All`/`First`/`Last`, `Zip`/`Chunk`/`Concat`/`Reverse` |
| **LINQ Set Operations** | `Union`, `Intersect`, `Except`, `Concat` |
| **Async/Await** | `Task<T>` → `Promise<T>` |
| **Resources** | `using` statements and declarations |
| **Exceptions** | `try-catch-finally`, `throw` (Exception → Error) |

> Constructs with no JS equivalent (pointers, `goto`, client-side `System.IO`/`Net.Http`, etc.) fail the
> build with a canonical diagnostic instead of miscompiling silently. See the
> [.NET coverage program](docs/DOTNET-COVERAGE-PROGRAM.md) and the
> [Supported Features wiki](https://github.com/equantic/equantic-ui/wiki/SupportedFeatures) for the full matrix.

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

- [📚 Wiki Home](https://github.com/equantic/equantic-ui/wiki) - Complete documentation
- [🏗️ Architecture](https://github.com/equantic/equantic-ui/wiki/Architecture) - DDD, CQRS, and Clean Architecture
- [📦 Package Architecture](https://github.com/equantic/equantic-ui/wiki/PackageArchitecture) - Self-contained package design
- [🧩 Core Components](https://github.com/equantic/equantic-ui/wiki/CoreComponents) - HtmlNode, HtmlElement, component types
- [💪 Component Robustness](https://github.com/equantic/equantic-ui/wiki/ComponentRobustness) - Production-ready components
- [🎨 Styling](https://github.com/equantic/equantic-ui/wiki/Styling) - Theme system and CSS integration
- [🔨 Build Flow](https://github.com/equantic/equantic-ui/wiki/BuildFlow) - How the compilation pipeline works
- [⚙️ Compiler](https://github.com/equantic/equantic-ui/wiki/Compiler) - C# to JavaScript transpilation
- [⚡ Runtime](https://github.com/equantic/equantic-ui/wiki/Runtime) - Virtual DOM and browser runtime
- [🐛 Debugging Tools](https://github.com/equantic/equantic-ui/wiki/Debug) - Professional debugging with logger and error overlay
- [🗺️ Roadmap](https://github.com/equantic/equantic-ui/wiki/Roadmap) - Project progress and future plans
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
| ✅ Theming System | `StyleBuilder` (CVA pattern), `Variant`/`SizeVariant` enums, `IAppTheme` |
| ✅ .NET Surface Coverage | Conformance-validated compat for value types, decimal/long, date-time family, Nullable, StringBuilder, collections (incl. record-keyed dictionaries) |
| ✅ Component Robustness | Compound components, variants, loading states, validation, input groups |
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
| 📋 Material Components | Expand the `eQuantic.UI.Material` package (theme + components available in preview) |
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
