<h1 align="center">eQuantic.UI</h1>

<p align="center">
  <strong>One C# codebase. Real web. Real native.</strong>
</p>

<p align="center">
  Write components once in C#. On the web they compile to optimized JavaScript at build time — no WASM.<br/>
  Natively they render through <strong>Photon</strong>, our own GPU engine on Metal and Vulkan — no WebView, no Skia.<br/>
  And the part of the vocabulary an email can hold renders as email-safe HTML, with its plain-text twin.
</p>

<p align="center">
  <a href="https://github.com/eQuantic/equantic-ui/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/equantic/equantic-ui/ci.yml?branch=main" alt="Build Status" /></a>
  <a href="https://www.nuget.org/packages/eQuantic.UI.Sdk"><img src="https://img.shields.io/nuget/vpre/eQuantic.UI.Sdk?label=nuget%20preview" alt="NuGet (preview)" /></a>
  <a href="https://www.nuget.org/packages/eQuantic.UI.Sdk"><img src="https://img.shields.io/nuget/dt/eQuantic.UI.Sdk?label=downloads" alt="NuGet downloads" /></a>
  <a href="https://github.com/equantic/equantic-ui/blob/main/LICENSE"><img src="https://img.shields.io/github/license/equantic/equantic-ui" alt="License" /></a>
  <a href="https://github.com/equantic/equantic-ui/stargazers"><img src="https://img.shields.io/github/stars/equantic/equantic-ui?style=flat" alt="GitHub stars" /></a>
</p>

<p align="center">
  <strong><a href="https://ui.equantic.tech/playground">Try it in your browser →</a></strong><br/>
  <sub>Write a component in C#, press Run, and watch it render, compiled by the eqc deployed with the playground, which can trail the newest preview. Nothing to install.</sub>
</p>

<p align="center">
  <a href="#quick-start">Quick Start</a> •
  <a href="#why-equanticui">Why eQuantic.UI</a> •
  <a href="#features">Features</a> •
  <a href="#c-that-behaves-like-c">C# fidelity</a> •
  <a href="#how-it-works">How It Works</a> •
  <a href="#documentation">Documentation</a>
</p>

---

