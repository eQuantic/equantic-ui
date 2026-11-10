# Proposal

Closes #1351, a Bug under #1174 (a sub-issue of #157).

## Why

A merge of the wiki committed git's conflict markers into `Upgrading.md` on master (fixed by hand in the wiki's d7728c2), and every docs guard passed. Each guard reads what it is about, a version mark, a snippet, a diagnostic, and none reads whether a page is whole. A conflict left in a page is the failure a reader meets first, and nothing failed on it.

## What Changes

- `ConflictMarkerTests` reads every page of the wiki, in all five languages, and every Markdown file git tracks in this repository, and fails naming each file and line that holds a conflict marker: `<<<<<<<`, `|||||||` (diff3's base), `=======` and `>>>>>>>`, each alone on its line or followed by the space git writes before a side's name.
- Seven `=` alone on a line is also how Markdown underlines a title, so it is a marker only where an underline cannot be: inside a conflict another marker opened, under a blank line, or at the start of a page.
- Nothing a developer using the SDK writes or meets changes: the change is a test, and neither the public surface nor the developer surface moves.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `working-agreement`: the docs guards refuse a page a merge left in conflict.
