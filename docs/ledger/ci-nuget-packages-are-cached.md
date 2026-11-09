- **2026-11-09 · CI restores NuGet packages from a cache keyed on their versions**: no job cached
  `~/.nuget/packages`, so every job of every run downloaded its packages again. The test jobs,
  `test (macos-latest)`, `build-packages` and `samples` now restore from a cache keyed on the system,
  the job, the group and a hash of every file that declares a version, with no fallback key, so a miss
  restores from nuget.org as before, and without this repository's own `equantic.*` packages
  ([#1312](https://github.com/eQuantic/equantic-ui/issues/1312)). Proposed and archived through
  OpenSpec (`openspec/specs/working-agreement`).
