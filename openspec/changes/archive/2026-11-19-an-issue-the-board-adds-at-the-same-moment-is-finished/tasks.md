# Tasks

## 1. The measure

- [x] 1.1 The refusal read from #1645's creation: `UNPROCESSABLE` on `addProjectV2ItemById`, "Content already exists in this project", the issue left with its type, parent, assignee and status, and no points
- [x] 1.2 An add of an issue already on the board (#1752) answers the item it has, the same id the issue shows, so a second add is safe

## 2. The fix

- [x] 2.1 The add made again on that refusal, three seconds apart, up to three times; any other refusal raised at once
- [x] 2.2 The body file read with `Path.read_text`

## 3. The proof

- [x] 3.1 `scripts/new_issue_test.py` runs `main()` against an in-memory board: the plain add, the race, the race every time, and another refusal
- [x] 3.2 The test fails without the fix, with a retry of every refusal, and with the item left unset
- [x] 3.3 CI's `roadmap` job runs it