> **⚠️ Development Preview**
>
> eQuantic.UI is in active development and published to nuget.org as **prereleases**
> (`0.2.0-preview.*`). `dotnet new install eQuantic.UI.Templates` and the Quick Start below are
> the way in; you do not need to build from source. Each preview's breaking changes, with the line
> to write instead, are on the [Upgrading](https://github.com/equantic/equantic-ui/wiki/Upgrading)
> page. Early adopters and feedback are very welcome!

---

## Why eQuantic.UI?

| | Blazor WASM | JavaScript frameworks | **eQuantic.UI** |
|---|---|---|---|
| **Language** | C# | JavaScript / TypeScript | **C#, end to end** |
| **Web payload** | the .NET runtime, downloaded | varies | **plain JavaScript and a small runtime**, whose gzipped size is a budget the test suite pins, and per-page code splitting |
| **Native apps** | a separate MAUI codebase | a separate React Native / Electron app | **the same components**, rendered by Photon on macOS, iOS, Android and Windows |
| **Styling** | CSS / Razor | CSS-in-JS / utility classes | **typed C#, no CSS authored**, with atomic classes generated |
| **Server calls** | SignalR setup | REST / GraphQL setup | **built-in RPC** (`[ServerAction]`) |
| **Toolchain** | .NET | Node.js, npm, bundlers | **only the .NET SDK**: the bundler ships embedded |

Components are authored **once** against an abstract visual vocabulary and realized per target:
DOM and atomic CSS on the web, GPU pixels in a native window, and tables and inline styles in an
email, for the part of the vocabulary an email can hold. It is not "write once, run in a WebView":
each target gets its real rendering path.

And the SDK stays out of your way. **Your project file names the SDK and nothing else**, and
everything else is C#, `appsettings.json` and fluent configuration in `Program.cs`. This is the
one the template writes:

```xml
<Project Sdk="eQuantic.UI.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

No `PackageReference` (you add one only to opt in to something extra, an icon pack say), no
Info.plist to edit, no Android manifest, no JavaScript, no CSS. What a platform needs, the SDK
writes from a C# declaration.

---

## Quick Start

### Prerequisites

- The .NET 10 SDK. No Node.js, no npm: the TypeScript and bundling toolchain ships embedded, one
  package per OS and architecture. (An iOS build also needs Xcode, as any iOS app does.)

### A web app

```bash
dotnet new install eQuantic.UI.Templates
dotnet new equantic-app -n MyApp            # --shell blank | topnav | dashboard
cd MyApp && dotnet run
```

### A native app: a real GPU window

```bash
dotnet new install eQuantic.UI.Templates
dotnet new equantic-native -n MyNativeApp   # --shell blank | tabs | drawer | list-detail
cd MyNativeApp && dotnet run
```

### Your first page

This is the page the web template scaffolds: state, your own component, and localization, with
no JavaScript, no CSS and no markup.

```csharp
[Page("/", Title = "MyApp")]
public sealed class HomePage : StatefulComponent
{
    private int _count;

    // The languages this app ships: one resx file per entry, nothing else.
    private static readonly CultureOption[] Languages =
    [
        new("en", "English"),
        new("pt-BR", "Português"),
    ];

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        return Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
            Padding = EdgeInsets.All(Space.S6),
        },
        Column(gap: Space.S4, children: [
            Text("MyApp", TypeRole.Display, theme.TextPrimary, maxLines: 1),
            Text(Strings.Tagline, TypeRole.BodyM, theme.TextSecondary, maxLines: 2),

            // Re-renders this page in the chosen language, in place, with no reload.
            CultureSwitcher(Languages),

            // Your own component, composed exactly like the framework's.
            Row(gap: Space.S3, children: [
                StatTile("Count", $"{_count}"),
                StatTile("Doubled", $"{_count * 2}"),
            ]),

            Row(gap: Space.S3, children: [
                Button("Count", onPressed: () => SetState(() => _count++)),
                Button("Reset", Variant.Outline, onPressed: () => SetState(() => _count = 0)),
            ]),

            // The build checks this {0} against every language's resx: a translation that asks
            // for {1} fails the BUILD, not a visitor.
            Text(string.Format(Strings.CountedTimes, _count), TypeRole.Caption, theme.TextMuted,
                maxLines: 1),
        ]));
    }
}
```

Every name in the tree is a **factory** in scope everywhere, the framework's and your own
components' alike: `StatTile` lives in the app, and a source generator writes its factory from
the component itself. Styles are typed values rather than CSS strings, so the compiler checks
the whole interface, layout and styling included.

The **same class** serves as a server-rendered, hydrated web page and as a native screen. The
target is a project setting, not a rewrite.

---

## Features

### Write-once components

One abstract vocabulary (`Box`, `Row`, `Column`, `Stack`, `Text`, `TextEntry`, `ScrollView`,
`Overlay`, …) and three realizers: the **web** (server-rendered DOM, hydrated in the browser),
**Photon** (GPU pixels) and **email** (tables and inline styles for the columns, rows, text, boxes,
images and links an email can hold, and its plain-text part from the same tree, while anything
else is refused). The component library is
authored once against that vocabulary. Selection marks, focus rings and editing carets are
computed in shared C#, and the two realizers are held to each other by cross-pinned fixtures and
a suite of native golden images.

### Styling without CSS

Components declare typed values (`BoxStyle`, `ColorToken`, `Space`, `TypeRole`, `EdgeInsets`) and
each realizer turns them into its native form:

- **Web**: every declaration becomes one **atomic CSS rule**, deduplicated app-wide, so the
  hundredth card adds no CSS. The server and the client hash declarations identically, so
  hydration never repaints. Hover and focus become real CSS pseudo-classes: interaction visuals
  with no JavaScript.
- **Native**: the same values resolve to device pixels and GPU paint. No CSS exists on this path.
- **Theming** is one `IAppTheme`. Add the Material package and `MaterialTheme.FromSeed(...)`
  rebrands the whole app, light and dark, from a single seed color, with Material 3 dynamic color.

### Server Actions: RPC without ceremony

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

The browser calls it like a method. Only `[ServerAction]` methods are callable (an allowlist),
`[Authorize]` RBAC runs before the body does, and payloads are size-limited and type-checked.

### Localization the .NET way, checked at build

- **resx**, exactly as .NET does it: the server resolves a key in the request's culture, and the
  browser resolves the same key from the catalog the build emitted.
- **Every translation is validated at build time.** A culture whose template asks for an argument
  the call does not pass fails the build (EQ2100, EQ2101), not a visitor in one language.
- **Culture routes** (`/pt-BR/...`) and `hreflang` alternate links from one declaration, and a
  switcher that changes language in place.
- **Culture-exact formatting**: `{0:C2}` prints the same on the server and in the browser, and a
  culture the browser cannot reproduce is a build error rather than a surprise.

### Photon: the native GPU engine

- **Metal and Vulkan** backends over one rendering interface, and a CPU **reference** backend that
  is the normative one: the GPU backends are held to it pixel by pixel, within a per-channel
  tolerance.
- **Four shells**: macOS, iOS, Android and Windows (Win32). Real text on each platform (CoreText
  on Apple, DirectWrite on Windows, the platform text layout on Android) and real input: keyboard
  with a single focus order, IME composition (dead keys, CJK), cursors and clipboard on the
  desktop, and gestures tracked across frames.
- **Accessibility** from one shared semantics tree: VoiceOver on macOS and iOS, TalkBack on
  Android.
- **Platform capabilities declared in C#**: `builder.Capabilities.UseCamera("why")` and its
  siblings, entitlements and bundle facts. The SDK writes the Info.plist, the Android manifest
  and the entitlements, and adds the ones .NET itself needs on its own.
- **Device services** taken by constructor: camera, location, biometrics, photos, network, motion,
  deep links, file dialogs, storage and a secret store.
- **App icons generated** from one declared asset: every size, the web manifest and a Windows icon.
- A **performance harness** pins what a steady-state frame allocates, with regression ceilings.

### A component library that goes past buttons

Buttons, inputs, selection controls, cards, lists, tabs, dialogs, drawers, menus and toasts, and
heavyweights authored once:

- **Spreadsheet**: cell, range, row and column selection, in-cell editing, a fill handle with
  directional pour, ⌘D and ⌘R, a TSV clipboard that round-trips with Excel, drag resize, and sparse
  undo and redo.
- **Code editor**, on an engine of its own (`eQuantic.UI.Code`): a line-based document, incremental
  highlighting for C#, TypeScript/JavaScript, Python, JSON and XML (and any language an app
  registers), find, bracket matching and virtualization.
- **Forms** with one validation model on the server, the browser and native: DataAnnotations,
  bridged, and `[FormModel]` generating the plumbing.
- **Charts**: a write-once `BarChart`, themed and accessible on both targets, plus Chart.js and
  ApexCharts wrappers for the web.
- **Date and time pickers**, a calendar, data tables, **Markdown** and **Mermaid** diagrams (no
  mermaid.js), code blocks and a cookie-consent card, with no JavaScript libraries behind them.
- **ListView** with windowed recycling: ten thousand rows emit the draw commands of a screenful.
- **Twelve icon packs** (Lucide, Heroicons, Material Symbols, Font Awesome, Phosphor, Tabler, and
  more), typed and trimmable, on both targets.
- On the web: image optimization (server-side resize, WebP, blur placeholders), Lottie
  animations, and analytics with Google Tag Manager behind a consent card.

### Developer experience

- **Hot reload** on both targets. A native window is patched in place with its state intact; a web
  page is recompiled and reloaded with its state replayed.
- **Stack traces in C#**: a development error overlay rewrites the call stack into C# frames, and
  the source maps are per statement, so a frame or a breakpoint lands on the C# line that threw.
  A Debug build writes the maps, and a Release build ships none.
- A **visual editor for VS Code**: live preview, click to select, an inspector, and structural
  edits written back to your C# source.
- **Every diagnostic documented**: each EQ code has its meaning and its fix in the
  [catalogue](https://github.com/equantic/equantic-ui/wiki/Diagnostics).
- True 404 and 500 pages, SEO metadata (`IHandleMetadata`), per-route lazy loading, and npm
  packages declared in the csproj and installed by the embedded Bun, with no Node.js.

---

## C# that behaves like C#

A C# expression means the same thing in the browser as on the server, and that is **measured**:
a conformance harness runs each construct as transpiled JavaScript and as real .NET, and asserts
identical results, across well over a thousand cases. Every construct resolves to one of three
mechanisms: a native JavaScript strategy, a faithful `$eq.*` runtime helper, or a **build error**
when it is genuinely impossible. Nothing miscompiles silently.

| Category | Supported |
|----------|-----------|
| **Language, up to C# 15** | C# 13 `params` collections and `\e`; C# 14 null-conditional assignment, `field`-backed properties, extension members and `nameof(List<>)`; C# 15 labeled `break`/`continue`, `union` declarations and `closed` hierarchies |
| **Numbers, exactly** | fixed-width integers wrap as their type does (`byte`, `short`, `uint`), `checked` throws, `float` rounds to single precision where it is produced, `decimal` is exact base 10, `long`/`ulong` are BigInt, and an integer or decimal division by zero throws, as .NET's does (a double's is an infinity or NaN) |
| **Nullable** | `Nullable<T>` with lifted operators: arithmetic, compound assignment and increments keep null and their type's rule (a `float?` rounds, a `byte?` wraps) |
| **Pattern matching** | type, property, positional, relational, list and slice patterns, and `and`/`or`/`not` |
| **Types** | `record`/`struct`/value tuples with structural equality and `with`; records emit as JavaScript classes with their methods, inheritance and generics; user-defined operators and conversions on your own types |
| **Text** | .NET's own number and date text, standard and custom format specifiers, alignment, `string.Format` with a format provider, `StringBuilder`, and `StringComparison`-aware comparisons (ordinal and ignore-case) |
| **Collections and LINQ** | `List`, `Dictionary` (structural keys for records and tuples), `HashSet`, `Queue`, `Stack`, `LinkedList`, the sorted family; LINQ from `Select` to `GroupJoin`, with stable composite ordering |
| **Date and time** | `DateTime`, `DateTimeOffset`, `TimeSpan`, `DateOnly`, `TimeOnly`, tick-precise |
| **Control flow** | `if`, `switch`, every loop, local functions, `using` statements and declarations, `try`/`catch`/`finally`, `async`/`await` (`Task<T>` → `Promise<T>`) |

> Constructs with no JavaScript equivalent (pointers, `goto`, client-side `System.IO` or
> `System.Net.Http`) fail the build with a canonical diagnostic. The full matrix is on the
> [Supported Features](https://github.com/equantic/equantic-ui/wiki/SupportedFeatures) page, and
> the [.NET coverage program](docs/DOTNET-COVERAGE-PROGRAM.md) is how it keeps growing.

---

## How It Works

### Web pipeline

```
dotnet build
    ↓
