# Proposal

Closes #410, a Task under the epic #160 (Instruments that fail, not warn).

## Why

The rules this repository runs on lived partly in `CLAUDE.md`, partly in the memory of whoever
worked last and partly in habit, while `AGENTS.md` had become a stale fork of an older
`CLAUDE.md` that still documents a Tailwind integration removed in August. A rule that is not in
the repository does not survive the next session: a cloud container starts without the owner's
git identity, signs commits with a key that is not the owner's, has no .NET SDK, and knows nothing
of the flow. Nothing checked any of it.

## What Changes

- A `## Workflow` section, identical in `CLAUDE.md` and `AGENTS.md`, is the working agreement:
  branch names, commit format and attribution, identity, the board issue behind every change, the
  pull request and Copilot review loop, the squash merge, the CI while GitHub Actions has no
  credits, English in everything committed, documentation and the ledger in the same pull
  request, and OpenSpec. A test fails when the two copies differ.
- `AGENTS.md` stops carrying its stale copy of the project guide and points to `CLAUDE.md` for it.
- OpenSpec is initialised for Claude Code and for tools that read `AGENTS.md`, with the project
  context and the per-artifact rules in `openspec/config.yaml`. The CLI is pinned by a lockfile in
  `tools/openspec/` and run through `scripts/openspec.sh`, with telemetry off.
- CI validates every change and spec strictly, and fails when there is nothing to validate.
- `.claude/settings.json` turns off every attribution Claude Code would add (commit trailer, pull
  request footer, session link) and registers a `SessionStart` hook that prepares a cloud
  container: the owner's git identity, commit and tag signing off, the .NET SDK `global.json`
  pins, the OpenSpec CLI, and Docker where the container allows it. Every downloaded installer is
  pinned by version and SHA-256.
- CI runs that hook the way a fresh container would, and asserts each of its promises.
- The pull request template asks for the issue it closes, the OpenSpec change and the ledger line.

Nothing changes for a developer using the SDK: no C#, setting, package or template moves, and
neither the public surface nor the developer surface does.

## Capabilities

### New Capabilities

- `working-agreement`: how a change reaches `main` in this repository, and what every session
  starts from: the written rules, the checks that hold them, and the prepared environment.

### Modified Capabilities

None.

## Impact

`CLAUDE.md`, `AGENTS.md`, `CONTRIBUTING.md`, `.github/pull_request_template.md`,
`.github/workflows/ci.yml` (two jobs), `.claude/settings.json`, `.claude/hooks/`, `.claude/skills/`,
`.claude/commands/opsx/`, `.agents/skills/`, `openspec/`, `tools/openspec/`, `scripts/`,
`docs/LEDGER.md`, and one test in `tests/eQuantic.UI.Web.Tests`. Contributors who run OpenSpec
locally need Node >= 20.19; building and testing the SDK still needs only the .NET SDK.
