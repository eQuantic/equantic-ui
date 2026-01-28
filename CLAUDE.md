# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**eQuantic.UI** is a Flutter-inspired component-based UI framework for .NET that compiles C# components directly to optimized JavaScript at build time (not WASM). It provides type-safe, HTML-native components with a minimal runtime (<30KB).

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

```bash
cd src/eQuantic.UI.Runtime

# Build TypeScript runtime
npm run build          # tsc && vite build

# Run TypeScript tests
npm run test           # vitest

# Lint and format
npm run lint           # eslint
npm run format         # prettier
```

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
├── eQuantic.UI.Components/  # Standard components (Button, Input, Container, etc.)
├── eQuantic.UI.Compiler/    # Roslyn-based C# to JavaScript transpiler
├── eQuantic.UI.Sdk/         # MSBuild SDK for project integration
├── eQuantic.UI.Server/      # ASP.NET Core SSR and Server Actions
├── eQuantic.UI.Runtime/     # TypeScript browser runtime (reconciler, state, events)
├── eQuantic.UI.Runtime.*/   # Platform-specific Bun bundles (Osx64, Win64, Linux64)
├── eQuantic.UI.Tailwind/    # Tailwind CSS integration
├── eQuantic.UI.CLI/         # Developer CLI tools
└── eQuantic.Build/          # MSBuild tasks
```

### Build Pipeline

```
dotnet build
    ↓
MSBuild: CompileEQuanticUI (BeforeTargets="Build")
    ↓
1. Roslyn parse /Pages/**/*.cs
2. Detect StatefulComponent/StatelessComponent classes
3. Generate TypeScript intermediate (.ts files)
4. Invoke embedded Bun for bundling
5. Output: wwwroot/_equantic/
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

## Component Types

1. **StatelessComponent** - Functional components depending only on props
2. **StatefulComponent** - Components with persistent internal state (`SetState`)
3. **HtmlElement** - Low-level primitives mapping to HTML tags

## Server Actions

Server Actions are C# methods invoked directly from browser via RPC:

```csharp
[Page("/todos")]
public class TodoList : StatefulWidget
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

```xml
<Target Name="BuildCSS" BeforeTargets="Build">
    <Exec Command="npx @tailwindcss/cli -i ./src/styles.css -o ./wwwroot/css/app.css --content './wwwroot/_equantic/**/*.js'" />
</Target>
```

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

Global version is defined in `Directory.Build.props` (currently 0.1.1). Debug builds auto-pack to `artifacts/packages/` for local testing.

## Compiler Boundaries (Server vs Client)

**Client Components (StatefulComponent/StatelessComponent):**
- Allowed: UI Logic, State Management, `System.Linq`, Basic Types
- Forbidden: `System.IO`, `System.Net.Http` (direct), blocking `.Wait()`
- Bridge: Data fetching MUST use `[ServerAction]`

**The compiler validates these boundaries before emitting JS.**
