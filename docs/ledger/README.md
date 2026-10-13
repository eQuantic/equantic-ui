# Events waiting for a fold

Each file here is one event the chronology of [`LEDGER.md`](../LEDGER.md) does not hold yet. A
release folds them into it, in order of date, and removes them (`./scripts/fold-ledger.sh`), so this
folder is the chronology's newest part.

## Why a file

Every pull request used to add its entry at the chronology's end, so any two open pull requests
conflicted there, and each merge into main left the others with a conflict to resolve by hand. A
merge GitHub performs reads no attribute that could tell it the file only grows (#892). A file of
its own is the one point nobody else writes.

## How to write one

Add a file named for the change, in lower-case words joined by hyphens: the slug of the pull
request's branch does it. It holds the entry as the chronology will hold it:

    - **2026-10-13 · What happened, as a sentence**: what was wrong, what changed and what was
      measured ([#892](https://github.com/eQuantic/equantic-ui/issues/892)). Proposed and archived
      through OpenSpec (`openspec/specs/working-agreement`).

One list item, opening with the day and what happened, every further line indented two spaces, and
the issue or the pull request cited. A second event is a second file. `LedgerTests` fails on a file
that is anything else, naming what it lacks.

Never write the entry into `docs/LEDGER.md` itself. The chronology is the fold's, `folded.json` here
records it as the last fold left it, and `LedgerTests` fails when the two differ.
