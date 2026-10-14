- **2026-10-14 · What 1.0 promises, and to which assembly**: every shipping assembly says its surface
  class in its own metadata, `contract`, `tool` or `experimental`. Of the 39, 25 are the contract, the
  compiler and the code writers are tools, and the 12 of the native track carry .NET's
  `[Experimental("EQX0001")]`, which the native SDK suppresses for the app that chose it and which
  stops any other project that uses one. The analyzer declares their API with the `[EQX0001]` prefix.
  The preview's rule now follows the version in `Directory.Build.props`: the day it loses its
  prerelease label, the build of a contract project refuses a `*REMOVED*` line, an `[Obsolete]` has
  to carry an `EQO` id, a URL and the major it leaves in, and a contract line of the developer surface
  cannot leave. Each guard was run with what it holds taken away, and failed
  ([#899](https://github.com/eQuantic/equantic-ui/issues/899),
  [#324](https://github.com/eQuantic/equantic-ui/issues/324),
  [#323](https://github.com/eQuantic/equantic-ui/issues/323)). Proposed and archived through OpenSpec
  (`openspec/specs/release-contract`).
