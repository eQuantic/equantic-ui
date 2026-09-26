# Tasks

## 1. The written agreement

- [x] 1.1 Write the `## Workflow` section and put the same text in `CLAUDE.md` and `AGENTS.md`
- [x] 1.2 Replace the stale body of `AGENTS.md` with a pointer to `CLAUDE.md`
- [x] 1.3 Add the test that fails when the two sections differ or one is missing
- [x] 1.4 Check: the test fails on a one-line edit to one copy, and passes on the committed files

## 2. OpenSpec

- [x] 2.1 Pin the CLI in `tools/openspec` with a lockfile, and run it through `scripts/openspec.sh`
- [x] 2.2 Initialise for `claude` and `agents`, and write the context and rules in `openspec/config.yaml`
- [x] 2.3 Add the CI job that validates strictly and fails on an empty tree
- [x] 2.4 Check: the gate fails with no item to validate, and passes with this change's spec

## 3. Sessions

- [x] 3.1 Turn off every attribution in `.claude/settings.json`, and set `OPENSPEC_TELEMETRY=0`
- [x] 3.2 Write the `SessionStart` hook, with the .NET SDK pinned by version and SHA-256
- [x] 3.3 Write the fixture that runs the hook as a fresh cloud container and asserts its promises
- [ ] 3.4 Check: CI's `session-start` job passes on a clean runner, with both controls biting

## 4. Documentation

- [x] 4.1 Update `CONTRIBUTING.md` and the pull request template
- [x] 4.2 Add the `docs/LEDGER.md` line citing #410
- [ ] 4.3 Archive this change before the merge, so `openspec/specs` on main matches the code
