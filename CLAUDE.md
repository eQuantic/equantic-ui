# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Git Commit Guidelines

**CRITICAL — commit format**: `emoji type: description` — the emoji comes FIRST, always
(✨ feat / 🐛 fix / 📝 docs / ♻️ refactor / ✅ test / 🔧 chore / 👷 ci / ⚡ perf / 💄 style;
merges: `🔀 merge: description`). ALL commit messages MUST be written in ENGLISH — subject and
body. The two rules compose: English text, emoji prefix, no exceptions.

**CRITICAL**: NEVER add co-authorship lines to commit messages. Do NOT include:

```text
Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>
```

All commits must be authored solely by the repository owner without any co-author attribution.

## Project Overview

**eQuantic.UI** is a Flutter-inspired component-based UI framework for .NET that compiles C# components directly to optimized JavaScript at build time (not WASM). It provides type-safe, HTML-native components with a minimal runtime (~85 KB gzipped).

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
# macOS:   uses src/eQuantic.UI.Runtime.Osx64/tools/bun/bun-darwin
# Linux:   uses src/eQuantic.UI.Runtime.Linux64/tools/bun/bun-linux
# Windows: uses src/eQuantic.UI.Runtime.Win64/tools/bun/bun.exe

# Run TypeScript tests
npm run test           # vitest

# Lint and format
npm run lint           # eslint
npm run format         # prettier
```

### Shader Toolchain (Photon / native track)

The Photon shaders have ONE normative source, `src/eQuantic.UI.Native.Engine/Shaders/Sdf.slang`.
The generated `Sdf.metal` / `Sdf.spv` / `Sdf.metallib` are **committed and never hand-edited** —
this script is their only writer:

```bash
./scripts/generate-shaders.sh
```

The toolchain resolves itself: the pinned `slangc` (2026.14.1) is taken from the local cache or
downloaded and SHA-256-verified on first use (`scripts/slang-toolchain.sh`). Set `EQ_SLANGC` to
override with your own build. App developers never run this — they consume the committed
`.metallib`/`.spv`; only framework developers changing a shader do.

The `metallib` step additionally needs the Xcode Metal Toolchain
(`xcodebuild -downloadComponent MetalToolchain`); without it the script warns and leaves the
committed `metallib` untouched.

### Bootstrap Build (required before first build)

The SDK depends on other packages. Run these in order before a full solution build:

```bash
dotnet pack src/eQuantic.UI.Core/eQuantic.UI.Core.csproj --configuration Release
dotnet pack src/eQuantic.UI.Components/eQuantic.UI.Components.csproj --configuration Release
dotnet pack src/eQuantic.UI.Server/eQuantic.UI.Server.csproj --configuration Release
dotnet pack src/eQuantic.UI.Sdk/eQuantic.UI.Sdk.csproj --configuration Release
```

### Development Workflow (rebuilding with samples)

When making changes to the framework and testing with samples, the full build chain must be rebuilt:

```bash
# Option 1: Use the dev-rebuild script (recommended)
./scripts/dev-rebuild.sh              # Rebuilds all and tests with CounterApp
./scripts/dev-rebuild.sh TodoListApp  # Specify different sample

# Option 2: Manual steps
cd src/eQuantic.UI.Runtime && npm run build  # If TypeScript changed
dotnet pack -c Release                        # Pack all packages
dotnet msbuild -t:ClearEQuanticCache          # Clear NuGet cache (eQuantic only)
cd samples/CounterApp && dotnet restore --force && dotnet build

