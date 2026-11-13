- **2026-11-13 · A Select groups its options**: a `Select` took one flat list, so an app whose design
  draws the choices under headings built the panel by hand. It now takes `groups`, each an
  `OptionGroup(label, options)` drawn under its heading and announced as a labelled group on the web and on
  Photon (email walks into a group and announces nothing), with one index across the groups in reading order; a new `Group` node carries the
  label, and a `Select` wraps no group whose label is blank. A `Text` gains `ExcludeFromSemantics`, so the heading is read once. The constructor and the `UI.Select` factory widen (two `*REMOVED*` lines, no source change for a
  call that passed options, index and `onChanged`). A choice that answers the app's own value rather than
  its index is left out ([#1516](https://github.com/eQuantic/equantic-ui/issues/1516))
  ([#677](https://github.com/eQuantic/equantic-ui/issues/677)). Proposed and archived through OpenSpec
  (`openspec/specs/anchored-panels`, `openspec/specs/screen-reader-parity`).
