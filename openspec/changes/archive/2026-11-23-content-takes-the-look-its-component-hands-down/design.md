## Context

`BoxStyle.Foreground` is the colour a text, an icon, a vector or a spinner with no colour of its own takes from the
nearest box that states one (#614). Photon carries it down its paint walk (`EmitState.Foreground`); the web writes it
as the box's `color`, which the DOM inherits. The type a `Text` is set in is resolved per node from its own role
(`EffectiveTypeStyle.Resolve`), in four places: Photon's measure and paint, the web's lowering (server and browser),
and the email realizer.

How Flutter solves it: `DefaultTextStyle` and `IconTheme`, two inherited widgets a Material button wraps its child in.
A `Text` merges its own `style` over the nearest `DefaultTextStyle.style`, and takes `maxLines` and `textAlign` from
it when it names none; an `Icon` takes its size and colour from the nearest `IconTheme`. docs/FLUTTER-PARITY.md,
section 3, the `WidgetStateProperty` row, already records the colour half as `BoxStyle.Foreground`.

## Goals / Non-Goals

**Goals:**

- A `Text` and an `Icon` that name nothing look like the component around them, on every target, and a `string`
  passed where a node is taken is that `Text`.
- Every tree written today draws exactly as it did.

**Non-Goals:**

- Changing a component. The family slices adopt it as they take their content as a node.

## Decisions

### 1. On the box, beside the colour

`BoxStyle.Content` is a `ContentStyle` with `Role`, `Type` (a `TypeStyle`), `MaxLines`, `Align` and `IconSize`, every
member optional. The box is where a component resolves its look and already states the colour; one place states the
look. Rejected: a separate inherited node per Flutter's shape. It is a new kind every visitor of the vocabulary must
answer, for a value that has the same owner as `Foreground`. The colour stays where it is, because a state's diff
changes it (`StyleDiff.Foreground`) and nothing here changes per state.

A box inside another lays its `Content` over the outer one member by member (`ContentStyle.Merge`, the shape of the
theme slots' `Merge`), as Flutter's `DefaultTextStyle.merge` does: a button's label row that states only the line
limit keeps the type its button stated.

### 2. What a node that names nothing takes

`Text.Under(ContentStyle?)` answers the look a text is drawn in, and every producer asks it:

- the role: its own, else the box's, else `BodyL`;
- the type style: its own `StyleOverride`, else, when the text took the box's role, the box's `Type`; a text that names
  its own role keeps that role's type whole, because a box's `Type` was written for the box's role;
- the line limit: its own, else the box's, else none;
- the alignment: its own, else the box's, else the start.

`Icon.SizeUnder(ContentStyle?)` answers its own size, else the box's `IconSize`, else 24. `EffectiveTypeStyle.Resolve`
takes the `ContentStyle` in effect, so the merge of the face, the code face and the slant stays one function.

### 3. Each producer carries the look down its own walk

The look follows the vocabulary tree, the same on every target: Photon's measure keeps it on the visitor while it
measures a box's child, and its paint on `EmitState` beside `Foreground`; the web's lowering keeps it on the visitor on
the server and on the `LoweringContext` in the browser, which a lowered record keeps, so a component the browser lowers
again alone (E3b-3) is lowered under the look it was first lowered under; the email realizer keeps it on its visitor.

Rejected: CSS inheritance on the web for the type. The line limit and an icon's size are not inherited properties, a
role's class would then sit on the box as well as on the text, and two mechanisms answering one question is how the
targets drift. The text is lowered with the role it resolved, as if it had named it.

### 4. A `string` is a node

`VisualNode` gains `implicit operator VisualNode(string text) => new Text(text)`. C# allows a user-defined conversion
into an abstract class from an unrelated type, and eqc carries a vocabulary type's conversion to the static its
runtime twin carries (`IconGlyph.fromIcons` is the precedent), here `VisualNode.fromString`.

### 5. The members widen; nothing keeps the old shape

`Text.Role`, `Text.MaxLines`, `Text.Align` and `Icon.Size` become nullable, the constructors' defaults null. A
reader that used them reads `Under` instead. The runtime's twins take `null` for the same parameters.

## Risks / Trade-offs

- [A realizer reads `text.Role` and skips `Under`] → `Role` is nullable now, so a reader that compares or formats it
  fails to compile or meets the null; the conformance suite runs the five producers on one tree.
- [A transpiled call that omitted the role changes its emitted default] → the transpiled pins are regenerated and must
  differ only in the defaults eqc writes; the conformance suite runs both sides.

## Migration Plan

Nothing an app writes changes. A reader of `Text.Role`, `Text.MaxLines`, `Text.Align` or `Icon.Size` reads
`text.Under(null).Role` (or the look in effect) and `icon.SizeUnder(null)`.
