- **2026-10-27 · The development overlay speaks only C#**: a frame of the app is its C#, and a frame
  inside the SDK is named as the SDK's, by its own C# file and line where eqc transpiled it into the
  runtime and by the runtime's module elsewhere. No frame reaches a developer at a JavaScript position,
  while the maps load or after one fails. The served runtime gained the map that makes it possible: the
  Server's build writes it with the bundle and eqc composes it (`eqc compose-map`) with the map now
  committed beside each transpiled module, naming the sources from `src/` with no text; the Server
  embeds it and answers it at `/_equantic/runtime.js.map` in Development alone. An error raised inside
  the SDK says the fault is the SDK's, names the app's call to check when there is one, and hands over a
  report to copy, with the SDK's version the configuration now carries in Development. The title is
  Unhandled Runtime Error, where every error read Build Error, and the browser's ResizeObserver loop
  notice opens nothing. The served runtime grew 1 215 bytes gzipped (to 300 359), inside its budget
  ([#1108](https://github.com/eQuantic/equantic-ui/issues/1108)). Proposed and archived through
  OpenSpec (`openspec/specs/dev-error-overlay`).
