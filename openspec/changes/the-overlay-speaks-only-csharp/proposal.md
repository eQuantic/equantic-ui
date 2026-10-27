# Proposal

Closes #1108, a Bug under #849 (the bug sweep). Met on #1081's branch (slice 2b of the spreadsheet,
epic #981): a click on a column header threw inside the SDK's runtime, and the development overlay
showed nothing but JavaScript.

## Why

The product principle says a developer using eQuantic.UI writes C# and never reads JavaScript. The
development error overlay breaks it the moment an error is raised inside the SDK. It walks a frame back
to C# only through the page modules' maps (and eqc's stage-one maps beside them), and the served
`runtime.js` ships no map, so every frame of the SDK stays `runtime.js?v=…:16:63983`, and when no frame
maps at all the overlay "degrades silently to the JS view". Edgar met it on 2026-10-26: the title
**Build Error** over an exception a click raised at run time, `Cannot read properties of undefined
(reading 'defaultPrevented')`, and a stack of `notTheSheets`, `view.events.click` and
`HTMLDivElement.<anonymous>` at JavaScript positions.

Two more defects were seen on the same page: every error is titled Build Error, though nothing the
overlay shows comes from a build, and the browser's benign `ResizeObserver loop completed with
undelivered notifications` notice opened the overlay when the window was resized.

## What Changes

For a developer, the overlay speaks only C# and says whose fault an error is:

- **A frame of the app is its C#**, as it is today when the module's map exists. A frame of a module
  that carries no map is named by the module (the C# type it was written from), never by a JavaScript
  position.
- **A frame inside the SDK is named as the SDK's.** What eqc transpiled into the runtime (the
  components, the charts, the spreadsheet, the code and sheet engines) is named by its own C# file and
  line (`eQuantic.UI.Spreadsheets/Spreadsheet.cs:412`); the hand-written runtime by its module
  (`eQuantic.UI runtime › shared/lowering`).
- **An error raised inside the SDK says so.** When the throwing frame is the SDK's, the overlay says
  the fault is in eQuantic.UI, not in the app (or, when the app's code called it, names that call as
  the first thing to check), and offers a report to copy for an issue: the message, the SDK's version,
  the page's path, the browser, and every frame named as above. It is the precedent Flutter set: a
  framework assertion asks to be reported as a bug in Flutter.
- **The stack is never shown as JavaScript**, not even while the maps are read: the call stack appears
  once its frames are named, and a map that cannot be read leaves a frame named by its owner rather than
  by its position.
- **A run-time exception is titled Unhandled Runtime Error**, and an exception an asynchronous method
  raised that nothing awaited says so in C#'s words rather than as a "Promise Rejection". Nothing the
  overlay shows comes from a build, so Build Error goes.
- **The browser's ResizeObserver loop notice opens nothing.**

## Capabilities

### New Capabilities

- `dev-error-overlay`: what the development error overlay shows a developer for an exception the
  browser raises, frame by frame, and when it opens.

### Modified Capabilities

(none)

## Impact

- **eqc**: `eqc compose-map <map> --names-from <dir>` composes a bundle's map with the maps its
  sources carry beside them, carrying no source text and naming every source from the given folder.
  `SourceMapComposer.ComposeFile` is the one way a map on disk is composed, and `ModuleBundler` uses it
  for an app's modules.
- **The runtime's transpiled modules carry their maps**: `SharedComponentTranspilationTests` writes
  each `src/eQuantic.UI.Runtime/src/shared/components/<Name>.ts.map` beside its module (C#'s lines, no
  C# text, sources named from `src/`) and pins it as it pins the module.
- **Server**: `BundleRuntime` writes `wwwroot/runtime.js.map` beside the bundle and has eqc compose it,
  which makes `eQuantic.Build` a build dependency of the Server (`ReferenceOutputAssembly=false`). The
  map is embedded and answered at `/_equantic/runtime.js.map` in Development alone. In Development the
  client configuration carries the SDK's version for the report. The bundle gains bun's `debugId`
  comment, and the served runtime's gzip budget moves only by what the overlay's code adds.
- **Runtime**: `dev/error-overlay.ts` names every frame by its owner (`dev/frame-owner.ts`), renders no
  JavaScript, titles by what was raised, offers the report, and ignores the ResizeObserver notice.
  `stack-remapper.ts` keeps each frame's generated URL, which is how a frame is known to be the SDK's.
- **Public surface**: eqc's new verb and `SourceMapComposer.ComposeFile` are tool surface
  (`eQuantic.UI.Compiler`), free at any version. No contract assembly and no line of the developer
  surface moves.
- **Break**: none an app meets. The overlay's `ErrorInfo.componentStack`, which nothing filled and
  nothing drew, goes.
