# Design

## Context

See proposal.md. The ledger is one file, and its rule is one entry per event, oldest first, so every
pull request writes at the chronology's end. The public API has the same shape and already has its
answer: additions wait in `PublicAPI.Unshipped.txt` and `./scripts/public-api.sh ship` folds them at
a release.

Flutter has no part to ask about a repository's own record. What it shows is the general form of
the answer: its release notes are gathered from the pull requests merged, so no pull request writes
a line another one writes. Projects that keep a hand-written changelog reached the same place by
giving each change a file of its own, folded at a release.

## Goals / Non-Goals

**Goals:**

- Two pull requests that each record an event never conflict over it, on GitHub as on a developer's
  machine, with nothing to resolve.
- The chronology stays one document, read top to bottom.
- The old habit, an entry written at the chronology's end, fails instead of working in silence.

**Non-Goals:**

- `PublicAPI.Unshipped.txt`. Two pull requests that add API to one project conflict there the same
  way. It is rarer, and the analyzer reads that file by its name.
- The requirement that a branch is up to date with main before it merges (#287). A pull request
  still takes main in when main moves. What goes is the conflict that kept GitHub from doing it.
- Changing what an entry says. It is the same text, written in another place.

## Decisions

**A file, since GitHub reads no attribute.** Git has a driver for a file that only grows,
`merge=union`, and it is what made the merge work on one machine, from `.git/info/attributes`.
Committing it was the first candidate and it was measured before anything was built: three merges
through GitHub's own API between scratch branches, every tree carrying the same `.gitattributes`. A
file marked `merge=union` conflicted (409) where git merges it, the control with no attribute
conflicted on both, and a text file under `*.metal binary` merged (201) where git refuses it. A
merge there reads no attribute at all. A workflow that resolved the conflict on every open pull
request would push to branches other people work on, with a token that starts no CI run.

**The conflict is the shared point.** With `git merge-file`, which reads no attribute, a branch
whose entry is the chronology's last conflicts with an entry main gained, and the same branch with
its entry one place up merges. No rule gives each pull request a point of its own inside one file:
any order two open pull requests can be neighbours in puts them at the same point. A file of its
own is the one point nobody else writes.

**The file holds the entry as the chronology will hold it.** One list item: `- **<date> · <what
happened>**`, then what was measured and the link, every further line indented two spaces. The fold
is then a copy, with no second format to keep in step, and the file reads on GitHub as the entry it
will be.

**A release folds, and nobody else.** The chronology needs one writer or the conflict is back, and a
release already has a fold: `./scripts/public-api.sh ship`. `./scripts/fold-ledger.sh` sits beside
it in the recipe. A file that reaches main after a fold waits for the next one, so the fold is never
late for anything. Between releases the folder is the newest part of the chronology, and its
listing reads as one: a name per event.

**Order by the date the entry states, then by name.** The entry carries its date already, which the
ledger's own rule asks for (a link without a date is a story), so the fold reads no history and
writes the same order on any clone. Entries of one day have no order worth keeping.

**The chronology is compared with a record of the last fold.** The habit is strong and several
sessions hold it in their notes: an entry written at the chronology's end still works, and the
conflict comes back with it. So `docs/ledger/folded.json` holds the number of entries and the
SHA-256 of the chronology as the last fold left it, and `LedgerTests` fails when they differ, saying
where an event is written. It runs with the suite on every host and on a developer's machine. A
comparison of the pull request's diff against main was the other shape, and it needs a base ref
that a local run and a shallow checkout do not have.

**Folding and recording are two acts.** `EQ_FOLD_LEDGER=1` folds what waits and records.
`EQ_UPDATE_LEDGER=1` records and folds nothing: it is for a deliberate change to the chronology
itself, a citation that moved with its document or a retired plan's lines. One variable for both
would make such a change fold everyone's entries outside a release.

**The fold is in the suite, and the script calls it.** What an entry is has one reader,
`LedgerEntry`, which the guard and the fold share, and the fold's own cases run on the three hosts
with everything else. A shell or Python fold would be a second reader that Windows never runs.

**A fold that refuses one entry writes nothing.** Every waiting file is read before a line is
written. A file that is not an entry stops the fold, and so does an entry whose first line the
chronology already holds, which is what a fold run twice over the same files would write.

**The folder has a README.** GitHub shows it under the listing, where someone adding a file is
looking, and it keeps the folder there after a fold removed every entry.

## Risks / Trade-offs

- An open pull request that wrote its entry the old way fails the comparison after its next merge of
  main, once. It moves the entry into a file.
- `EQ_UPDATE_LEDGER=1` after writing an entry by hand makes the comparison pass. The failure's
  message says what that variable is for, and the diff shows the record changed outside a release.
- The fold writes no month heading. One written by hand is a deliberate change, recorded.
- Until a fold, the chronology's newest part is read as files. A release is cut every few days.
