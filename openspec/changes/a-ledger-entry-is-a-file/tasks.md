# Tasks

## 1. The measurements

- [x] 1.1 Three merges through GitHub's API between scratch branches: a file marked `merge=union`, a control with no attribute, a text file under `*.metal binary`, each beside what git answers here
- [x] 1.2 `git merge-file` over the real ledger: a branch's entry as the chronology's last, and one place up, against an entry main gained

## 2. The entry and its guard

- [x] 2.1 `LedgerEntry` reads a file under `docs/ledger/` and says what keeps it from being one dated event that cites its evidence
- [x] 2.2 `LedgerTests` fails on such a file, naming it and what it lacks, and each refused shape has a case, a file of another kind and a folder among them
- [x] 2.3 `docs/ledger/README.md` says what a file here is and how one is written
- [x] 2.4 This change's own event is the first file

## 3. The fold and its record

- [x] 3.1 `Ledger.Fold` writes every waiting entry after the chronology's last, by date and then by name, removes the files and records the chronology
- [x] 3.2 It writes nothing when one entry is refused, and refuses an entry whose first line the chronology holds
- [x] 3.3 `docs/ledger/folded.json` records the chronology, and `LedgerTests` fails when the chronology differs, with `EQ_FOLD_LEDGER=1` to fold and `EQ_UPDATE_LEDGER=1` to record
- [x] 3.4 `./scripts/fold-ledger.sh` runs the fold
- [ ] 3.5 Run the script in a scratch worktree over this tree's ledger and this change's own entry, and read what it wrote

## 4. The agreement

- [x] 4.1 The Workflow section of `CLAUDE.md` and `AGENTS.md`, and the release recipe in `CLAUDE.md`
- [x] 4.2 `CONTRIBUTING.md`, the pull request template, `openspec/config.yaml`, the row of `docs/README.md` and the ledger's own header and "How to add an entry"
- [x] 4.3 No wiki page names the ledger, so there is no wiki branch

## 5. The proof on GitHub

- [ ] 5.1 Two scratch branches that each add a file under `docs/ledger/` merge through GitHub's API, beside two that each add a line at the chronology's end
- [ ] 5.2 Archive the change with `openspec archive a-ledger-entry-is-a-file --yes`
