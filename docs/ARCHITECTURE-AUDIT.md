# Nine doors to one vocabulary

The companion to [FLUTTER-PARITY.md](FLUTTER-PARITY.md), asking the other half of the same question.
That file asks *how does Flutter solve this?* and answers across Flutter's whole surface. This one
asks *where is our own structure weak?* and answers from counts taken off the tree.

Both are architecture, both are measured rather than recalled, and both are pinned so they cannot
rot: the parity rows by `FlutterParityPinTests`, the coverage claims below by
`VocabularyCoverageTests`.

---

## The finding

The SDK has ONE visual vocabulary — 39 public node types in `eQuantic.UI.Primitives` — and several
places that decide what each word means. Each is a switch over the node type. Each covers a
different subset. Until the pin below, nothing made them agree, and **a deliberate omission and a
forgotten one looked identical** to a reader and to the build.

| Dispatch | Assembly | Covers | Decides |
|---|---|---|---|
| `WebRealizer` | `UI.Web` | 37 / 39 | what DOM this becomes |
| `LayoutEngine` | `Native.Framework` | 36 / 39 | how big it is, and where |
| `PhotonRealizer` | `Native.Components` | 17 / 39 | what the GPU draws |
| `Semantics` | `Native.Components` | 13 / 39 | what a screen reader says |

Plus five more inside `LayoutEngine` alone — `MinContentWidth`, `Shrinkable`, `WidthKind`,
`CrossSizeKind`, `PositionedOf` — each re-answering "what kind of node is this" in its own switch.

Low coverage is not itself a fault. `PhotonRealizer` paints LAID-OUT nodes, so a container that the
layout pass already resolved into geometry has nothing left to draw. The fault was that this
reasoning lived in nobody's head and in no test.

### What it cost

Six defects, one cause. Four were found by someone looking at a running application, not by us.

| Node | What happened | Found by |
|---|---|---|
| `Canvas.Label` | A labelled grip was silent to VoiceOver | a consumer, counting a11y elements |
| `Image.Label` | Emitted nothing on Photon while the web carried `alt` | an audit |
| `Vector` · `Drawing` · `CameraPreview` | Three more of the same, in one sweep | asking the assembly |
| `Text.Align` | Honoured by the web, dropped by all three native text services | a cross-pin (#103) |
| `VisualNode.Key` | Documented as reconciler identity, read by neither realizer | an audit (#95) |
| `Navigable` · `Overlay` | Open: honoured by the web, silent on Photon | still open |

### The asymmetry it exposes

CLAUDE.md names the transpiler as the bar for the rest of the SDK. The transpiler has this exact
problem — dispatch over an open set of constructs, extended constantly, two implementations that
must agree — and answers it with 152 strategy files, a registry, and a coverage suite that
enumerates by reflection against a baseline that may only shrink.

The realizers had the same problem and none of that machinery. The bar was stated and not applied to
the half of the SDK where the vocabulary lives.

### What now holds it

`VocabularyCoverageTests` asks the ASSEMBLY for the vocabulary and the SOURCE for each dispatch's
cases. A node with no case must be named in that dispatch's exemption list with its reason, and the
list may only shrink — three assertions, both directions:

- a node with neither a case nor an exemption fails, naming it;
- an exemption for a node that IS now handled fails, so the list cannot become a record of what
  somebody once believed;
- an exemption for a node the vocabulary no longer has fails, which is the same rot from the far end.

A bare mention does not count as coverage: the matcher takes the two shapes C# offers for type
dispatch and nothing else, because `WebRealizer` names `SemanticRole` in a comment while sitting in
an assembly that cannot reference it.

A/B'd against the real defect: removing the `Canvas` case from the semantics walk fails the pin with
`Unaccounted: Canvas`.

---

## Open findings

### `SemanticRole` lives inside one realizer

`SemanticRole` — what a node IS to assistive technology — is declared in
`eQuantic.UI.Native.Components`, a target-specific assembly. Accessibility is not target-specific.
The web emits the same decisions as ARIA, 44 times, inline, and cannot reference the enum; the
evidence is already written by hand at `WebRealizer.cs:1123`, a comment pointing at a type the file
cannot name in a file its assembly cannot see.

The measured consequence is the last row of the ledger above. `Navigable` and `Overlay` stay mute on
Photon because closing that gap needs a new `SemanticRole` member, and the vocabulary it belongs to
sits where only one of its two callers can reach it.

**Move:** `SemanticRole` and `SemanticNode` to `Primitives`, per-target bridges stay where they are.

### The transparent-wrapper set is written down in its consumers

"A layout-transparent wrapper" — `Pressable`, `Adjustable`, `Link`, `Positioned`, `InFlow`,
`Presence`, `DragDismiss`, `Draggable` — is enumerated 8 of 8 in `WebRealizer` and 6 of 8 in
`LayoutEngine`. It is a fact about the vocabulary, held by the code that consumes it.

### 58 public types in one file

`Primitives/Nodes/VisualNode.cs` is 2,213 lines and declares 58 public types, while the same folder
holds 23 other files. Readability rather than architecture — but it interacts with the finding
above, because a vocabulary that is hard to enumerate by eye has dispatches that are hard to check
by eye, and the review that would have caught `Canvas` is exactly the review a 2,213-line file
defeats.

---

## Measured and healthy

Worth recording, because an audit that only lists faults misleads about the whole.

**The realizers do not re-derive the design system.** Every shared decision was probed across web,
Photon and email: variant colours, shape scale, type role and disabled opacity resolve in one place
above all three (0 occurrences each). Elevation (6 web / 2 Photon) and density (4 Photon) are
resolved in more than one place and are worth a look, but they are ordinary duplication rather than
a structural fault. The write-once architecture is working where it matters; what was duplicated is
the type DISPATCH, not the decisions.

**The vocabulary keeps its own rule.** Across all of `Primitives`, only two files mention a target's
word at all, and both are the escape hatch describing itself. The "speaks no target's language"
discipline is holding.

---

## Order of attack

1. **The pin, before any refactor** — done, and it is the instrument the rest needs in order to be
   safe.
2. **`SemanticRole` to Primitives**, which unblocks the container-role decision holding `Navigable`
   and `Overlay` mute.
3. **Split `VisualNode.cs`** along the lines the vocabulary already has.
4. **Hoist the transparent-wrapper set** onto the vocabulary, so its two consumers ask rather than
   remember.

Not yet measured: the compiler's own internals (24,619 lines, the largest assembly and the one
already holding the bar), the Server surface, the six shell assemblies, and the TypeScript runtime.