# Option 3: Clear only NuGet cache
dotnet msbuild -t:ClearEQuanticCache
```

**Why is this needed?** The samples use NuGet packages (like a real consumer would). Changes to the framework must flow through: source → pack → NuGet cache → restore → build sample.

## Architecture

### Project Structure

```
src/
├── eQuantic.UI.Core/        # Core abstractions (IComponent, HtmlElement, HtmlNode)
├── eQuantic.UI.Primitives/  # Abstract visual vocabulary + design tokens (zero deps)
├── eQuantic.UI.Components/  # WRITE-ONCE component library (authored against Primitives; realized per target)
├── eQuantic.UI.Compiler/    # Roslyn-based C# to JavaScript transpiler
├── eQuantic.UI.Sdk/         # MSBuild SDK for project integration
├── eQuantic.UI.Server/      # ASP.NET Core SSR and Server Actions
├── eQuantic.UI.Runtime/     # TypeScript browser runtime (reconciler, state, events)
├── eQuantic.UI.Runtime.*/   # Platform-specific Bun bundles (Osx64, Win64, Linux64)
├── eQuantic.UI.Tailwind/    # Tailwind CSS integration
├── eQuantic.UI.CLI/         # Developer CLI tools
└── eQuantic.Build/          # MSBuild tasks
```

### Package Architecture (Self-Contained Design)

eQuantic.UI follows a **self-contained package architecture** where each package manages its own artifacts:

**Key Packages:**

- **eQuantic.UI.Runtime** - Packages `runtime.js` at `tools/runtime/runtime.js`
- **eQuantic.UI.Components** - Packages C# source files at `tools/source/*.cs` for compiler type resolution
- **eQuantic.UI.Sdk** - Orchestrates build, references other packages via `$(PkgeQuantic_UI_*)` NuGet properties

**Design Principles:**

1. ✅ Each package is self-contained (no embedding of other packages' artifacts)
2. ✅ SDK references packages via NuGet-generated `$(Pkg*)` properties
3. ✅ Enables independent versioning (e.g., Runtime 0.1.3 + SDK 0.1.2)
4. ✅ No artifact duplication across packages
5. ✅ Clear interfaces between packages

**Example Resolution:**

```xml
<!-- Auto-generated by NuGet in obj/*.nuget.g.props -->
<PkgeQuantic_UI_Runtime>~/.nuget/packages/equantic.ui.runtime/0.1.2</PkgeQuantic_UI_Runtime>
<PkgeQuantic_UI_Components>~/.nuget/packages/equantic.ui.components/0.1.2</PkgeQuantic_UI_Components>

<!-- Used in Sdk.targets -->
<_RuntimeSource>$(PkgeQuantic_UI_Runtime)/tools/runtime/runtime.js</_RuntimeSource>
<_ComponentsSource>$(PkgeQuantic_UI_Components)/tools/source</_ComponentsSource>
```

See [wiki/PackageArchitecture](https://github.com/equantic/equantic-ui/wiki/PackageArchitecture) for complete documentation.

### Build Pipeline

```
dotnet build
    ↓
MSBuild: CompileEQuanticUI (BeforeTargets="Build")
    ↓
1. Roslyn parse /Pages/**/*.cs + Components source from $(PkgeQuantic_UI_Components)
2. Detect StatefulComponent/StatelessComponent classes
3. Generate TypeScript intermediate (.ts files)
4. Invoke embedded Bun for bundling
5. CopyEQuanticRuntime: Copy runtime.js from $(PkgeQuantic_UI_Runtime)
6. Output: wwwroot/_equantic/
   ├─ runtime.js (from Runtime package)
   └─ *.js (compiled components)
