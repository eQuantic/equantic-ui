# eQuantic.UI Compiler Evolution Roadmap

## Vision

Transform the current `TypeScriptEmitter` from a syntax-based transpiler into a semantic-aware, extensible compiler capable of handling complex business logic and producing optimized, type-safe TypeScript/JavaScript.

## Rules of the Game: Controlled Environment & Boundaries

To prevent "spaghetti code" and unsupported operations in the browser, the compiler will enforce strict boundaries, inspired by **Next.js** (Server/Client split) and **Flutter** (Constraints).

### 1. Server vs. Client Split

- **Server Components** (`.cs` only): Can use `System.IO`, `DbContext`, and any .NET library. Renders static HTML or initial state.
- **Client Components** (`StatefulComponent` / `StatelessComponent`):
    - **Allowed**: UI Logic, State Management, `System.Linq` (via compilation), Basic Types (`string`, `int`, `DateTime`).
    - **Forbidden**: `System.IO`, `System.Net.Http` (direct), blocking `.Wait()`.
    - **Bridge**: Data fetching MUST use `ServerActions` (RPC style).

### 2. Semantic Validation (The "Guardrail")

The compiler will reject code that breaks these rules _before_ emitting JS:

- ❌ `File.ReadAllText()` in a `StatefulComponent`.
- ❌ Direct SQL queries in `OnMount`.
- ✅ Calling a method annotated with `[ServerAction]`.

---

## Phase 1: Extensibility & Intelligence (Current Focus)

### 1. Type Mapping Registry

**Goal**: Replace hardcoded method translations with a data-driven registry.
**Implementation**:

- Create `TypeMappingRegistry` class.
- Support key-value mapping for types (`DateTime` -> `Date`) and methods (`Console.WriteLine` -> `console.log`).
- Replace `switch` statements in `CSharpToJsConverter` with registry lookups.

### 2. Structured Code Builder

**Goal**: Eliminate `StringBuilder` fragility and ensure syntactically correct output.
**Implementation**:

- Create `TypeScriptCodeBuilder` class.
- Methods for `Class()`, `Method()`, `If()`, `Block()`, avoiding manual indentation management.

## Phase 2: Semantic Analysis

### 3. Roslyn Semantic Model

**Goal**: Resolve types to handle overloads and extension methods correctly.
**Implementation**:

- Update `ComponentCompiler` to compile C# trees (Roslyn `CSharpCompilation`).
- Pass `SemanticModel` to `CSharpToJsConverter`.
- Use symbol information to distinguish between `List.Add` and other `Add` methods.

## Phase 3: Advanced Features

### 4. LINQ Support

**Goal**: Translate C# LINQ to JS Array methods.
**Implementation**:

- Map `.Where` -> `.filter`
- Map `.Select` -> `.map`
- Map `.First` -> `.find`

### 5. Async/Await & Tasks

**Goal**: Robust `Task` handling.
**Implementation**:

- Ensure all `Task` returning methods are marked `async` in JS.
- Automatically await calls to these methods if they are awaited in C#.
