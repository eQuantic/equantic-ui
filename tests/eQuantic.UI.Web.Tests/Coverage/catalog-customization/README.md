# What the catalog still takes as text, and the components without a style of their own

`CatalogCustomizationTests` reads every component of the catalog and every item type a component takes,
and fails on a member that carries text or a component that takes no style of its own, unless this folder
lists it. The rule it holds is CLAUDE.md's product principle: every ready-made component takes the
developer's content as a node, a builder for what it repeats, and its family's slot record as `style`.

The list may ONLY SHRINK. Each family has a file of its own, so the family slices (#1547 to #1551) never
edit one file:

- `# owns:` names the components the family holds, and the item types that carry text through them. A new
  component belongs to a family before the test passes; the family is the one whose theme slot styles it.
- Each entry is `<id>  <kind>  <reason>`, two spaces apart. The id is `Type.Property` for a settable
  property, `Type(parameter)` for a constructor parameter no settable property carries, and the
  component's name for `style`. The kind is `text` or `style`.
- The reason names the issue that removes the entry (`#N`), or starts with `kept:` and says why the text
  is text by nature (a field's value, an accessible name, a source to parse, an identifier) or why the
  component has no look of its own.

An entry is written BY HAND. Regenerate with `EQ_UPDATE_CATALOG_CUSTOMIZATION=1` after a REMOVAL: it drops
the entries the catalog no longer measures, and the item types that carry no text any more, keeps every
other line where it was, and never adds one.