Roslyn reads your components (.cs), with the project's full semantic model
    ↓
eqc transpiles C# → TypeScript, one strategy per construct, through an IR with one writer per level
    ↓
the embedded Bun bundles → wwwroot/_equantic/*.js (per-page splitting)
    ↓
ASP.NET Core serves server-rendered pages; the browser hydrates and takes over
```

### Native pipeline

```
dotnet build
    ↓
the same components compile as .NET
    ↓
Photon lays them out (its own C# flex engine) and realizes a display list
    ↓
Metal or Vulkan present GPU frames in a real window (macOS, iOS, Android, Windows)
```

Static structure resolves at build time; event handlers, state and lifecycle run in the browser;
data access stays on the server behind Server Actions.

### Zero external dependencies

The Bun binary ships inside one package per OS and architecture, and the SDK picks the right one
itself. `dotnet build` is the entire toolchain: no Node.js, no npm, no bundler configuration.

### One version, self-contained packages

Every package ships at one version, and the SDK pins its siblings to its own. Each package owns
its artifacts and the SDK wires them together through NuGet's `$(Pkg*)` properties, with no
artifact duplicated: the Server embeds the runtime it serves and ships the same bytes for the
build to copy. See [Package Architecture](https://github.com/equantic/equantic-ui/wiki/PackageArchitecture).

---

## Project Structure

```
src/
├── eQuantic.UI.Primitives/     # The abstract visual vocabulary, tokens and contracts (zero deps)
├── eQuantic.UI.Components/     # WRITE-ONCE component library (one source, every target)
├── eQuantic.UI.Code/           # The code editing engine: document, selection, history, languages
├── eQuantic.UI.Charts/         # WRITE-ONCE charts (BarChart, …)
├── eQuantic.UI.Web/            # Web realizer (SSR lowering) + the DOM escape hatch (HtmlElement)
├── eQuantic.UI.Server/         # ASP.NET Core host: SSR, Server Actions, routing, assets, and the served runtime.js
├── eQuantic.UI.Email/          # Email realizer (tables and inline styles)
├── eQuantic.UI.Runtime/        # The TypeScript browser runtime's source and tests (reconciler, state, router)
├── eQuantic.UI.Runtime.*/      # Embedded Bun, one package per OS+arch (Osx64, OsxArm64, Win64, WinArm64, Linux64, LinuxArm64)
├── eQuantic.UI.Compiler/       # Roslyn-based C# → TypeScript transpiler (the library)
├── eQuantic.Build/             # eqc, the transpiler CLI the SDK runs
├── eQuantic.UI.Codegen/        # Writers for generated files (one CodeWriter, one writer per file type)
├── eQuantic.UI.Web.Build/      # Generators of the runtime's TypeScript twins (design system, enum unions, icons, strings)
├── eQuantic.UI.Generators/     # Source generator: the declarative factory surface for an app's own components
├── eQuantic.UI.Sdk/            # MSBuild SDK for web apps
├── eQuantic.UI.Sdk.Native/     # MSBuild SDK for Photon apps
├── eQuantic.UI.Native.*        # Photon: Engine (+ Metal, Vulkan, Reference), Framework (layout), Components (realizer, host),
│                               # Hosting (application builder), Build (eqicon), Generators, and the shells
│                               # (Shell.Apple, Shell.MacOS, Shell.iOS, Shell.Android, Shell.Windows)
├── eQuantic.UI.Design*/        # The visual editor's design host
├── eQuantic.UI.Templates/      # dotnet new equantic-app / equantic-native
├── eQuantic.UI.Material/       # Material 3 theme (dynamic color)
├── eQuantic.UI.<Pack>/         # Icon catalogs (Lucide, Heroicons, Tabler, …), Gtm, Images, Lottie, Charts.ChartJs/.ApexCharts
extensions/vscode/              # The VS Code extension: the visual editor
docs/                           # The measured audits and the plans; docs/README.md is the index
```

---

## Documentation

- [📚 Wiki Home](https://github.com/equantic/equantic-ui/wiki), in English and Brazilian Portuguese
- [🚀 Getting Started](https://github.com/equantic/equantic-ui/wiki/GettingStarted)
- [🧬 Write-Once Components](https://github.com/equantic/equantic-ui/wiki/WriteOnceComponents), the architecture
- [🎇 Photon Engine](https://github.com/equantic/equantic-ui/wiki/Photon), the native GPU track
- [🧩 Components](https://github.com/equantic/equantic-ui/wiki/Components), the catalog, and [Forms](https://github.com/equantic/equantic-ui/wiki/Forms), [Charts](https://github.com/equantic/equantic-ui/wiki/Charts) and the [Code Editor](https://github.com/equantic/equantic-ui/wiki/CodeEditor)
- [🎨 Styling](https://github.com/equantic/equantic-ui/wiki/Styling): typed styles, atomic CSS and theming
- [🌍 Localization](https://github.com/equantic/equantic-ui/wiki/Localization): resx, culture routes and build-time validation
- [📱 Capabilities](https://github.com/equantic/equantic-ui/wiki/Capabilities): the device services and the platform declarations
- [⚙️ Compiler](https://github.com/equantic/equantic-ui/wiki/Compiler), [Supported Features](https://github.com/equantic/equantic-ui/wiki/SupportedFeatures) and [Diagnostics](https://github.com/equantic/equantic-ui/wiki/Diagnostics)
- [🔨 Build Flow](https://github.com/equantic/equantic-ui/wiki/BuildFlow) • [⚡ Runtime](https://github.com/equantic/equantic-ui/wiki/Runtime) • [🐛 Debugging](https://github.com/equantic/equantic-ui/wiki/Debug) • [🖌️ Visual Editor](https://github.com/equantic/equantic-ui/wiki/VisualEditor)
- [⬆️ Upgrading](https://github.com/equantic/equantic-ui/wiki/Upgrading), each preview's breaking changes, and the [🗺️ Roadmap](https://github.com/equantic/equantic-ui/wiki/Roadmap)
- [📐 docs/](docs/README.md), the architecture, measured: the [architecture audit](docs/ARCHITECTURE-AUDIT.md), [how Flutter solves each of these](docs/FLUTTER-PARITY.md), and the plans in flight
- [CLAUDE.md](CLAUDE.md), the technical reference for contributors

---

## Contributing

Contributions are welcome. I am especially interested in collaborating with developers who want to
work on transpiler correctness (C# that must behave identically in .NET and in the browser), layout
parity between the web and Photon realizers, the Photon engine itself — text shaping across CoreText,
DirectWrite and Android's layout, the Metal and Vulkan backends, and the macOS, iOS, Android and
Windows shells — accessibility bridges, and making the write-once contract hold on more host and
target combinations. The [architecture audit](docs/ARCHITECTURE-AUDIT.md) includes an order of
attack that is also a list of open work, each item measured and sized.

See the [Contributing Guide](CONTRIBUTING.md) for the mechanics.

- [Code of Conduct](CODE_OF_CONDUCT.md)
- [Technical Reference](CLAUDE.md)

---

## License

MIT © [eQuantic Tech](https://github.com/eQuantic)

---

<p align="center">
  <sub>Built with C# and a lot of ☕ by the eQuantic team</sub>
</p>
