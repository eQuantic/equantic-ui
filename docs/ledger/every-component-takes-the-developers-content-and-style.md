- **2026-11-16 · Every component takes the developer's content, builders and style, and a guard holds it**: the
  rule is written beside the product principle and as principle 15, and its design answers what #1545 left open:
  content is a node with Flutter's slot names and a `string` converts to one, a repeated part has a builder while
  a named part keeps its name as its accessible name, and the instance's style is its family's slot record, the
  whole of it ([#1546](https://github.com/eQuantic/equantic-ui/issues/1546)). `CatalogCustomizationTests` names
  every member of the catalog that carries text and every component without a style of its own, 173 entries on the
  day it was written, in one file per family so the five family slices never share one, and the list only shrinks.
  The content defaults every family's content needs are cut as
  [#1577](https://github.com/eQuantic/equantic-ui/issues/1577). Proposed and archived through OpenSpec
  (`openspec/specs/catalog-customization`).
