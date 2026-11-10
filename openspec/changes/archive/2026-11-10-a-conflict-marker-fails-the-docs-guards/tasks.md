# Tasks

## 1. The guard

- [x] 1.1 Read the markers of a text, a title's underline apart. Verify: a planted marker of each kind is found, a whole conflict at each of its three lines, and a Setext underline, eight-character runs and a marker in mid-line are not
- [x] 1.2 Read every page of the wiki and every Markdown file git tracks. Verify: both pass on the wiki's master and on main

## 2. Proved against the real thing

- [x] 2.1 Point the wiki guard at the commit before d7728c2. Verify: it fails naming `Upgrading.md`, and passes on master
- [x] 2.2 Plant a conflict in `docs/README.md`. Verify: the repository guard fails naming its line, and passes once the file is restored from a copy

## 3. Documentation

- [x] 3.1 One entry for the ledger citing #1351, as a file of its own under `docs/ledger/`
