## 1. The test legs run in parallel

- [x] 1.1 `scripts/test-groups.sh` splits the projects found under tests/ into conformance, web and rest
- [x] 1.2 Its self-test: each group's projects, the groups partition the tree, a new project lands in rest, an empty or unknown group fails saying so; proved both ways
- [x] 1.3 The `test` job's matrix runs each system's three groups, every red project named at the end of its job

## 2. NuGet packages are cached

- [x] 2.1 The restoring jobs cache `~/.nuget/packages`, keyed on the versions' files, with no fallback key and without `equantic.*`

## 3. Proof and documentation

- [x] 3.1 The longest test job on Ubuntu and on Windows, the run's wall time and the restore time, before and after, in the pull request
- [x] 3.2 The docs point at this spec's list of required jobs, and one ledger entry per issue
