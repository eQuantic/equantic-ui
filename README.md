<h1 align="center">eQuantic.UI</h1>

<p align="center">
  <strong>One C# codebase. Real web. Real native.</strong>
</p>

<p align="center">
  Write components once in C#: on the web they compile to optimized JavaScript at build time (no WASM),<br/>
  and natively they render through <strong>Photon</strong>, a proprietary GPU engine (Metal/Vulkan — no WebView, no Skia).
</p>

<p align="center">
  <a href="https://img.shields.io/github/actions/workflow/status/equantic/equantic-ui/ci.yml?branch=main"><img src="https://img.shields.io/github/actions/workflow/status/equantic/equantic-ui/ci.yml?branch=main" alt="Build Status" /></a>
  <a href="https://github.com/equantic/equantic-ui/blob/main/LICENSE"><img src="https://img.shields.io/github/license/equantic/equantic-ui" alt="License" /></a>
</p>

<p align="center">
  <strong><a href="https://ui.equantic.tech/playground">Try it in your browser →</a></strong><br/>
  <sub>Write a component in C#, press Run, and watch it render — compiled by the same eqc your build uses. Nothing to install.</sub>
</p>

<p align="center">
  <a href="https://ui.equantic.tech/playground">Playground</a> •
  <a href="#quick-start">Quick Start</a> •
  <a href="#why-equanticui">Why eQuantic.UI</a> •
  <a href="#features">Features</a> •
  <a href="#how-it-works">How It Works</a> •
  <a href="#documentation">Documentation</a>
</p>

---

> **⚠️ Development Preview**
>
> eQuantic.UI is in active development and published to nuget.org as **prereleases**
> (`0.2.0-preview.*`) — `dotnet new install eQuantic.UI.Templates` and the Quick Start below are
> the intended way in; you do not need to build from source. Expect the surface to move between
> previews. We welcome early adopters and feedback!

---

## Why eQuantic.UI?

| Challenge | Blazor WASM | JavaScript frameworks | **eQuantic.UI** |
|-----------|-------------|----------------------|-----------------|
| **Language** | C# | JavaScript/TypeScript | **C#, end to end** |
| **Web payload** | ~2 MB+ runtime | varies | **~85 KB** gzipped runtime, per-page code splitting |
| **Native apps** | separate MAUI codebase | separate React Native/Electron | **the same components**, GPU-rendered |
| **Styling** | CSS/Razor | CSS-in-JS / utility classes | **typed C# — no CSS authored**, atomic classes generated |
| **Server calls** | SignalR setup | REST/GraphQL setup | **built-in RPC** (`[ServerAction]`) |
| **Toolchain** | .NET | Node.js, npm, bundlers | **only the .NET SDK** — everything else is embedded |

Components are authored **once** against an abstract visual vocabulary and realized per target:
DOM + CSS on the web, GPU pixels on macOS/iOS/Android. Not "write once, run in a WebView" —
each target gets its real rendering path.

---

## Quick Start

### Prerequisites

- .NET 10.0 SDK — that's it. No Node.js, no npm; the TypeScript/bundling toolchain ships embedded.

### Web app in three commands

```bash
dotnet new install eQuantic.UI.Templates
dotnet new equantic-app -n MyApp
cd MyApp && dotnet run
```

### Native app (a real GPU window) in three commands

```bash
dotnet new install eQuantic.UI.Templates
dotnet new equantic-native -n MyNativeApp
cd MyNativeApp && dotnet run
```

### Your first component

The template scaffolds this page — a component, a state field and a handler. No JavaScript,
no CSS, no markup:

```csharp
[Page("/", Title = "MyApp")]
public sealed class HomePage : StatefulComponent
{
    private int _count;

    public override VisualNode Build(ComponentContext context) =>
        Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = context.Theme.Background,
            Padding = EdgeInsets.All(Space.S6),
        },
        Column(gap: Space.S4, children: [
            Text("MyApp", TypeRole.Display, context.Theme.TextPrimary),

            // Your own component. Its factory is generated from the component itself,
            // so it composes exactly like the framework's.
            StatTile("Count", $"{_count}"),

            Row(gap: Space.S3, children: [
                Button("Count", onPressed: () => SetState(() => _count++)),
                Button("Reset", Variant.Outline, onPressed: () => SetState(() => _count = 0)),
            ]),
        ]));
}
```

