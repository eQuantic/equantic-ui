# Tasks

## 1. A script is read like the command it hides

- [x] 1.1 The hook reads the file of a script the command runs, and what that file runs one level down, and refuses `gh issue create` (a line or an argument list) or `createIssue` there; `scripts/new-issue.py` and the hook itself pass
- [x] 1.2 Verify: the self-test holds a script that creates (Python list, GraphQL, shell, run as a program, through a wrapper) and one that reads, comments or is missing; six of its cases fail against main's classifier; the tool that slipped through is refused on the branch and passed on main

## 2. Documentation

- [x] 2.1 `docs/ROADMAP-FORECAST.md` says the hook reads the scripts a command runs
- [x] 2.2 One entry for the ledger citing #1478, as a file of its own under `docs/ledger/`
