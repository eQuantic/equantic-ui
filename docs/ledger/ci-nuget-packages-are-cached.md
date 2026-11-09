- **2026-11-09 · CI restores NuGet packages from a cache keyed on their versions**: no job cached
  `~/.nuget/packages`, so every job of every run downloaded its packages again. The test jobs,
  `test (macos-latest)`, `build-packages` and `samples` now restore through one composite action from
  a cache keyed on the system, the job and a hash of the lines that declare a package or a version, so
  an edit to a project that declares none keeps the cache; there is no fallback key, so a miss restores
  from nuget.org as before, and this repository's own `equantic.*` packages are never saved
  ([#1312](https://github.com/eQuantic/equantic-ui/issues/1312)). Proposed and archived through
  OpenSpec (`openspec/specs/working-agreement`).
