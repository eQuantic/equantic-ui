# Design

## A row is not a line

A diff draws what its documents do not hold: padding that keeps two sides level, the other
document's removed lines between the ones that replaced them, a fold that stands for a run, and the
header of what a patch left out. So the view draws ROWS over a map (`CodeRows`) from its lines to its
rows, and every question a line used to answer (where it is drawn, which line a press is on, where
the caret steps) goes through the map. One block draws both sides, and a diff is two blocks over
two maps in one vertical scroll, so the sides cannot drift apart.

## Flutter's shortcuts answer for the focus

`docs/FLUTTER-PARITY.md` said SAME for `Shortcuts`, and it was not: Flutter's shortcuts answer for
the focus, a key travelling up from it, while ours answered for the whole page, the last one mounted
winning. Both kinds are needed: a dialog's Escape and a palette's ⌘K must answer wherever the focus
is, without being placed anywhere in particular. So `Shortcut` keeps answering page-wide, and
`FocusScoped` is Flutter's kind, for a component's own chords. The web stamps the subtree's root and
walks up from the focused element at the key, and Photon's binding carries the subtree's path,
compared with the focused path. The parity row says DIFFERENT now, and its probe asserts the member.

## One writer for a string

The text of a string literal was quoted in nine places, five of them with an incomplete escape.
The structural fix is the writer, not a tenth copy: `JsStringLiteral` is the one, in the IR, and the
copies are gone.

## Measured, not assumed

Each defect was seen failing before its fix: a browser walk of the sample page found the extended
pattern and the page-wide F7, the conformance suite found the line breaks, and the runtime's own
`tsc` found the record annotations. Two review claims did not survive a measurement (a negated
`IsNullOrWhiteSpace` does narrow in TypeScript, and a raw U+2028 does parse), and the second is
escaped anyway, for the reader.
