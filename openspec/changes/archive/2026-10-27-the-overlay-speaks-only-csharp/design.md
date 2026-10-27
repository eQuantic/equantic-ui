# Design

## Context

The overlay (`src/eQuantic.UI.Runtime/src/dev/error-overlay.ts`) is installed by the boot in
Development. It rendered the JavaScript stack at once, then remapped it asynchronously: hop one reads
`<module>.js.map` (an app module's map, which `ModuleBundler` already composes down to C#), hop two
reads eqc's stage-one map at `/_equantic/src-map/<Name>.ts.map` for a frame that stopped in a TS
intermediate. When no frame mapped, the JavaScript stayed.

The served runtime is bundled once, by the Server's `BundleRuntime`, from
`src/eQuantic.UI.Sdk/Resources/boot.ts`, with bun and no map. It holds the hand-written runtime and
every module eqc transpiled from the SDK's own C# (`src/eQuantic.UI.Runtime/src/shared/components/`,
written and pinned by `SharedComponentTranspilationTests`). Measured on 2026-10-27: 1,201,233 bytes,
298,551 gzipped; bun's external map of it is 3.7 MB, 2.5 MB of which is the TypeScript's text.

## How Flutter solves it

Flutter's error report names a framework frame by its package path in Dart, the framework's own
language (`package:flutter/src/widgets/framework.dart`), never by the engine's machine code, and a
framework assertion that fires says it may be a bug in Flutter and asks for an issue on GitHub. Both
are what this change does: an SDK frame is named by the SDK's C# or module, and an error raised inside
the SDK asks for a report.

## Decisions

### 1. The runtime's map is composed when the runtime is bundled

`BundleRuntime` writes `wwwroot/runtime.js.map` beside the bundle (`--sourcemap=external`, which needs
`--outdir`), and eqc composes it in place with the maps its sources carry beside them: the composer
already exists (`SourceMapComposer`, #356) and `ModuleBundler` uses the same rule for an app's modules,
an inner map at `<source>.map`. Composing in the browser instead (a second hop per transpiled module)
would serve a map per module and the TypeScript's text besides. The composed map carries no source
text and names each source from `src/` (`eQuantic.UI.Sheet/Editing/SheetController.cs`,
`eQuantic.UI.Runtime/src/shared/lowering.ts`): the overlay names a frame, it does not show the SDK's
source.

One rule differs from an app module's composition. A position its module's map leads nowhere is
dropped there, as `@ampproject/remapping` dropped it, and a lookup falls back to the segment before it,
which in a one-module bundle is the statement before. One line of the runtime's bundle holds hundreds
of modules, so the segment before can be another module's: in the runtime's map such a position stays
at its module, and the overlay names it by the module's C# type. A record's module, which eqc writes on
one line with no mapping, is named the same way.

eqc runs from its build output, as `Sdk.targets` runs it in a source tree, and the Server gains a
`ProjectReference` to `eQuantic.Build` with `ReferenceOutputAssembly=false` so a cold build orders it.

### 2. The transpiled modules' maps are committed beside them

The stage-one maps exist only where eqc transpiles the SDK's C#, which is the test that pins the
modules (it needs the full reference set; the Server's build has none). So the test writes each map
beside its module and pins it byte for byte, as it pins the module: `SourceMapMode.External` (no C#
text), sources named from `src/`, and a `sourceRoot` leading from the map back to `src/`. A map that
drifted from its module fails the same assertion a module that drifted does.

### 3. The map is embedded and answered in Development alone

The Server embeds the runtime it serves, so it embeds the runtime's map too and answers it at
`/_equantic/runtime.js.map`, beside the stage-one endpoint and under the same condition: Development.
The overlay asks `<frame URL without its query>.map`, which is that route. The bundle gains only bun's
`debugId` comment.

### 4. A frame is named by its owner, decided by its URL

A frame whose generated URL (without its query) is the runtime's own URL is the SDK's; the overlay is
part of that bundle, so `import.meta.url` is that URL. A frame of a module under `/_equantic/` on the
page's origin is the app's. Anything else (an extension, a third-party script) is named by its host.
The map then names the place: C# for a `.cs` source, the runtime module for one under
`eQuantic.UI.Runtime/src/` (or `boot` for the SDK's boot), and the owner alone when nothing mapped.
`remapStackTrace` keeps each frame's generated URL so the owner survives the mapping.

### 5. Nothing is rendered before it is named

The overlay renders the title and the message at once and the call stack when its frames are named;
a remap that throws names the frames without maps. The JavaScript formatter and the code highlighter
that showed the stack's first line go.

### 6. The report

The report is plain text, copied with the Clipboard API: the message, the SDK's version, the page's
path (no query or fragment), the user agent, and the frames as the overlay names them, each with its
owner. The SDK's version reaches the page in the client configuration in Development alone, read from
the Server assembly's informational version.

## Risks / Trade-offs

- The Server's build now waits for eqc's. In CI and the samples it is built anyway; a build of the
  Server alone costs the compiler's build once.
- The embedded map adds its size to the Server assembly in every environment, while only Development
  answers it. Measured at implementation and stated in the pull request.
- 221 generated maps join the repository beside the 221 generated modules. They move only when a module
  moves.
- A frame's member is still the twin's name (`moveTo`, not `MoveTo`): eqc's maps carry no names. That is
  a follow-up of eqc's, not of the overlay's. Where bun renamed a class the bundle declares twice
  (`SheetController3`), a frame led to C# takes the type's name from its C# file.
- The message is the browser's (`TypeError: Cannot read properties of null`), where .NET would raise a
  `NullReferenceException`. Translating it is a follow-up.
