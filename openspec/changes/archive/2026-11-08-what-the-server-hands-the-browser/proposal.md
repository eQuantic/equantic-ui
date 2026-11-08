## Why

Closes #936, #997 and #1002, bugs under #849 (the transpiler's fences hold on every path, part three).

Three places where what the server hands on was less than it had. `AddUI` registered the Brotli and Gzip providers and
nothing applied them: on the blank template in Release, `/_equantic/runtime.js` went out at 939,966 bytes whatever the
browser accepted (#1002). `ThemeBridge` wrote everything of `IAppTheme` but `Code`, so a theme's code palette coloured a
`CodeBlock` on the server and on Photon and fell to the default in the browser (#936). And the design host waited for
its renderer to exit before reading it, and hot reload read a failed rebuild's output with no deadline, so a child
that said too much, or left something holding its streams, held the host (#997).

## What Changes

- An app's responses are compressed with no line of the developer's: the SDK puts `UseResponseCompression` in front of
  the pipeline through an `IStartupFilter`. A stream of events is no type it compresses.
- `window.__EQ_THEME__` carries the colour of every `CodeTokenKind`, and the browser's theme answers `code(kind)` from it.
- The design host drains both of the renderer's streams from the start and holds the exit and both streams to one
  deadline, whose message names the command. Hot reload holds a failed rebuild's output to what is left of its limit,
  and starts the rebuild so that no node or server stays behind holding it.

## Capabilities

### New Capabilities

- `served-assets`: what an app's server answers for its runtime and its modules.

### Modified Capabilities

- `design-system`: gains "A theme's code palette reaches the browser".
- `hot-reload`: gains "A rebuild's output never holds the loop".

## Impact

- **The server**: `ResponseCompressionFirst`, `HotReloadService`. **The web realizer**: `ThemeBridge`. **The runtime**:
  `theme-bridge.ts`. **The design host**: `DesignSession`, `ChildOutput`.
- **eqc, the Photon shells, the SDKs, the templates**: none.
- **Public surface**: none. The developer surface does not move. The served runtime grows by the few lines that read the palette.
