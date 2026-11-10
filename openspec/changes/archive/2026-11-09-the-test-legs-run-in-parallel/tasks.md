## 1. The test legs run in parallel

- [x] 1.1 `scripts/test-groups.sh` splits the projects found under tests/ into conformance, web and rest
- [x] 1.2 Its self-test: each group's projects, the groups partition the tree, a new project lands in rest, an empty or unknown group fails saying so; proved both ways
- [x] 1.3 The `test` job's matrix runs each system's three groups, the list read whole before the loop, every red project named at the end of its job
- [x] 1.4 One finder, `scripts/test-projects.sh`, for the groups and the macOS job, failing when `find` fails

## 2. NuGet packages are cached

- [x] 2.1 One composite action caches `~/.nuget/packages/*` without `equantic.*`, keyed on the system, the job and the hash of the lines that declare packages and versions, with no fallback key
- [x] 2.2 The key's self-test: a version, a new package, a version property and the SDK move it; an edit that declares no package and a project under bin/ do not; proved both ways

## 3. Proof and documentation

- [x] 3.1 The longest test job on Ubuntu and on Windows, the run's wall time and the restore time, before and after, in the pull request
- [x] 3.2 The docs point at this spec's list of required jobs, and one ledger entry per issue