No markup language, no builder ceremony, and no `new` — just C# expressions. Every name there is a
factory in scope everywhere: the framework's, and your own components'. Because styles are typed
values instead of CSS strings, the compiler checks the whole interface, layout and styling
included.

The **same class** serves as a server-rendered, hydrated web page and as a native screen — the
target is a project setting, not a rewrite.

---

## Features

### Write-once components

One abstract vocabulary (`Box`, `Row`, `Column`, `Text`, `Button`, `TextEntry`, `ScrollView`,
`Stack`, `Overlay`, …) with two realizers. Layout, selection marks, focus rings and editing
carets are computed in shared C#, so both targets are identical **by construction** — parity is
enforced by a cross-target test harness and a pixel golden suite.

### Styling without CSS

Components declare typed values — `BoxStyle`, `ColorToken`, `Space`, `TypeRole`, `EdgeInsets` —
and the engine does the rest:

- **Web**: every declaration becomes one **atomic CSS rule**, deduplicated app-wide (the 100th
  card adds zero bytes of CSS). SSR and the client hash declarations identically, so hydration
  never repaints; new styles appearing at runtime insert their rule exactly once.
- **Native**: the same values resolve to dp and GPU paint. No CSS exists on this path at all.
- Hover/focus become real CSS pseudo-classes on the web — interaction visuals with zero JS.
- Theming is one line: provide an `IAppTheme` (`MaterialTheme.FromSeed(...)` rebrands the whole
  app, light and dark, from a single seed color).

### Server Actions — RPC without ceremony

```csharp
[Page("/todos")]
public class TodoList : StatefulComponent
{
    [ServerAction]
    public async Task<List<Todo>> LoadTodos()
    {
        using var db = new AppDbContext();
        return await db.Todos.ToListAsync();
    }
}
```

Only `[ServerAction]` methods are callable from the client (allowlist), with `[Authorize]` RBAC
enforced before execution, payload limits and type allowlisting.

### The Photon engine (native track)

- **Metal and Vulkan backends** over a shared RHI, with a CPU reference backend as the normative
  core — backends are held to ±1 LSB parity against it.
- Shells for **macOS, iOS and Android**; real text (CoreText), real input: keyboard with a single
  focus order, IME composition (dead keys, CJK), per-path gestures, mouse cursors, clipboard.
- **Accessibility**: a shared semantics tree drives the native bridges (VoiceOver on macOS —
  labels, activation, slider adjustment).
- Steady-state frames allocate under **72 KB** with frame recycling on, pinned by a perf harness
  with regression ceilings.

### Component library

Buttons, inputs, selection controls, cards, lists, tabs, dialogs, drawers, menus, toasts — plus
heavyweights authored once and running on both targets:

- **Spreadsheet**: Excel-grade interaction — cell/range/row/column selection, in-cell editing,
  fill handle with directional pour, ⌘D/⌘R, TSV clipboard that round-trips with Excel, drag
  resize, sparse undo/redo.
- **Code editor**: line-based document model, incremental highlighting for six languages,
  word-aware undo, find, bracket matching, virtualization.
- **ListView**: windowed recycling — a thousand rows emit the draw commands of a screenful.

### Developer experience

- **Hot reload on both targets** — edit C#, the browser page and the native window update in place.
- Next.js-style **error overlay with the C# stack trace**, mapped through source maps.
- True 404/500 pages, SEO metadata (`IHandleMetadata`), per-route lazy loading.
- `dotnet watch` is the dev loop; the embedded toolchain does the rest.

---

## How It Works

### Web pipeline

