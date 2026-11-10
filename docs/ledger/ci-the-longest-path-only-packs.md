- **2026-11-09 · The run's longest path only packs, and macOS runs what only a Mac answers**:
  `build-packages` set a pull request's wall time (18.2 of 24.0 minutes on run 42675312854) because it
  ran the whole suite a third time on macOS, 13 minutes 38 seconds, the same projects Ubuntu and Windows
  had run, for the facts only a Mac answers. It now builds, packs and checks, and a job of its own,
  `test (macos-latest)`, runs beside it, whole and with the Apple workloads, the projects that reference
  the Apple track or hold a test asking for macOS, derived by `scripts/macos-tests.sh` from the tests;
  a project that runs no test fails. Ubuntu and Windows still run every project, and the ruleset
  requires the new leg ([#1311](https://github.com/eQuantic/equantic-ui/issues/1311)). Proposed and
  archived through OpenSpec (`openspec/specs/working-agreement`).
