# Changelog

## Unreleased

- **Native (Photon) projects preview too.** The design host compiles them against the reference list
  the native SDK now writes, and the extension bundles the browser runtime they don't carry. The
  toolbar says what it is looking at — *native project · web realizer* — and opens in the phone shell,
  because that is the shape the app ships in.

## 0.2.0 — 2026-08-15

- **A layers list** in the Explorer: the whole screen as a tree, each row naming where it is written
  (`PaymentsPage.cs:28`). Picking a row selects on the canvas; selecting on the canvas marks the row.
- **A toolbar of the preview's own**, with the inspect toggle (a real pressed state) and a **format
  selector**: phone, phone L, tablet, desktop — applied as the framework's own two axes (window size
  class and density), with the adaptive gates resolved against the chosen width and the tree
  re-mounted so density is honest. Phone formats render inside a device shell.
- **Drag between containers.** A node can leave its list for another one in the same file — one edit,
  one undo, re-indented to its new depth, refused with the compiler's own words when it would not
  compile there.
- **Duplicate** (Cmd/Ctrl+D), and a **keyboard** for everything: arrows walk the tree, Alt+arrows
  move the node, Delete removes, Esc lets go.
- **Every list, not only children**: a Grid's `columns` gets the same gestures, with a palette of the
  element type's own values. A `BoxStyle` opens as a key/value sheet — pick a key, type a value,
  remove a row.
- **Closed sets are selects**, whatever the type says: enums, and scale tokens like `Space.S3`
  (inferred from what is written, qualified the way the file qualifies it).
- **A pile is not a row**: inside a `Stack`, the drop mark covers the child the node would land in
  front of, the badge says *in front of Card*, and the panel's arrows read *Send backward / Bring
  forward*.

## 0.1.0 — 2026-08-15

- **Live preview of the unsaved buffer.** A long-lived design host holds the project's Roslyn
  compilation and compiles what is in the editor, not what is on disk; C# errors arrive as ordinary
  diagnostics, and the last good frame stays up while an edit does not compile.
- **Click to select**: every rendered element knows the C# expression that built it, and a click
  lands the editor's cursor on that exact span — in whichever file it is written.
- **A property panel that writes C#** through the document's own undo stack, with every candidate
  edit compiled before it is offered.
- **Insert, move, remove** in declarative `children: [ … ]` lists, with refusals that explain
  themselves before the affordance is drawn.
- Snippets: `eq-page`, `eq-comp`, `eq-stateful`, `eq-action`, `eq-row`, `eq-col`.
