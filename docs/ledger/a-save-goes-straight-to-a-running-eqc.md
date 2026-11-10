- **2026-11-09 · A web save goes straight to an eqc the server keeps running**: every save started a
  new MSBuild, which evaluated the project and resolved its references before it started a new eqc,
  measured at 6.8s of MSBuild on CI's runner before a module was written. The server now asks the
  project's MSBuild once for a run, while the app starts, how to start eqc, and keeps it running:
  `eqc --serve` reads `compile` on its standard input for each save and answers in one line, loading
  the project's compilation again. The server owns the process: eqc ends with its input, a stop or a
  compile past its limit kills it, and the next save starts it again. `--watch`, which compiled with
  the semantic model it loaded at its start, gave way to it. Measured on the dashboard sample at a load
  average of 80 to 100, the MSBuild part of a warm save went from seconds to 5 to 12ms, with one
  document per save, hydrated ([#1276](https://github.com/eQuantic/equantic-ui/issues/1276)). Proposed
  through OpenSpec (`openspec/specs/hot-reload`).
