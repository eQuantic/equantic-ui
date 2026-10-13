# Proposal

Closes #892, a Task under #160 (Instruments that fail, not warn).

## Why

Every pull request adds its line to `docs/LEDGER.md` at one point, the end of the chronology. Two
open pull requests therefore conflict there, and each merge into main leaves every other open one
with a conflict GitHub cannot resolve. On 2026-10-13 the pull request of #885 conflicted twice in one
afternoon on that point alone, and each time it took a checkout, a merge of main by hand, a suite
and a push.

Two measurements chose the fix. A merge GitHub performs reads no attribute a tree commits: through
`POST /repos/eQuantic/equantic-ui/merges`, a file every tree marks `merge=union` conflicted where git
merges it, and a text file under `*.metal binary` merged where git refuses it. So a committed
`merge=union` changes nothing there. And the conflict is the shared point and nothing else: with
`git merge-file`, a branch whose entry is the chronology's last conflicts with an entry main gained,
and the same branch with its entry one place up merges.

## What Changes

- An event is a file of its own under `docs/ledger/`, holding the entry as the chronology will hold
  it: one dated list item that cites its issue or pull request. A file nobody else adds never
  conflicts.
- A release folds the files into the chronology of `docs/LEDGER.md`, in date order, and removes
  them: `./scripts/fold-ledger.sh`, beside `./scripts/public-api.sh ship` in the release's recipe.
  The release is the chronology's one writer.
- `LedgerTests` fails on a file that is not one dated event citing its evidence, naming the file
  and what it lacks.
- `docs/ledger/folded.json` records the chronology as the last fold left it, and `LedgerTests`
  fails when the chronology differs, naming where an event goes. `EQ_FOLD_LEDGER=1` folds and
  records, and `EQ_UPDATE_LEDGER=1` records a deliberate change to the chronology itself.
- The Workflow section of `CLAUDE.md` and `AGENTS.md`, the release recipe in `CLAUDE.md`,
  `CONTRIBUTING.md`, the pull request template, `openspec/config.yaml` and the ledger's own
  "How to add an entry" say where an event is written.

For a developer who uses the SDK: nothing changes. This reaches no part of what ships (not eqc, the
runtime, a realizer, a shell, an SDK or a template), and neither the public surface nor the
developer surface moves.

For whoever opens a pull request here: the entry is written in a new file instead of at the end of
`docs/LEDGER.md`. An open pull request that wrote it the old way moves it once, when the comparison
fails after its next merge of main.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `working-agreement`: an event of the ledger is a file of its own until a release folds it into the
  chronology, and the chronology changes through the fold.
