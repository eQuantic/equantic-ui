# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## THE PRODUCT PRINCIPLE (read this first — it decides designs)

**A developer using eQuantic.UI never writes a line of Swift, an Xcode setting, an Android
manifest, a plist, JavaScript, HTML or CSS. Everything is C#, `appsettings`, and fluent
configuration in `Program.cs` — inside ordinary Microsoft .NET semantics.**

The SDK is **self-aware**: it resolves the platform's details itself, and the developer states
only simple, meaningful settings. Every design decision is measured against this, and a feature
that satisfies the requirement while making someone learn a platform artifact has NOT satisfied it.

What this rules out, concretely:

- A doc that says "add this key to your Info.plist" or "create an entitlements file". The SDK
  writes those from a C# declaration.
- A knob whose value is a platform incantation the developer must look up. If the SDK can derive
  it from something it already knows, it derives it.
- An escape hatch offered as the primary path. Escape hatches exist (`HtmlElement`, a raw key);
  they are the exception with a stated reason, never the answer to "how do I do X".

What it looks like when honored — the measured example: a framework-dependent .NET app signed with
the macOS hardened runtime **cannot launch** (library validation refuses Microsoft's own dylibs)
and needs the JIT entitlement for .NET's own JIT. The wrong design tells the developer to declare
both, and warns when they forget. The right one — the SDK knows whether the app is AOT and whether
hardening is on, so it declares them ITSELF, logs what it added, and leaves the developer declaring
only what their own code needs (`builder.Entitlements.RequireNetworkClient()`).

The existing idioms this principle produced, to imitate rather than reinvent:

- **Fluent on the builder** in `Program.cs` (`builder.Capabilities.UseCamera("why")`), read by a
  source generator into an assembly attribute, read by the build into the platform's file.
- **Typed C# everywhere a string would be a platform incantation** (`ColorToken`, `Space`,
  `TypeRole`, `PhotonEntitlements.AllowJit`).
- **`appsettings.json` + the standard configuration binder** for what varies per environment.
- **MSBuild properties in the csproj** for build-time facts, named like .NET's own
  (`EQuanticSigningIdentity` beside `PublishAot`).

### The vocabulary speaks NO target's language

Never bring a target's nomenclature into a component — **unless the component IS the transcription
of that target's element**. The SDK needs a neutral idiom that works on web, desktop and mobile;
it was strongly inspired by Flutter, so when a name is missing, look there before inventing one
(`EdgeInsets`, `MainAxisAlignment`, `Positioned`, `Stack` all came from it).

Two questions settle every case. *Would this name exist if the web did not?* If no — *is this type
an HTML element?* If yes, the target's word IS the right word.

| correct | wrong, and why |
|---|---|
| `HtmlStyle.ZIndex`, `HtmlElement.Alt`, `DynamicElement.TagName` — the escape hatch transcribes the DOM on purpose | `Positioned.ZIndex` → `Layer`, `Image.Alt` → `Label` — abstract vocabulary borrowing a target's word |

The tell that a borrowed name is wrong is usually already in the tree: `CameraPreview.Alt` lowered
to `aria-label` and never to `alt`, and `KeyModifiers.Alt` — the KEY — carried the same word.

### What the developer's csproj says

`<Project Sdk="eQuantic.UI.Sdk">` and nothing else. No `PackageReference` — the SDK adds
Primitives, Components, Web, Server, Runtime, the Generators analyzer and the right embedded-bun
package itself. The developer writes one only to opt IN to something extra, an icon pack being the
usual case.

That is why the bun ships as per-OS, per-architecture packages (`eQuantic.UI.Runtime.Osx64`,
`.WinArm64`, …) selected by condition in `Sdk.props`: bundling is a build-time need the developer
never asked for, so they never install a toolchain, never learn which one, and never see it fail on
a machine of a different shape. A reference in that file is often a DELIVERY VEHICLE rather than a
code dependency — removing one because no code calls it breaks the consumer's restore, not our
build.

### The bar for a decision

Decisions here are **robust and durable**: the goal is to stabilise the SDK while keeping it ready
for new features, on strongly defined architectural lines, using the best of what .NET itself
offers. **The transpiler is the bar** — Strategy per construct, an IR with one writer per level,
coverage suites that enumerate by reflection against a baseline that may only shrink — and the
rest of the SDK is measured against it, not excused from it.

