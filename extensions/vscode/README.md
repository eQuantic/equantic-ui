# eQuantic UI

A screen renders beside the file that produces it, from the buffer you are typing in — and every
element on it knows which C# expression built it, so you can click one, see that expression, and edit
it.

The preview is the **real** web realizer running the **real** compiled module. It is not a lookalike,
and that is the point: a preview that renders is evidence the page renders.

## What it does

- **Live preview of the unsaved buffer.** A long-lived design host holds your project's Roslyn
  compilation and compiles what is in the editor — not what is on disk. C# errors arrive as ordinary
  diagnostics, and the last frame that compiled stays on screen instead of blanking.
- **Click to select.** Every rendered element carries the source span of the expression that built it,
  so a click opens the file it is written in — often not the one being previewed — and puts the
  selection on that exact expression.
- **A property panel that writes C#.** Enums and design-token scales become lists; a `BoxStyle` becomes
  a sheet of key/value rows you can add to and take from. Every candidate edit is compiled before it is
  offered, and applied through the document's own undo stack, so one Ctrl+Z reverses a whole gesture.
- **Structure from the canvas.** Drag a child to reorder it, drag it into another container, hover for
  a `+` where something can go, duplicate, remove.
- **Refusals that explain themselves** before the affordance is drawn, never after it is clicked — a
  node built inside a loop has no single row to move, and a factory call has no object initializer to
  set an init-only member in.

## Keys

| | |
|---|---|
| Arrows | walk the tree — up to the parent, down to the first child, left and right along the siblings |
| Alt+Arrows | move the node instead of the selection |
| Delete | remove it |
| Cmd/Ctrl+D | duplicate it |
| Esc | let go — first of the selection, then of inspect mode |

## Requirements

- A project using the **eQuantic.UI SDK**, **built once**. The preview stands on an ordinary build's
  output: the reference list the SDK writes and `wwwroot/_equantic/runtime.js`. "Build the project
  once" is the honest instruction when a piece is missing.
- The **.NET runtime** the project targets. The design host ships inside this extension as ordinary
  framework-dependent binaries.

## Using it

Open a C# page or component and run **eQuantic UI: Open Preview**. The preview's own controls are in
the editor title bar: the pointer starts and stops inspecting, and a restart is there for when the host
has to be brought back.

## Building it from source

```bash
cd extensions/vscode
npm install
npm run package      # compiles, publishes the design host into host/, writes equantic-ui.vsix
```

`npm test` downloads a VS Code, loads the extension into it on the dashboard sample, and drives it.

## Settings

| | |
|---|---|
| `equanticUI.compileDebounceMs` | how long typing pauses before the preview recompiles (default 400) |
| `equanticUI.diagnoseDebounceMs` | how long before errors are re-checked (default 150) |
| `equanticUI.sidecarPath` | an explicit design host, for working on the host itself |
| `equanticUI.runtimePath` | an explicit `runtime.js`, when it is not where the SDK puts it |

## Snippets

`eq-page`, `eq-comp`, `eq-stateful`, `eq-action`, `eq-row` and `eq-col`.
