- **2026-11-02 · eqc compiles what the C# compiler compiled, for the app and each referenced project**: two bugs
  under #565 and #849 ([#828](https://github.com/eQuantic/equantic-ui/issues/828),
  [#1003](https://github.com/eQuantic/equantic-ui/issues/1003)). eqc walked each project's directory and parsed
  with no symbol, so `#if DEBUG` took the `#else` branch in the browser in both configurations, a file the project
  removed became a module, a file it linked from outside was never read, and a Release build after a Debug one
  read both configurations' generated usings. The web SDK now writes the app's `Compile` items and
  `DefineConstants` into a list eqc reads with `--sources`, and asks each project eqc compiles for its own through
  a target imported into that project for the question alone, with the global properties the ProjectReference
  protocol hands it. eqc compiles, transpiles and scans from the list, each project's files parsed with its own
  symbols. Measured on `samples/SharedLibrary` in Debug and Release. Proposed and archived through OpenSpec
  (`referenced-projects`).