In practice: prefer the structural fix to the patch (declaring a build dependency beats warning
that it is missing); prefer the mechanism .NET already has to a home-grown one (a
`ProjectReference` with `ReferenceOutputAssembly=false` beats a `Condition="Exists(…)"` over the
solution's own output); and prefer an instrument that FAILS to one that warns — a pin that compares
and fails, regenerated behind an env var, never one that silently rewrites itself.

### NO MEMBER SURVIVES TO KEEP AN OLD SHAPE ALIVE

The SDK is in **preview**, and a preview version means exactly this: **a signature widens and the
old one GOES.** A breaking change is the cheapest thing here — cheaper than the member kept beside
the real one, cheaper than the doc explaining why that member exists, cheaper than the test pinning
it, and far cheaper than the reader who has to work out which of two shapes is the live one.

So: no compatibility constructor, no retained `Deconstruct`, no `[Obsolete]` forwarder, no property
that exists because widening a constructor would have been a break. Change the shape, fix every call
site, delete what the change replaced.

This rule is written down because the opposite one was, in code, and it taught two rounds of the
wrong lesson. `TypeStyle` gained a face and grew a seven-parameter constructor and a seven-output
`Deconstruct` to keep the shipped surface reachable, plus a test asserting they stayed. `SemanticNode`
then gained a live-region urgency and copied the repair — which cost a CS0121 ambiguity across every
six-argument call in the tree, and a paragraph explaining the trap. A third site (`UI.ProgressBar`)
carried twenty lines explaining why it could NOT offer the same repair. Three artefacts, all of them
serving a consumer nothing in this repository has: no project here compiles against a released
`eQuantic.UI.*` package — the VS Code extension is TypeScript. All three are gone.

The rule ENDS at the vocabulary's enums, and for a reason that is not compatibility: `SemanticRole`,
`NodeKind` and their siblings are append-only because C# bakes an enum constant into the CONSUMING
assembly's IL, so renumbering is silent rather than a break a compiler would report. That is a
correctness pin (`EnumValueAbiTests`), not a shape kept alive — a member is never deleted or moved
there, and nothing is kept beside anything.

---

## Workflow

The working agreement, for every session and every agent. This section is the same text in
`CLAUDE.md` and in `AGENTS.md`, and `WorkflowSectionTests` fails when the two copies differ: change
both in the same commit.

### Language

Talk to Edgar in Brazilian Portuguese in the chat. Everything committed or published is in English:
code comments, XML documentation, Markdown, commit messages, branch names, test names, pull requests
and issues, even where the file around it is in Portuguese. Never a code comment in Portuguese.

### Every change starts from an issue on the board

The board is the [eQuantic UI project](https://github.com/orgs/eQuantic/projects/11). Before the
branch, find the issue the change serves. If there is none, create it as a SUB-ISSUE of the epic or
feature it belongs to, with the right type (Epic, Feature, User Story, Task or Bug), and add it to
the board. Its status moves with the work, in the same step as git: In progress when the branch
starts, In review when the pull request opens, Done when it merges. A sub-issue and a type are set
through GraphQL (`addSubIssue`, `updateIssueIssueType`), and the board status through
`updateProjectV2ItemFieldValue`.

### Branches

`<type>/<slug>`: the type is one of `feat/`, `fix/`, `chore/`, `refactor/`, `docs/`, `test/`,
`ci/`, `perf/` or `build/`, and the slug is the change in words, not an identifier:
`refactor/vocabulary-speaks-no-target`, `fix/the-sdk-does-not-dictate-your-xcode`,
`chore/0.2.0-preview.52`. Never a tool's or an agent's own prefix (`claude/…`, or whatever name a
session suggests): rename it before the first commit. A squash merge keeps no head ref, so the
branch list is the only place this convention is legible.

Nothing goes straight to `main`. A repository ruleset enforces `pull_request` and
`copilot_code_review` there and rejects a direct push; never commit onto `main` locally either.

### Commits

`emoji type: description`, in English, subject and body, emoji first: ✨ feat · 🐛 fix · 📝 docs ·
♻️ refactor · ✅ test · 🔧 chore · 👷 ci · ⚡ perf · 📦 build · 💄 style, and 🔀 merge for a merge
commit.

NEVER a co-authorship or attribution line, in any of these shapes:

```text
Co-Authored-By: <assistant or model name> <noreply@…>
<Assistant>-Session: <link back to a conversation>
🤖 Generated with <tool>
```

…or any other spelling of the same thing. They are placeholders on purpose: this section is pushed
like everything else, so it must not be the one place a real model identifier or conversation link
lives, and a reader who matched only the literal examples would have learned the wrong rule. The
rule is the CATEGORY: no co-authorship for an assistant, no session or conversation link, no tool's
signature line, whatever a harness's own default says. It holds for every artifact (commit messages,
pull request titles and bodies, code comments, documentation); naming the TOOLING in prose stays
allowed. The project's `.claude/settings.json` turns off every attribution Claude Code would add
(the commit trailer, the pull request footer and the session link), so it is not written in the
first place.

And a job compares, because reading it back is not enough: `scripts/check-commit-messages.sh`, run
by the `commit-messages` job, reads every commit message of a pull request and of `main`. Five squash
messages reached main carrying their composer's scaffolding while this rule was already written, and
all five came from the message composed at merge time, the one artefact nobody re-reads. That is why
the merge below composes nothing.

### Identity

Commits are authored and committed as `Edgar Mesquita <edgar@equantic.tech>`, set in the
repository's git config. In a cloud session, `.claude/hooks/session-start.sh` sets that identity in
every new container and turns commit and tag signing off there, because the container's signing key
is not the owner's.

### Pull requests

1. **Review it yourself, then open it yourself.** Before opening the pull request, review the whole
   diff (in Claude Code, `/code-review high` on the branch) and fix what the review finds: Copilot's
   first round starts on its own when a pull request that is not a draft opens (the ruleset skips
   drafts), and it should read a diff that has already been through a full review. If no round has
   started a few minutes after the pull request is ready for review, request it with
   `gh pr edit <n> --add-reviewer @copilot`. Work that is committed and pushed is parked on a branch
   nobody reviews. Opening the pull request is the last step of the work, so open it without
   waiting to be told, whatever a harness's own default says about not opening one unless asked.
2. **In English, and it closes its issue.** The title is `emoji type: description` and becomes the
   squash commit's subject; the body follows `.github/pull_request_template.md` and says
   `Closes #N`. Then READ BACK the body you posted: a tool may append its own "Generated with…"
   footer when the pull request is created, and that footer is deleted.
3. **Answer Copilot: three rounds at most, and stop at the first without a defect.** Its review is
   light and reveals a pull request a little at a time (#446 measured it), so a loop that waits for
   it to run dry pays one round per finding. Read the WHOLE review, its body included, where a
   finding can live without a thread ("Previously missed"). Sort every finding:
   - a **defect**, when the code this pull request changes does the wrong thing, or a guard passes
     where it should fail: fix it, prove the fix both ways, and it earns another round;
   - **hardening, documentation or a nit**, a "Previously missed" item that is not a defect
     included: fix it in the same push, or open an issue under the pull request's parent. It earns
     no round;
   - **wrong**, when the finding does not hold: answer it with what shows so, a measurement where
     one exists. It earns no round.

   Answer every thread and resolve it (GraphQL `resolveReviewThread`: the ruleset refuses a merge
   while one is open), and put every fix of a round in ONE push. When the round found a defect, ask
   for the next one with `gh pr edit <n> --add-reviewer @copilot` and wait for it; it takes a few
   minutes. The ruleset does not review on push (ruleset 21198869, `review_on_push` off, readable
   with `gh api repos/eQuantic/equantic-ui/rulesets/21198869`), so a merge from main, or a change to
   the body or the docs alone, costs no round. The loop ends at the first round with no defect, and
   after the third in any case: a defect found later is still fixed and proved, and anything else
   is sorted as in any round, without asking for another. When the account's GitHub credits are
   exhausted and Copilot cannot review, skip this step.
4. **Squash-merge on green CI, composing nothing.** The ruleset requires thirteen of the CI's jobs
   and a branch up to date with `main` (#287), so when `main` moved after the CI ran, merge `main`
   into the pull request first: CI runs again, and it costs no Copilot round. Then
   `gh pr merge <n> --squash` takes the repository's squash default, the pull request's title and
   body, so the commit's subject IS the title that was already read. If a message is composed
   anyway, it goes in a file and `./scripts/check-commit-messages.sh --file <file>` reads it before
   the merge. Last, merge the pull request's wiki branch, when it has one, into the wiki's master
   (see Documentation below): main and the wiki move in the same step, and the next pull request's
   guards read both.
5. **A release is Edgar's call.** Merging a pull request never implies one; `CLAUDE.md`'s Version
   Management section says what a bump touches.

Do not open thin pull requests: group a coherent body of work (a slice, a family of bugs, a refactor
and the net that proves it). Commits inside it stay small; the pull request is the unit that must be
substantial.

### CI

`.github/workflows/ci.yml` runs on GitHub Actions. While GitHub Actions has no credits, the same
workflow runs on the eQuantic Space runner, followed with `eqs runs ls`, `eqs runs get <n>` and
`eqs runs logs <n>` (`--job <key>` for one job's full log). The environment has `EQS_API_URL` and
`EQS_TOKEN`, and its setup script installs `eqs`. `eqs runs ls` lists every repository of the
workspace and pull request numbers repeat across them, so confirm the run's `repo` with
`eqs runs get` before reading a failure as this repository's. The Space runner has no Docker daemon.

### Documentation and the ledger

Documentation changes with the behaviour it describes. This repository's Markdown changes in the
same pull request. The wiki page changes in English AND Portuguese, in one commit on a branch of the
wiki repository named exactly like the pull request's own branch. CI checks that branch out for the
docs guards when it exists (`scripts/checkout-wiki.sh`), so the pull request is checked against the
pages it brings, while every other one still reads master, and so does a pull request from a fork,
whose branch name can repeat one of this repository's. When the pull request merges, its wiki
branch is merged into the wiki's master (rebased on master first if master has moved) and deleted.
A page pushed to master before its change merges fails the wiki guards of every other pull request
(#406). Locally the guards read `equantic-ui.wiki` beside the repository, which every local run
shares, so keep that clone on master: to run them against a pull request's wiki branch, point
`EQ_WIKI_DIR` at a worktree of it (`git -C ../equantic-ui.wiki worktree add <dir> <branch>`).
`docs/LEDGER.md` keeps the history, one line per event, citing the issue.

### OpenSpec

Specs are versioned in the repository, under `openspec/`, with the project's context and the rules
for each artifact in `openspec/config.yaml`.

- Every change that creates or changes behaviour starts with a proposal (`/opsx:propose`) in the
  same pull request as the code, and is archived before the merge with
  `openspec archive <change> --yes`, which moves it under `openspec/changes/archive/` and writes its
  delta into the main specs in one step, so `openspec/specs` on `main` always matches the code on
  `main`. `/opsx:archive` walks through the same step by hand; its `mkdir` and `mv` go through the
  session's normal permissions, since a skill's `allowed-tools` pre-approves and forbids nothing.
- A capability gets its spec when a change first touches it, not before.
- The CLI is pinned by `tools/openspec/package-lock.json` and runs through `scripts/openspec.sh`,
  or as `openspec` in a session the hook prepared. CI's `openspec` job validates every change and
  spec with `openspec validate --all --strict`, through `scripts/check-openspec.sh`, which also
  fails when there is nothing to validate.
- Telemetry is off: `OPENSPEC_TELEMETRY=0`, set in `.claude/settings.json`, by the scripts and in CI.

### Sessions start from these rules

A session reads this section from `CLAUDE.md` or `AGENTS.md`, and `.claude/settings.json` registers
a `SessionStart` hook, `.claude/hooks/session-start.sh`, that prepares its environment. In every
session it puts the pinned OpenSpec CLI on PATH. In a cloud container (`CLAUDE_CODE_REMOTE=true`) it
also sets the git identity, turns signing off, installs the .NET SDK that `global.json` pins, and
starts Docker where the container allows it; on a laptop it leaves all of that as the owner set it
up. Every installer it downloads is pinned by version AND SHA-256 and refused on a mismatch, and npm
packages are pinned by the lockfile's integrity hashes. When a tool it is responsible for cannot be
set up, the person in the session sees a warning naming it, and the session still starts. CI's `session-start` job runs the hook the
way a fresh container would and asserts every promise (`scripts/check-session-start.sh`).

## Project Overview

**eQuantic.UI** is a Flutter-inspired component-based UI framework for .NET that compiles C# components directly to optimized JavaScript at build time (not WASM). It provides type-safe, HTML-native components with a small runtime (its gzip size is measured by the site's
build and not quoted here — it changes every release).

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
# Type-check (tsc) and test (vitest run) the runtime through the embedded Bun — no Node, any OS.
# The build extracts the bun itself from the sibling package for this OS and architecture
# (src/eQuantic.UI.Runtime.<Os><Arch>/tools/bun/), and the target installs from bun.lock first.
dotnet build src/eQuantic.UI.Runtime -t:TestRuntime

# Lint and format go through the same binary, from src/eQuantic.UI.Runtime:
#   <that bun> x eslint src --ext .ts
#   <that bun> x prettier --write "src/**/*.{ts,json}"
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

### Development Workflow (building the samples)

The samples build against the framework PROJECTS, never against packages. `Sdk.props` sees the source
tree beside it (`IsEQuanticDevMode`) and swaps every `PackageReference` for a `ProjectReference`;
`Sdk.targets` runs the `eqc` and `eqicon` the graph just built (`_EqSourceTree`). The two tools are
`ProjectReference` edges with `ReferenceOutputAssembly=false`, so a cold `dotnet build` orders them
itself — no bootstrap pack, no local feed, no cache step between an edit and the sample that shows it:

```bash
dotnet build samples/DefaultUIDashboard   # web; PhotonDesktop and WalletMobile are the native heads
```

`runtime.js` in a source-tree build is `src/eQuantic.UI.Server/wwwroot/runtime.js`, which the Server
project's `BundleRuntime` target writes with the embedded bun (from
`src/eQuantic.UI.Sdk/Resources/boot.ts`) before every build of the Server — part of the same graph,
since a sample references the Server. It is the one writer of the bundle (#335).

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
├── eQuantic.UI.Code/           # The code editing ENGINE: document, selection, history, languages, keymap —
│                               # write-once, transpiled into the runtime; realizers drive it only through
│                               # the vocabulary's ICodeSurfaceModel (docs/CODE-EDITOR-PLAN.md)
├── eQuantic.UI.Components/     # WRITE-ONCE component library (authored against Primitives; realized per target)
├── eQuantic.UI.Charts/         # WRITE-ONCE chart library (BarChart…): runtime-provided like Components, colour from IAppTheme.Data
├── eQuantic.UI.Web/            # WEB REALIZER + the DOM escape hatch (HtmlElement, HtmlNode, ClassBuilder)
├── eQuantic.UI.Server/         # ASP.NET Core SSR, Server Actions, metadata and assets
├── eQuantic.UI.Compiler/       # Roslyn-based C# to JavaScript transpiler (the library)
├── eQuantic.Build/             # eqc — the transpiler CLI the SDK runs; ships as tools/net10.0/eqc.dll
├── eQuantic.UI.Generators/     # Source generator: the declarative factory surface for an app's own components
├── eQuantic.UI.Sdk/            # MSBuild SDK for web apps (Sdk.props, Sdk.targets, Resources/boot.ts)
├── eQuantic.UI.Runtime/        # TypeScript browser runtime's source and tests (reconciler, state, events); the Server bundles it
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
└── eQuantic.UI.<Pack>/         # Icon catalogs (Lucide, Heroicons, …), Charts.ChartJs/.ApexCharts, Gtm, Images, Lottie, Email, Material
```

### Package Architecture (Self-Contained Design)

eQuantic.UI follows a **self-contained package architecture** where each package manages its own artifacts:

**Key Packages:**

- **eQuantic.UI.Server** - Embeds the runtime it serves, and ships the same bytes at `tools/runtime/runtime.js` for the SDK to copy beside an app's modules (the tools that load them outside a server read that copy). `eQuantic.UI.Runtime` is the TypeScript's project and its proof; it publishes nothing (#335)
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
<PkgeQuantic_UI_Server>~/.nuget/packages/equantic.ui.server/<version></PkgeQuantic_UI_Server>
<PkgeQuantic_UI_Components>~/.nuget/packages/equantic.ui.components/<version></PkgeQuantic_UI_Components>

<!-- Used in Sdk.targets; beside a source tree no $(Pkg*) is set and the Server's wwwroot/ and the sources are the fallback -->
<_RuntimeSourcePath>$(PkgeQuantic_UI_Server)/tools/runtime/runtime.js</_RuntimeSourcePath>
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
5. CopyEQuanticRuntime: Copy the served runtime.js from $(PkgeQuantic_UI_Server) (or the Server project's own wwwroot/runtime.js beside a source tree)
6. Output: wwwroot/_equantic/
   ├─ runtime.js (the Server's bundle, byte for byte)
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
7. **ValueFlow** (`CodeGen/Strategies/ValueFlow.cs`) - the dispatcher settles EVERY expression for
   the implicit conversion the BOUND tree (`IOperation`) wraps it in — char promotion, int→long —
   at every site C# applies it. A rule ValueFlow owns is removed from the syntax strategies (never
   both, or the value converts twice). It now owns TEXT (a value on its way into a concatenation or
   an interpolation hole) and decimal too; what it still hands back untouched is principled, not
   owed — no bound tree to read, no implicit conversion, or two chars compared, which stay chars.
   `docs/BOUND-TREE-PLAN.md` has the slices and what each one cost
8. **SemanticHelper** (`Services/SemanticHelper.cs`) - symbol-based decisions. The rule: name
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
  bound tree's `IsChecked` — the first IOperation use). A `float` is a single wherever it is
  PRODUCED (`SinglePrecision`) — every `+ - * /`, increment and compound, an int past 2^24 on its
  way in, every float answer of the numeric table, a float constant, a hydrated value — because
  RyuJIT rounds each operation; it prints via `$eq.num.single`. What the BROWSER produces enters
  through the runtime, which rounds at every seam C# types `float` (`FloatSeamsTests` derives them).
  `char++` steps the code unit; enum arithmetic computes on the value
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
- User-defined operators on IN-SOURCE types: binary, unary (named by arity), compound, and
  implicit/explicit conversions — the twin carries them as static methods (`Money.opAdd`,
  `Money.fromInt`) and every site the bound tree shows calls them. A VOCABULARY type's conversion
  crosses the same way, to the static its runtime twin carries (`IconGlyph.fromIcons`), unless the
  operator says its twin takes the operand as it is (`[ConversionPassesThrough]`, `SizeValue` from a
  number); a type from outside the SDK (`Index`) passes its primitive through
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

What a build actually writes under `wwwroot/_equantic/` (sizes are not quoted here: the served
runtime's is recorded in `tests/eQuantic.UI.Server.Tests/Budgets/served-runtime.json`, which the
suite compares, and CI reports the page modules' on every pull request):

1. **runtime.js** — virtual DOM, events, state, the server-actions bridge AND the shared component
   library, whose transpiled modules ship INSIDE it (`[RuntimeProvided]`); eqc routes
   `using eQuantic.UI.Components` imports there rather than emitting a per-app copy. The page reaches
   it as the bare module `@equantic/runtime` through the shell's import map, and the Server serves its
   own embedded copy at that route. That copy is a BUILD OUTPUT: the Server's `BundleRuntime` writes
   `wwwroot/runtime.js` from `Resources/boot.ts` before every build, and it is never committed — a
   committed copy lagged the runtime's source twice without anything noticing (#273)
2. **`<Component>.js`**, and a **`.js.map`** only where a developer is debugging — one module per
   page or component, flat. The map carries the C# it came from, so `EQuanticSourceMaps` is `full`
   in Debug and `none` everywhere else (`external` writes one without the C# for an error reporter),
   its sources are named inside the project, and a publish never takes one (#352). `boot.ts` imports
   the page's module dynamically on navigation, so per-route lazy loading falls out of the module
   graph. eqc runs bun with `--splitting`, so what two or more modules share is a chunk named after
   one of them with a hash: `NotFoundScreen-<hash>.js` in the dashboard sample is its console
   shell, not a second type of that name. There is no `pages/` folder
3. **strings/`<culture>`.json** — the culture catalogs, when the app has `.resx`
4. **icons/** — the app icon sizes and the web manifest, when an `AppIcon` is declared

The styles are not a file: the token sheet and the atomic classes are written inline by the server
(`<style id="eq-atomic">` among them). A base stylesheet was copied here until #335, and no page had
ever linked it.

## Component Attributes

- `[Component]` - Marks a class as a UI component
- `[Page("/route")]` - Marks a component as a routable page
- `[ServerAction]` - Marks a method for server-side execution
- `[Authorize(Roles = "Admin")]` - RBAC authorization on server actions
- `[AllowAnonymous]` - Bypasses authorization

## One type per file, or the file says why not

A `.cs` file declares ONE top-level type, and takes its name. A second type riding behind the
first one's closing brace is invisible — to a reader who looks for it by file name, and to any tool
that moves code by member. `RealizedElement`, the web realizer's only output shape, lived at the
tail of a 2,666-line `WebRealizer.cs` and was DROPPED for one build when that file was split,
because the range check driving the split stopped at the last member of the class instead of at the
end of the file. The compiler caught it; the point is that it could go missing at all.

The exceptions are a LIST rather than a category, in
`tests/eQuantic.UI.Compiler.Tests/Coverage/one-type-per-file.baseline.txt`, which may ONLY SHRINK.
Each entry carries the count it was measured at and why the file is the unit: an interop header
transcribed in one place, a closed hierarchy whose cases ARE the type, one protocol's record set,
one vendor's options object — or the honest `not looked at yet`. A category the test waves through
("interop is exempt") hides a careless second type inside it; a named entry stays visible and stays
reducible, and the count is what stops a listed file quietly growing one more.

A new file gets no entry. Split it, or add the entry BY HAND with its reason — the regenerator
(`EQ_UPDATE_ONE_TYPE_BASELINE=1`) exists for a REMOVAL: it carries every reason across and never
writes the sentence that would justify a new one.

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
  eqc takes that list from `ProjectCompilationHelper.GetCompilationUnits` and walks for nothing
  itself, and the SDK removes a generated file its generator stopped emitting after a compile that
  RAN, never after one that was skipped (#253).

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
- **TypeScript Tests**: Vitest (`src/eQuantic.UI.Runtime/src/**/*.spec.ts`), run with `dotnet build src/eQuantic.UI.Runtime -t:TestRuntime`

## Version Management

Global version is defined in `Directory.Build.props` — ONE line, which is the whole bump.

A release is: commit the bump as `🔧 chore: <version>`, then tag `v<version>` and push the tag —
the tag is what triggers publication to nuget.org. Never bump on your own initiative: choosing to
release is Edgar's call, and a version number in the tree that nobody released is worse than none.
(Do not quote the current version here; it changes every release and a stale number in the docs is
how a reader is misled — read `Directory.Build.props`.)

**The release's list of BREAKS is not written from memory — the build already has it.** Every
project that ships an assembly declares its public surface in `PublicAPI.Shipped.txt`, and
`Microsoft.CodeAnalysis.PublicApiAnalyzers` fails the build of any change that is not declared:
RS0016 names a signature that appeared, RS0017 one that went. So the third step of a release, after
the bump and before the tag, is:

```bash
./scripts/public-api.sh ship
```

which folds each project's `PublicAPI.Unshipped.txt` into its `Shipped.txt` — an addition moves
across, a `*REMOVED*` line cancels the entry it names. THOSE `*REMOVED*` LINES ARE THE BREAKS, and
`git diff v<previous>..v<this> -- '**/PublicAPI.Shipped.txt'` is the whole change of surface,
retirements and additions together. Copy them into the annotated tag's message and the wiki's
[Upgrading](https://github.com/eQuantic/equantic-ui/wiki/Upgrading) page (both languages); write
the migration line from what an app upgrading actually met, because the analyzer says WHAT moved
and never what to write instead. A break missing from the notes is now a build that failed and was
made to pass without reading the diff — `./scripts/public-api.sh update` prints
"Read the diff before committing it — that IS the API review" for exactly that reason.

The analyzer holds C# signatures and nothing else, and the product principle puts the rest of what an
app writes in its csproj, its appsettings and its `dotnet new` line. That surface is
`tests/eQuantic.UI.Web.Tests/developer-surface.baseline.txt` — every `EQuantic…`/`Eqc…` property
the two SDKs define or read, every configuration section the source binds, every template
parameter and choice — pinned by `DeveloperSurfaceContractTests` and regenerated with
`EQ_UPDATE_DEVELOPER_SURFACE=1`. A line that leaves it is a break an app meets as a setting silently
ignored, so it goes in the notes beside the `*REMOVED*` lines: `git diff v<previous>..v<this> --
tests/eQuantic.UI.Web.Tests/developer-surface.baseline.txt` is the rest of the change of surface.

## Compiler Boundaries (Server vs Client)

**Client Components (StatefulComponent/StatelessComponent):**
- Allowed: UI Logic, State Management, `System.Linq`, Basic Types
- Forbidden: `System.IO`, `System.Net.Http` (direct), blocking `.Wait()`
- Bridge: Data fetching MUST use `[ServerAction]`

**The compiler validates these boundaries before emitting JS.**
