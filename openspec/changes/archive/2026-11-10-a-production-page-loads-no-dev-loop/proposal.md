# Proposal

Closes #1359, a Bug under #1174 (a sub-issue of #157), on 1.0's served-runtime budget (#1357).

## Why

The boot imported the dev loop statically, the error overlay and the capture and replay of a page's fields around a hot reload, with what they pull in (`frame-owner`, `stack-remapper`, `source-map`), and decided at run time whether to use them. So the runtime every page loads first carried code a production page never runs: 29,920 bytes of `src/dev/` in the minified bundle at 9014ee883, by bun's metafile, about 7 KB of gzip per page, part of what took the runtime over its budget. The runtime's `index.ts` also re-exported `installErrorOverlay`, which nothing outside the runtime used, and which kept the overlay in the bundle on its own.

## What Changes

- The runtime has two bundles, both from the Server's `BundleRuntime`: `runtime.js`, whose entry is `boot.ts` and which carries no module of `src/dev/`, and `runtime.dev.js`, whose entry is `boot.dev.ts`, the same boot with the dev loop installed through one seam (`DevLoop`, `installDevLoop`). They are whole bundles rather than a chunk of one, since the loop reads the runtime's own classes (`instanceof`) and a second copy of them in a chunk would answer false; a page loads one or the other, never both.
- The server names the runtime a page imports, in the shell's import map: `runtime.dev.js` in Development and wherever it streams rebuilds, and `runtime.js` everywhere else. It answers `runtime.dev.js` only where it serves the dev loop, and its map beside the other's in Development.
- `index.ts` no longer re-exports `installErrorOverlay`.
- Nothing a developer writes changes. A production page downloads about 30 KB less of runtime, 8.8 KB of gzip; a page in Development or under `dotnet watch` loads the dev runtime and behaves as before.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `hot-reload`: a production page loads no byte of the dev loop.
