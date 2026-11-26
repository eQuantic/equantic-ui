- **2026-11-26 · A save's edit to view is held by the work it does, and a loaded runner no longer fails it**: the
  web's edit-to-view held the warm median of three saves to 1.5s, and on loaded macOS runners the median moved with
  the load, 1509 and 1898ms on runs whose fastest saves were 1471 and 1074ms, dropping pull requests that never
  touched hot reload from the merge queue. eqc now writes the work each compile did into `EQC_TIMINGS` (whether it
  kept the compilation, the files it walked and compiled, the modules it wrote, the files and bytes bun wrote), and
  the measurement holds a save to the most any warm save did, which no load moves: a save of one screen compiles 1
  of 159 files, and one that recompiled them all fails at 159. The time is read on the fastest warm save, and a
  `dotnet watch` that exits before the app answers is started once more, its exit named with its output, its code
  and the load ([#1909](https://github.com/eQuantic/equantic-ui/issues/1909)). Proposed and archived through OpenSpec
  (`openspec/specs/hot-reload`).
