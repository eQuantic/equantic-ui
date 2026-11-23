# Proposal

Closes #1752, a Bug under #1587.

## Why

`scripts/new-issue.py` creates an issue, then adds it to Project 11 with `addProjectV2ItemById` and
sets its status and points on the item the add answers. The board's own workflow adds every new
issue too. On 2026-11-18, creating the 127 issues of the 3D epic (#1624) in a row, the two adds ran at
the same moment for #1645: GitHub refused the script's with `Content already exists in this project`,
the script exited 1 with "created, but not whole", and the issue's points had to be set by hand.

Adding an issue the board already holds is not refused: GitHub answers the item it has (measured on
#1752). The refusal is a race between two adds, and the add is safe to make again.

## What Changes

- When the add is refused with `Content already exists in this project`, the script makes it again
  three seconds later, up to three times, and sets the status and the points on the item it answers.
- Any other refusal is reported at once, as before, and so is this one when every try meets it.
- A test runs the script whole against a board that answers in memory, and CI's `roadmap` job runs it.
- The body file is read with `Path.read_text`, which closes it (the test's standard error showed the
  `ResourceWarning` of the file left open).

## Impact

- `scripts/new-issue.py`, `scripts/new_issue_test.py`, `.github/workflows/ci.yml`. The SDK, its
  public surface and its developer surface do not move, and an app writes nothing new.
- `docs/PRINCIPLES.md`: an instrument that fails, not one that warns. The test fails without the fix
  and with the two wrong fixes measured (retrying every refusal, and leaving the item unset).
