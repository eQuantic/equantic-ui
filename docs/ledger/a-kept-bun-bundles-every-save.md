- **2026-11-10 · A kept bun bundles every save, and writes what changed**: `ModuleBundler` keeps one
  bun process between bundles, started by the first and handed every later one through a worker eqc
  carries (`bun -e`), and a served eqc keeps one for the life of the server. Every entry point is still
  bundled every time, with splitting, so the chunks the pages share stay valid; what changed is what is
  written, a module, a chunk or a map only when its text changed (a map also when a map it composes with
  did), and only the maps written are composed. Measured on the dashboard sample at one load, bun's own
  work was the smallest part of the bundle: 30 to 44 ms inside a kept process, against 117 to 215 ms
  started anew from eqc, and 74 to 159 ms more composing every map. A bundle with no maps is byte for
  byte what bun's command line wrote, so a publish copies what it did
  ([#1280](https://github.com/eQuantic/equantic-ui/issues/1280)).
