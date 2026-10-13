- **2026-10-13 · A ledger entry is a file, so two pull requests never write the same line**: every
  pull request added its entry at one point, the chronology's end, so any two open ones conflicted
  there and each merge into main left the others with a conflict to resolve by hand. The pull
  request of #885 met it twice in one afternoon. A merge GitHub performs reads no attribute a tree
  commits, measured through its API between scratch branches: a file marked `merge=union` conflicted
  where git merges it, and a text file under `*.metal binary` merged where git refuses it. And the
  conflict is the shared point alone: with `git merge-file`, a branch whose entry is the
  chronology's last conflicts with an entry main gained, and with its entry one place up it merges.
  An event is now a file of its own under `docs/ledger/`, which a release folds into the chronology
  in order of date (`./scripts/fold-ledger.sh`). `LedgerTests` fails on a file that is not one dated
  event citing its evidence, and on a chronology no fold wrote, saying where an event goes. Of the
  175 dated entries on record the guard accepts 171, every one since issues were cited
  ([#892](https://github.com/eQuantic/equantic-ui/issues/892)). Proposed and archived through
  OpenSpec (`openspec/specs/working-agreement`).
