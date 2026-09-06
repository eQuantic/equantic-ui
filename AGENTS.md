# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## Git Commit Guidelines

**CRITICAL**: NEVER add co-authorship lines to commit messages. Do NOT include:

```text
Co-Authored-By: Codex Sonnet 4.5 <noreply@anthropic.com>
```

All commits must be authored solely by the repository owner without any co-author attribution.

## Project Overview

**eQuantic.UI** is a Flutter-inspired component-based UI framework for .NET that compiles C# components directly to optimized JavaScript at build time (not WASM). It provides type-safe, HTML-native components with a small runtime (its gzip size is measured by the site's build and not quoted here — it changes every release).

### Core Principles

1. **100% .NET** - Zero external runtime dependencies (Node.js, npm, etc.)
2. **Self-Contained** - ASP.NET Core serves and compiles everything
3. **Compiler-First** - C# → TypeScript → JavaScript (two-layer type checking)
4. **Performant** - Intelligent compilation (static vs dynamic), tree-shaking, code splitting

## Build Commands

```bash
# Build the entire solution
dotnet build

# Build in Release mode
dotnet build --configuration Release

# Run all tests (.NET)
dotnet test

# Run a specific test project
dotnet test tests/eQuantic.UI.Compiler.Tests

# Run a single test by name
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Pack NuGet packages
dotnet pack --configuration Release --output nupkgs
```

### TypeScript Runtime (src/eQuantic.UI.Runtime)

The TypeScript runtime is built using **embedded Bun** (bundled in platform-specific runtime packages). Bun binaries are stored as `.zip` files and auto-extracted during build.

```bash
cd src/eQuantic.UI.Runtime

# Build TypeScript runtime (using embedded Bun - auto-extracted if needed)
# macOS:   src/eQuantic.UI.Runtime.OsxArm64 (Apple Silicon) or .Osx64 — tools/bun/bun-darwin
# Linux:   src/eQuantic.UI.Runtime.LinuxArm64 or .Linux64 — tools/bun/bun-linux
# Windows: src/eQuantic.UI.Runtime.WinArm64 or .Win64 — tools/bun/bun.exe

# Run TypeScript tests
npm run test           # vitest

# Lint and format
npm run lint           # eslint
npm run format         # prettier
```

### Development Workflow (building the samples)

The samples build against the framework PROJECTS, never against packages. `Sdk.props` sees the source
tree beside it (`IsEQuanticDevMode`) and swaps every `PackageReference` for a `ProjectReference`;
`Sdk.targets` runs the `eqc` and `eqicon` the graph just built (`_EqSourceTree`). The two tools are
`ProjectReference` edges with `ReferenceOutputAssembly=false`, so a cold `dotnet build` orders them
itself — no bootstrap pack, no local feed, no cache step between an edit and the sample that shows it:

```bash
dotnet build samples/DefaultUIDashboard   # web; PhotonDesktop and WalletMobile are the native heads
```

`runtime.js` in a source-tree build is `src/eQuantic.UI.Runtime/dist/index.js`, which the Runtime
project's `GenerateRuntimeBundle` target regenerates with the embedded bun (from
`src/eQuantic.UI.Sdk/Resources/boot.ts`) whenever a runtime `.ts` changed — part of the same graph.