```
dotnet build
    ↓
Roslyn parses your components (.cs)
    ↓
eqc transpiles C# → TypeScript (two-layer type checking)
    ↓
embedded Bun bundles → wwwroot/_equantic/*.js (per-page splitting)
    ↓
ASP.NET Core serves SSR pages; the client hydrates and takes over
```

### Native pipeline

```
dotnet build
    ↓
the same components compile as .NET
    ↓
PhotonHost lays out (own C# flex engine) and realizes a display list
    ↓
Metal / Vulkan present GPU frames in a real window (macOS/iOS/Android shells)
```

Static structure resolves at build time; event handlers, state and lifecycle run client-side;
data access stays server-side behind Server Actions.

### Zero external dependencies

The platform Runtime packages embed the Bun binary; the SDK orchestrates everything through
MSBuild. `dotnet build` is the entire toolchain — no Node.js, no npm, no bundler configuration.

### Self-contained package architecture

Each package owns its artifacts and the SDK wires them together through NuGet-generated
`$(Pkg*)` properties — independent versioning, no artifact duplication. See
[Package Architecture](https://github.com/equantic/equantic-ui/wiki/PackageArchitecture).

---

## Supported C# Features

Fidelity is enforced by a **conformance harness** (500+ cases) that runs each C# construct as both
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
| **Collections** | `List`, `Dictionary` (string/number keys → object; **record/struct/tuple keys → structural `valueMap`**), `HashSet`, `Queue`, `Stack`, `LinkedList`, sorted family (`SortedSet`/`SortedDictionary`/`SortedList`) |
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

```
src/
├── eQuantic.UI.Core/           # Core abstractions (IComponent, HtmlElement — the web escape hatch)
├── eQuantic.UI.Primitives/     # The abstract visual vocabulary + design tokens (zero deps)
├── eQuantic.UI.Components/     # WRITE-ONCE component library (one source, both targets)
├── eQuantic.UI.Compiler/       # Roslyn-based C# → TypeScript transpiler (eqc)
├── eQuantic.UI.Sdk/            # MSBuild SDK for web projects
├── eQuantic.UI.Sdk.Native/     # MSBuild SDK for Photon projects
├── eQuantic.UI.Server/         # ASP.NET Core SSR + Server Actions
├── eQuantic.UI.Runtime/        # TypeScript browser runtime (reconciler, state, atomizer)
├── eQuantic.UI.Runtime.*/      # Platform Bun bundles (Osx64, Win64, Linux64)
├── eQuantic.UI.Native.*        # Photon: engine (RHI, Metal, Vulkan), framework, shells
├── eQuantic.UI.Templates/      # dotnet new equantic-app / equantic-native
└── eQuantic.Build/             # MSBuild build tasks
```

---

## Documentation

- [📚 Wiki Home](https://github.com/equantic/equantic-ui/wiki) — complete documentation
- [🚀 Getting Started](https://github.com/equantic/equantic-ui/wiki/GettingStarted)
- [🧬 Write-Once Components](https://github.com/equantic/equantic-ui/wiki/WriteOnceComponents) — the architecture
- [🎇 Photon Engine](https://github.com/equantic/equantic-ui/wiki/Photon) — the native GPU track
- [🧩 Components](https://github.com/equantic/equantic-ui/wiki/Components) — the catalog
- [🎨 Styling](https://github.com/equantic/equantic-ui/wiki/Styling) — typed styles, atomic CSS, theming
- [⚙️ Compiler](https://github.com/equantic/equantic-ui/wiki/Compiler) — C# → JavaScript
- [🔨 Build Flow](https://github.com/equantic/equantic-ui/wiki/BuildFlow) • [⚡ Runtime](https://github.com/equantic/equantic-ui/wiki/Runtime) • [🐛 Debugging](https://github.com/equantic/equantic-ui/wiki/Debug)
- [🗺️ Roadmap](https://github.com/equantic/equantic-ui/wiki/Roadmap) — what's ahead
- [CLAUDE.md](CLAUDE.md) — technical reference for contributors

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
