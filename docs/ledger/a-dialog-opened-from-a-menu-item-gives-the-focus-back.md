- **2026-11-23 · A dialog opened from a menu item gives the focus back to the menu's trigger**: a row's menu
  offering Delete closed and opened its confirm in one pass, and Cancel gave the focus back to nothing. Two causes.
  A menu's item took the focus on its press and left with its menu, so the confirm recorded no opener; the items now
  leave the keyboard on the trigger, as the menu pattern keeps it there. And the browser reconciled its focus traps
  before the render manager wrote the pass's tree, so a dialog a render opened took no focus and recorded no opener
  until a later pass came, which also hit #1811's case; the traps now read the DOM the pass wrote
  ([#1812](https://github.com/eQuantic/equantic-ui/issues/1812)). Proposed and archived through OpenSpec
  (`openspec/specs/modal-focus`).