To validate a change through the REAL consumer path — packages, `global.json`, restore — pack a local
feed and consume it from an app; `dotnet msbuild -t:ClearEQuanticCache` clears every `equantic.*`
package from the NuGet cache between repacks. The recipe is the wiki's
[BuildFlow](https://github.com/equantic/equantic-ui/wiki/BuildFlow) page, "Consuming an unreleased
SDK from local packages". `artifacts/packages/` is part of neither flow: the Debug auto-pack that once
targeted it never ran and is gone.

## Architecture

### Project Structure

```
src/
├── eQuantic.UI.Primitives/     # Abstract visual vocabulary, tokens and the contract attributes (zero deps)
├── eQuantic.UI.Components/     # WRITE-ONCE component library (authored against Primitives; realized per target)
├── eQuantic.UI.Web/            # WEB REALIZER + the DOM escape hatch (HtmlElement, HtmlNode, ClassBuilder)
├── eQuantic.UI.Server/         # ASP.NET Core SSR, Server Actions, metadata and assets
├── eQuantic.UI.Compiler/       # Roslyn-based C# to JavaScript transpiler (the library)
├── eQuantic.Build/             # eqc — the transpiler CLI the SDK runs; ships as tools/net10.0/eqc.dll
├── eQuantic.UI.Generators/     # Source generator: the declarative factory surface for an app's own components
├── eQuantic.UI.Sdk/            # MSBuild SDK for web apps (Sdk.props, Sdk.targets, Resources/boot.ts)
├── eQuantic.UI.Runtime/        # TypeScript browser runtime (reconciler, state, events) → runtime.js
├── eQuantic.UI.Runtime.*/      # Embedded Bun, one package per OS+arch (Osx64, OsxArm64, Win64, WinArm64, Linux64, LinuxArm64)
├── eQuantic.UI.Native.*/       # PHOTON, the native track: Engine (+ .Metal/.Vulkan/.Reference backends),
│                               # Framework, Components, Hosting, Build (eqicon — vectors, app icons, manifests;
│                               # ships as tools/net10.0/eqicon.dll), Generators, and the shells —
│                               # Shell.Apple (shared Apple code), Shell.MacOS, Shell.iOS, Shell.Android,
│                               # Shell.Windows (Win32 + DirectWrite/Direct2D/WIC; Vulkan or the Reference backend)
├── eQuantic.UI.Sdk.Native/     # MSBuild SDK for Photon apps: picks the shell per TFM and, on desktop, per host OS
├── eQuantic.UI.Templates/      # dotnet new equantic-app / equantic-native
├── eQuantic.UI.Codegen/        # Writers for generated files (one CodeWriter, one writer per file type)
├── eQuantic.UI.Web.Build/      # Generators of the runtime's TypeScript twins (design system, enum unions, icons, SDK strings)
├── eQuantic.UI.Design*/        # The visual editor's design host
└── eQuantic.UI.<Pack>/         # Icon catalogs (Lucide, Heroicons, …), Charts*, Gtm, Images, Lottie, Email, Material
```

### Package Architecture (Self-Contained Design)

eQuantic.UI follows a **self-contained package architecture** where each package manages its own artifacts:

**Key Packages:**

- **eQuantic.UI.Runtime** - Packages `runtime.js` at `tools/runtime/runtime.js`
- **eQuantic.UI.Components** - Packages C# source files at `tools/source/*.cs` for compiler type resolution
- **eQuantic.UI.Sdk** - Orchestrates build, references other packages via `$(PkgeQuantic_UI_*)` NuGet properties, ships `eqc` and `eqicon` under `tools/net10.0/`

**Design Principles:**

1. ✅ Each package is self-contained (no embedding of other packages' artifacts)
2. ✅ SDK references packages via NuGet-generated `$(Pkg*)` properties
3. ✅ Reads whatever version NuGet resolved — the packages ship at ONE version and the SDK pins its siblings to its own
4. ✅ No artifact duplication across packages
5. ✅ Clear interfaces between packages

**Example Resolution:**

```xml
<!-- Auto-generated by NuGet in obj/*.nuget.g.props — only for a PackageReference with GeneratePathProperty="true" -->
<PkgeQuantic_UI_Runtime>~/.nuget/packages/equantic.ui.runtime/<version></PkgeQuantic_UI_Runtime>
<PkgeQuantic_UI_Components>~/.nuget/packages/equantic.ui.components/<version></PkgeQuantic_UI_Components>

<!-- Used in Sdk.targets; beside a source tree no $(Pkg*) is set and the tree's dist/ and sources are the fallback -->
<_RuntimeSourcePath>$(PkgeQuantic_UI_Runtime)/tools/runtime/runtime.js</_RuntimeSourcePath>
<_StandardComponentsDir>$(PkgeQuantic_UI_Components)/tools/source</_StandardComponentsDir>
```

See [wiki/PackageArchitecture](https://github.com/equantic/equantic-ui/wiki/PackageArchitecture) for complete documentation.

### Build Pipeline

```
dotnet build
    ↓
MSBuild: CompileEQuanticUI (BeforeTargets="Build")
    ↓
1. Roslyn parse /Pages/**/*.cs + Components source from $(PkgeQuantic_UI_Components) (or src/eQuantic.UI.Components beside a source tree)
2. Detect StatefulComponent/StatelessComponent classes
3. Generate TypeScript intermediate (.ts files)
4. Invoke embedded Bun for bundling
5. CopyEQuanticRuntime: Copy runtime.js + equantic.css from $(PkgeQuantic_UI_Runtime) (or the tree's dist/index.js)
6. Output: wwwroot/_equantic/
   ├─ runtime.js (from Runtime package)
   └─ *.js (compiled components)
```

### Compilation Strategy

**Static Shell** (build-time): Component structure, layout, styles, initial state, routing metadata
**Dynamic Logic** (client-side): Event handlers, state mutations, computed properties, lifecycle hooks
**Server Actions** (server-side): Database queries, business logic, authentication

### Compiler Components (eQuantic.UI.Compiler)

The Roslyn-based compiler uses the **Strategy Pattern**:

1. **ComponentParser** (`Parser/ComponentParser.cs`) - Parses C# AST
2. **CSharpToJsConverter** (`CodeGen/CSharpToJsConverter.cs`) - Main conversion orchestrator
3. **TypeScriptEmitter** (`CodeGen/TypeScriptEmitter.cs`) - TypeScript code generation
4. **Strategies** (`CodeGen/Strategies/`) - Individual converters for C# constructs:
   - `Expressions/` - Binary, member access, invocation, object creation
   - `Statements/` - If, switch, while, foreach, try-catch, using
   - `Linq/` - Where→filter, Select→map, First→find, Count→length
   - `Types/` - Enum, Guid, Nullable, Tuple
5. **SourceMapGenerator** - V3 Source Maps for C# debugging in browser
6. **TypeMappingRegistry** - Data-driven type/method translations

**Supported C# Features:**
- Expressions: Arithmetic, Logical, Ternary, Null-coalescing (`??`)
- Control Flow: `if`, `switch`, `for`, `foreach`, `while`
- Modern Patterns: Recursive, Property, Positional, Relational (C# 9-12)
- Resource Management: `using` statements and `using var`
- Exceptions: `try-catch-finally`
- LINQ: Direct conversion to JS equivalents
- Async/Await: `Task` → `Promise`

### Runtime Architecture (eQuantic.UI.Runtime)

```
src/
├── core/
│   ├── component.ts       # Component base class
│   ├── types.ts           # HtmlNode, EventHandler types
│   ├── server-actions.ts  # Server method invocation
│   └── service-provider.ts
├── dom/
│   ├── renderer.ts        # DOM rendering
│   └── reconciler.ts      # Virtual DOM diffing (keyed LIS algorithm)
├── state/                 # State management
└── utils/
    └── style-builder.ts   # CVA-inspired class utility
```

**Reconciler Features:**
- Type comparison (tag changes trigger full replacement)
- Attribute diffing (only modified attributes updated)
- Keyed identity (`key` prop preserves element state during moves)
- WeakMap-based event tracking (prevents memory leaks)
- Hydration support (attaches listeners to SSR-rendered HTML)

### Bundle Strategy

What a build actually writes under `wwwroot/_equantic/` (sizes are measured by the site's build, not
quoted here):

1. **runtime.js** — virtual DOM, events, state, the server-actions bridge AND the shared component
   library, whose transpiled modules ship INSIDE it (`[RuntimeProvided]`); eqc routes
   `using eQuantic.UI.Components` imports there rather than emitting a per-app copy. The page reaches
   it as the bare module `@equantic/runtime` through the shell's import map, and the Server serves its
   own embedded copy at that route
2. **`<Component>.js` + `.js.map`** — one module per page or component, flat (a hash suffix
   disambiguates types that share a name). `boot.ts` imports the page's module dynamically on
   navigation, so per-route lazy loading falls out of the module graph — there is no `pages/` folder
   and no chunk splitting
3. **equantic.css** — the base styles, copied from the Runtime package beside runtime.js
4. **strings/`<culture>`.json** — the culture catalogs, when the app has `.resx`
5. **icons/** — the app icon sizes and the web manifest, when an `AppIcon` is declared

## Component Attributes

- `[Component]` - Marks a class as a UI component
- `[Page("/route")]` - Marks a component as a routable page
- `[ServerAction]` - Marks a method for server-side execution
- `[Authorize(Roles = "Admin")]` - RBAC authorization on server actions
- `[AllowAnonymous]` - Bypasses authorization

## Component Types

1. **StatelessComponent** - Functional components depending only on props
2. **StatefulComponent** - Components with persistent internal state (`SetState`)
3. **HtmlElement** - Low-level primitives mapping to HTML tags

## Server Actions

Server Actions are C# methods invoked directly from browser via RPC:

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

**Compiles to:**
```javascript
async loadTodos() {
    return await this._serverActions.invoke("TodoList/LoadTodos", []);
}
```

**Security:**
- Only `[ServerAction]` methods are callable (whitelist)
- `[Authorize]` enforces RBAC before execution
- Payload size limits and type whitelisting

## Styling System

Built on three pillars: Abstraction, Flexibility (Tailwind as "Happy Path"), Performance.

### StyleBuilder (CVA-inspired)

```csharp
["class"] = StyleBuilder.Create(theme?.Base)
    .Add(theme?.GetVariant(Variant))
    .Add(theme?.GetSize(Size))
    .Add(ClassName)
    .Build()
```

### Theme Types

- **Variant**: `Primary`, `Secondary`, `Destructive`, `Outline`, `Ghost`, `Link`, `Success`, `Warning`, `Info`
- **Size**: `Small`, `Medium`, `Large`, `XLarge`

### Tailwind Integration

The `eQuantic.UI.Tailwind` package generates CSS automatically at build time through the
EMBEDDED Bun (`bun x @tailwindcss/cli@<pinned>` — zero Node, zero manual targets). Consumers only
reference the package; no build configuration is required.

## Server Integration

```csharp
builder.Services.AddUI(options => {
    options.ScanAssembly(typeof(Program).Assembly)
           .ConfigureHtmlShell(shell => shell.SetTitle("App"));
});

app.UseStaticFiles();
app.UseServerActions();
app.MapUI();  // SPA routing
```

## SEO & Metadata

Components implement `IHandleMetadata` for dynamic SEO:

```csharp
public class BlogPostPage : StatelessComponent, IHandleMetadata
{
    public void ConfigureMetadata(SeoBuilder seo)
    {
        seo.Title("Blog Post Title")
           .Description("Summary...")
           .Canonical("https://example.com/post")
           .OpenGraph("type", "article");
    }
}
```

## Testing

- **.NET Tests**: xUnit with FluentAssertions (`tests/eQuantic.UI.Compiler.Tests`, `tests/eQuantic.UI.Server.Tests`)
- **TypeScript Tests**: Vitest (`src/eQuantic.UI.Runtime/src/**/*.spec.ts`)

## Version Management

Global version is defined in `Directory.Build.props`.

## Compiler Boundaries (Server vs Client)

**Client Components (StatefulComponent/StatelessComponent):**
- Allowed: UI Logic, State Management, `System.Linq`, Basic Types
- Forbidden: `System.IO`, `System.Net.Http` (direct), blocking `.Wait()`
- Bridge: Data fetching MUST use `[ServerAction]`

**The compiler validates these boundaries before emitting JS.**
