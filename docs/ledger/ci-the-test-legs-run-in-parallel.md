- **2026-11-09 · Each system's tests run as three parallel jobs**: a `test` leg ran the eleven test
  projects one after another, 13.4 minutes on Ubuntu and 15.1 on Windows on run 42675312854, and
  Conformance.Tests alone took 5 minutes 32 seconds of Ubuntu's. Each system now runs `conformance`,
  `web` and `rest` side by side, the projects found by `scripts/test-groups.sh`, so a new project lands
  in `rest` without an edit, a group that finds none fails, and every red project is still named at
  the end of its job. The ruleset requires the six jobs in place of the two legs
  ([#1310](https://github.com/eQuantic/equantic-ui/issues/1310)). Proposed and archived through
  OpenSpec (`openspec/specs/working-agreement`).
