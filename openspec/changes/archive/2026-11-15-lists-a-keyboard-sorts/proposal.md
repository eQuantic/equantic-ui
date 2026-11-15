## Why

Closes #1536, a user story of #1534 (sortable lists and drag and drop across containers), under the epic
#1510. A list that only a pointer can sort shuts out a keyboard and a screen reader: the board, the list and
the tree of eQuantic Space must be sortable without a mouse, and each step must be heard.

## What Changes

- **Keyboard**: with the focus on a `SortableItem` (or on a control inside it), Space picks it up; the arrows
  along the list's axis move it one position; the cross arrows move it to the neighbouring list of its group,
  at the same position or the last; Space or Enter drops it; Escape, Tab or a pointer's press puts it back. The
  list reports the place while it moves (`OnHovered`), asks its guard (`Accepts`) before each place, and reports
  the drop (`OnDropped`), exactly as a pointer's drag does. After a drop the keyboard is put back on the item.
- **Announcements**: each step is said through one polite live region, in the reader's language, from the
  SDK's strings (`SdkResources`): how to start (the focused item's description), picked up, moved, refused,
  dropped, put back. An item is named by its own accessible name or text, a list by `Sortable.Label`, new.
- **Focus**: an item whose element takes no focus of its own (a box, not a button) becomes a Tab stop.
- **Photon**: unchanged, fenced with the pointer's drag (#1537).

## Capabilities

### Modified Capabilities

- `sortable-lists`: a keyboard sorts an item and a reader hears each step.

## Impact

- **Vocabulary**: `Sortable.Label`. `PublicAPI.Unshipped.txt` moves, one addition.
- **Components**: `UI.Sortable` gains `label`; `SdkResources` gains the six strings in every `.resx`.
- **Web realizer and its twin**: the list's label and the item's Tab stop in both lowerings; the keyboard
  path and the announcer in `dom/sortable.ts`.
- **eqc, Photon, SDKs, templates**: nothing.
- **Docs**: the wiki's Sortable section in every language, the parity row, a ledger entry.

No break. Rules of `docs/PRINCIPLES.md` followed: 4, 5, 14; bent as before: 4, for Photon (#1537).
