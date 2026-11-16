## Why

Closes #1567, a sub-issue of #197 (semantics and text direction), part of the block catalog 1.0 waits for.

A developer composing a row, a card or a list item has no word for "a reader hears this as one" or "a
reader passes over this". A switch drawn beside its label is two stops, a label and an unnamed switch,
and the initials an avatar draws are read before the name the row already says. The catalog's own
components get around it inside a `Pressable`, whose content is its name, and an app cannot: the gap is
two words in the vocabulary. `docs/FLUTTER-PARITY.md` marks `MergeSemantics` and `ExcludeSemantics` as
a GAP.

## What Changes

- **New vocabulary node `MergeSemantics`** (`eQuantic.UI.Primitives`): a layout-transparent wrapper whose
  subtree is heard as ONE stop. When a control is among its stops the stop is the first control, with its
  role, value, states and identity, named by everything the subtree says in reading order; with none, a
  `SemanticRole.Group` named the same way. A later control stays a stop of its own. Factory:
  `MergeSemantics(child)`.
- **New vocabulary node `ExcludeSemantics`** (`eQuantic.UI.Primitives`): a layout-transparent wrapper whose
  subtree a reader passes over on every bridge, with `Excluding` (true by default) to turn it off without
  changing the tree. Factory: `ExcludeSemantics(child, excluding = true)`.
- **The web** writes a merge with `aria-labelledby`: the parts a reader would have stopped on are hidden and
  named by ids, and the control, or the host as a `role="group"`, names itself by them. The browser computes
  the name, so it follows the content. An exclusion is a host marked `aria-hidden`. Both producers, the
  server's C# and the browser's TypeScript, write the same markup.
- **Photon** collects a merge's stops and joins them into one node, with the merge's bounds; an exclusion
  announces nothing.
- **The harness** (`eQuantic.UI.Testing`) reads `aria-labelledby` as the accessible-name rule does: an element
  naming itself is named as it would be with no reference, and a referenced part gives its label or `alt`
  before its text.
- `docs/FLUTTER-PARITY.md`: the row reads SAME.
- No break: two nodes, two factories and a visitor member are added (the visitor is the vocabulary's own,
  implemented by its realizers).

What a developer writes:

```csharp
MergeSemantics(Row(children: [Text("Wi-Fi"), Switch(_on, Toggle)]))   // one switch, named "Wi-Fi"
Row(children: [ExcludeSemantics(Avatar("JD")), Text("Jane Doe")])      // the initials are not read
```

## Capabilities

### Modified Capabilities

- `screen-reader-parity`: a merged subtree is one stop and an excluded one is none, alike on every target.

## Impact

- `eQuantic.UI.Primitives` (two nodes, visitor members, child slots), `eQuantic.UI.Components` (`UI` factories).
- `eQuantic.UI.Web` (lowering, `MergedReading`), `eQuantic.UI.Runtime` (`lowering.ts`, `nodes.ts`,
  `vocabulary.ts`, exports, generated node kinds and transpiled twins).
- `eQuantic.UI.Native.Framework` (measure), `eQuantic.UI.Native.Components` (emit, semantics),
  `eQuantic.UI.Email` (both reach a message as their child).
- `eQuantic.UI.Testing` (`AnnouncedTree` naming).
