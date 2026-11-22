- **2026-11-22 · A closed dialog gives the focus back, and to the control in its opener's place when
  the opener is gone**: the web's trap gave the focus back to the control that opened a dialog, and
  gave up when that control had left the page, such as a row the dialog deleted. The focus then fell to
  the body. Photon recorded no opener at all, and a modal layer neither took the focus when it opened
  nor gave it back when it closed. On both targets now, a closed modal layer gives the focus to its
  opener, or, when the opener is gone, to the control now at its place among those Tab reaches beneath
  the layer. That is the next row's same control, or the last one, and inside a dialog that stays open,
  never the page. Photon's layer also takes the focus when it opens, on `InitialFocus` or its first
  stop, and keeps the ring as it was. A reader on r/csharp asked for this as a side-by-side demo.
  `ModalFocusReturnTests` and `focus-trap.spec.ts` run the same rows: every case fails without the
  change, and the deleted-row cases fail with a give-back that has no fallback. This pull request
  carries the Tab walk of #1066 and the Escape of #1181, which waited on a branch since 10/11
  ([#1811](https://github.com/eQuantic/equantic-ui/issues/1811)). Proposed and archived through
  OpenSpec (`openspec/specs/modal-focus`).