```

### Compilation Strategy

**Static Shell** (build-time): Component structure, layout, styles, initial state, routing metadata
**Dynamic Logic** (client-side): Event handlers, state mutations, computed properties, lifecycle hooks
**Server Actions** (server-side): Database queries, business logic, authentication

### Compiler Components (eQuantic.UI.Compiler)

The Roslyn-based compiler READS C# with strategies and WRITES JavaScript from an IR:

1. **ComponentParser** (`Parser/ComponentParser.cs`) - Parses C# AST
2. **CSharpToJsConverter** (`CodeGen/CSharpToJsConverter.cs`) - Dispatches each node to a strategy
   and returns IR (`ConvertIr` / `ConvertStatementIr`); the string API (`ConvertExpression`) is the
   seam for consumers not yet on the IR
3. **Strategies** (`CodeGen/Strategies/`) - One per C# construct (`Expressions/`, `Statements/`,
   `Linq/`, `Types/`, `Primitives/`…). Statements ALWAYS build a `JsStatement`. Expressions that
   crossed over implement `IExpressionIrStrategy`; a text-returning `IConversionStrategy` is spliced
   as an OPAQUE node, byte-identical to what it always produced — the strangler boundary. New
   strategies are born on the IR: `tests/…/Coverage/ir-migration.baseline.txt` lists the text ones
   and may only shrink (regen `EQ_UPDATE_IR_BASELINE=1`)
4. **IR + writers** (`CodeGen/Ir/`) - `JsExpr` → `JsStatement` → `JsClassMember` → `JsClass` →
   `JsModule`, ONE writer per level. The writers own what a strategy must never hand-write:
   parentheses (precedence, associativity, the `??`-beside-`&&` rule JavaScript enforces), single
   evaluation (`JsTemplate` binds a part used twice; a plain name is inlined), statement layout
   (`JsLayout.Pretty`; `Compact` reproduces the old string world byte for byte), the one class
   layout rule, and imports (`JsImport` records, `JsModuleWriter`)
5. **TypeScriptEmitter** (`CodeGen/TypeScriptEmitter.cs`) - Decides WHAT a module contains —
   members, imports — and hands nodes to the builder; it assembles no text
6. **SourceMapGenerator** - V3 Source Maps for C# debugging in browser
7. **SemanticHelper** (`Services/SemanticHelper.cs`) - symbol-based decisions. The rule: name
   heuristics are legal ONLY where the model cannot be asked (`Knows()` false — no model, or a
   strategy-rewrote node); an in-tree call the model cannot bind is a build error (EQ2006), never
   a guessed translation.

Output contracts: the component pins (`EQ_UPDATE_TRANSPILED=1`) and the conformance suite (both
sides executed) are the net. A layout change must be WHITESPACE-ONLY against the pins; a
translation change must execute identically on both sides.

**Supported C# Features:**
- Expressions: Arithmetic, Logical, Ternary, Null-coalescing (`??`)
- Fixed-width integers settle by RESULT type (`IntegerWidth`): byte/sbyte/short/ushort/uint always
  wrap, int/long wrap only under explicit `unchecked`, a `checked` context throws (read from the
  bound tree's `IsChecked` — the first IOperation use); float results `Math.fround`, print via
  `$eq.num.single`; `char++` steps the code unit; enum arithmetic computes on the value
- Control Flow: `if`, `switch`, `for`, `foreach`, `while`
- Modern Patterns: Recursive, Property, Positional, Relational (C# 9-12); bare-type and
  positional arms test by `instanceof` (in-source classes/records included)
- C# 13: `params` collections, `System.Threading.Lock` (inert object), `\e`, dictionary-key
  initializers as computed keys (`[^i] =` in initializers is fenced, EQ2008)
- C# 14: null-conditional assignment (`a?.B = v`, guarded single-eval lowering), `field`-backed
  properties, extension members (blocks lower to statics; call sites follow), out-lambdas with the
  callee contract, `nameof(List<>)`; same-file partial declarations are fenced (EQ2009)
- C# 15 (preview; eqc parses with `LanguageVersion.Preview` — see `Services/ParseDefaults`):
  labeled `break`/`continue` (1:1 JS labels), collection-expression `with(...)` (capacity drops,
  comparers are EQ2007), `union` declarations (TS union alias module), `closed` hierarchies,
  extension indexers (`static item(receiver, …)`)
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

1. **Core Runtime** (`/_equantic/runtime.js` ~15kb): Virtual DOM, events, state, server actions bridge
2. **Component Library** (`/_equantic/widgets.js` ~30kb): Standard components
3. **Page Bundles** (`/_equantic/pages/*.js`): Per-route lazy loading
4. **Shared Chunks** (`/_equantic/chunks/`): Automatic code splitting

## Component Attributes

- `[Component]` - Marks a class as a UI component
- `[Page("/route")]` - Marks a component as a routable page
- `[ServerAction]` - Marks a method for server-side execution
- `[Authorize(Roles = "Admin")]` - RBAC authorization on server actions
- `[AllowAnonymous]` - Bypasses authorization

## Authoring: the declarative surface

Trees are written with FACTORIES, never `new`: `Column(gap: Space.S3, children: [ Text(…),
Button(…) ])`. The SDK puts `eQuantic.UI.Components.UI` in scope in every file, and a source
generator (`eQuantic.UI.Generators`) writes the same surface for the APP's own components, so a
screen composes both identically.

- A factory is named EXACTLY like its type and mirrors a constructor parameter-for-parameter; no
  overloads (the twin is JavaScript). Containers take a trailing `children`.
- The generator mirrors the WIDEST constructor — the rule the emitter already applies to
  overloads — unless one is elected with `[UiFactory]`. Diagnostics: EQ3101 (two elected), EQ3102
  (two components share a name).
- A factory SHADOWS its type for types reached by `using` (the framework's, not the app's own):
  `Spacer.Fixed(34)` → use `Gap(34)`, `Badge.AsDot(v)` → `DotBadge(v)`. A conformance test fails
  naming any new one.
- eqc reads FILES, so generated sources must be on disk (`EmitCompilerGeneratedFiles`) and scoped
  to the configuration being built (`--generated`), or types arrive twice and resolve to neither.

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

Styling is TYPED C#, CSS-free at the authoring level: components declare `BoxStyle`
values (colors as `ColorToken`, spacing via `Space`/`EdgeInsets`, type via `TypeRole`) and each
realizer turns them into its native form — deduplicated atomic CSS classes on the web, GPU paint
on Photon. Theming is providing a `Primitives.IAppTheme` (select it with `AddUI(...).UseTheme(...)`;
`MaterialTheme.FromSeed(...)` rebrands in one line). Variants (`Primitives.Variant`) resolve
through `IAppTheme.Colors(variant)`.

- **Escape hatch**: raw HTML/CSS via `HtmlElement`/`DynamicElement` and `ClassBuilder` — web-only,
  for pages that need hand-written markup. Any external CSS a consumer brings is their own build
  concern; the framework ships exactly one styling engine.

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

Global version is defined in `Directory.Build.props` (currently 0.1.2). Debug builds auto-pack to `artifacts/packages/` for local testing.

## Compiler Boundaries (Server vs Client)

**Client Components (StatefulComponent/StatelessComponent):**
- Allowed: UI Logic, State Management, `System.Linq`, Basic Types
- Forbidden: `System.IO`, `System.Net.Http` (direct), blocking `.Wait()`
- Bridge: Data fetching MUST use `[ServerAction]`

**The compiler validates these boundaries before emitting JS.**
