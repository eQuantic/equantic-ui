## Context

The code editor's engine left `Primitives` in #359: `CodeSurface` depends on `ICodeSurfaceModel`,
the engine lives in `eQuantic.UI.Code`, and neither host references it. The spreadsheet is the other
half of that sentence. Today `SheetSurface` carries a `SheetController` and the window's origin
(`FirstRow`, `FirstCol`, `HeaderWidth`, `HeaderHeight`); `PhotonHost` and the web's
`lowerSheetSurface` each keep a drag flag and a fill flag, each turn a point into a cell with the same
prefix sums over the document's sizes, each decide what a double click and a press on the fill handle
mean, and each call `SheetKeymap` and the TSV methods directly.

*How does Flutter solve it?* `TextEditingController` and `EditableText` own the editing protocol and
`RenderEditable` paints what it is told (docs/FLUTTER-PARITY.md, the `TextEditingController` row,
which says the sheet "has not yet" taken that road). The code editor's answer to it, and this one, is
the same: the node depends on an interface stating what a host may tell the engine, and the engine
lives above the vocabulary.

## Goals / Non-Goals

**Goals:**

- One engine assembly, `eQuantic.UI.Sheet`, referencing `Primitives` only, transpiled into the
  runtime like `eQuantic.UI.Code`, one type per file.
- A host forwards the platform's events and decides nothing a press or a key means.
- The behaviour a person sees is today's on both targets, measured by the existing sheet suites.

**Non-Goals:**

- No new spreadsheet behaviour: no IME composition in a cell, no Ctrl+Y redo, no shift-drag, no
  reveal of the active cell on keyboard moves. Each is a later change against one engine instead of
  two hosts.
- No keyboard convention in the protocol (see Decisions).
- The forms' models stay in `Primitives` with `[TwinIsTranspiled]`; they are not part of #185.

## Decisions

**1. The engine's assembly mirrors `eQuantic.UI.Code`.** Folders `Document/` (the cell, the range,
the document and its snapshot, the edit, its kind, the history, the TSV codec) and `Editing/` (the
controller, the keymap, the motion, the axis, the grid). Namespace `eQuantic.UI.Sheet`. It joins
`RuntimeProvidedTypeScanner.VocabularyNamespaces` and `RuntimeAssemblies`, so eqc imports its types
from the runtime, writes its enums as unions and builds its records the vocabulary's way, exactly as
for the code engine. *Alternative:* keeping the namespace `eQuantic.UI.Primitives` inside the new
assembly would spare apps the `using`, and would leave the namespace lying about where a type lives,
which is the confusion `[TwinIsTranspiled]` was invented to patch (#592).

**2. `ISheetSurfaceModel` carries six members, each one a call a host makes today:**
`HandleKey(key, modifiers, clipboard)`, `HandleText(text)`, `CopyText()`, `Cut()`, `Paste(text)` and
`HandlePointer(phase, position, modifiers, clicks)`. The shapes are `ICodeSurfaceModel`'s, so a
realizer drives both surfaces the same way. What the code surface has and the sheet does not is left
out on purpose: no carets to paint (the component draws the draft and its bar), no reveal or focus
versions (nothing asks for them yet), no composition (the sheet types committed text), and no
keyboard convention, because Excel's chords are one keyboard in both traditions once
`KeyModifiers.Command` stands for ⌘ or Ctrl, so the engine would read a parameter for nothing.
*Alternative:* copying `ICodeSurfaceModel` whole, which would commit the 1.0 contract to members no
implementation honours.

**3. The window is the engine's: `SheetGrid(Point Origin, int FirstRow, int FirstCol)`.** The
component hands it in `Build`, before it reads the selection, as `CodeEditor` hands `CodeGrid`. With
the document's sizes it places every cell, so `CellAt(point)` and the fill handle's corner exist once.
The four window properties of `SheetSurface` go; `Origin` covers what `HeaderWidth`/`HeaderHeight`
did for a child that draws its own headers. *Alternative:* keeping the window on the node and
passing it with every pointer call, which keeps arithmetic in the protocol's caller.

**4. The gesture is the engine's.** A press records whether it is selecting (a plain single press)
or filling (on the handle); a move answers whether anything changed; a release lands the fill. The
host keeps one flag, that a press began in a sheet, and routes the press's moves and release to that
sheet until it lifts, as it does for a code surface (`_codeDragging`).

**4b. A press can be taken away.** `PointerPhase` gains `Cancel`, after its five members as the
vocabulary's enums require: the system ends a press without lifting it, and neither model may land
what the press was doing. Flutter's `PointerCancelEvent` is the same thing. *Alternative:* a release,
which would pour a fill nobody let go of.

**5. The TSV members take the protocol's names.** `CopyTsv`/`PasteTsv` become `CopyText`/`Paste`,
and `Cut` copies then clears. No alias stays (CLAUDE.md, no member survives to keep an old shape).

**6. The clipboard chords move into the keymap.** `SheetKeymap.Handle` takes the modifiers and the
clipboard, as `CodeKeymap.Handle` does, and claims ⌘C/⌘X/⌘V only with a clipboard. Escape outside an
edit stays unclaimed, so Photon leaves the sheet and the browser does what it does.

**7. `[TwinIsTranspiled]` leaves the moved types.** Every site that reads it also asks
`IsTranspiledNamespace`, which the new namespace answers; `TwinIsTranspiledTests` keeps holding the
attribute to the forms' folder.

**8. The SDKs add the package.** Both `Sdk.props` reference `eQuantic.UI.Sheet` beside
`eQuantic.UI.Code`, as a package and, beside a source tree, as a project; `Components` references it.

## Risks / Trade-offs

- [A press that began in a sheet with Shift or as a double click now owns the pointer until it lifts
  on Photon, so a drag after it no longer reaches a scroll view's pan] → the rule a code surface
  already follows; a plain press on a sheet already owned the pointer, and the scroll view still
  scrolls by its wheel and its bar.
- [The web lost `dblclick` and reads the click count off the press, as the code surface does] → the
  count is the platform's (`event.detail`), and the sheet suites dispatch it.
- [Line citations of `PhotonHost` in docs/HANDOFF-FIDELITY-AUDIT.md move when its sheet code
  shrinks] → `DocsIndexTests` names each one; they are rewritten in the same change.
