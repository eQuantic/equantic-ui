## Why

Closes #1546, the first slice of the feature #1545 (every ready-made component takes the developer's own content,
builders and style), under the epic #898 (1.0: the web SDK stable). Edgar decided on 2026-11-15 that this goes
into 1.0, because widening a constructor of a contract assembly is free in preview and costs a major once the
version is stable.

Measured on main by this slice's guard: 42 of the catalog's 65 components take text where a developer may want a node
(`Button(label)`, `ListItem(title, subtitle)`, `Dialog(title, body)`, `Tabs(labels)`, `Select(options)` and the
rest), only `ListView` and `ListDetail` take a builder, and none takes a style its factory reaches (`Markdown`'s is an initializer's). Flutter takes
a widget in each of those places, an `itemBuilder` for what a list repeats, and a `ButtonStyle` beside the
widget. A developer who wants an icon beside a dialog's title, an option drawn with a flag, or one button in the
brand's other colour has no way to say it today but to copy the component.

Five family slices (#1547 to #1551) change the components. They run in parallel once this one is in, so this one
settles the shape every one of them follows, writes the rule where every session reads it, and puts a guard on
the tree that fails before any of them starts.

## What Changes

- **The design** (`design.md`): content is a node, with Flutter's slot names, and a `string` converts to one; a
  repeated part is drawn by a builder, and a part with a name keeps the name as a string; the instance's style is
  the family's slot record, laid over the theme's with its `Merge`, and that record is the whole instance style;
  everything is a constructor parameter. It answers the three questions #1545 left open.
- **The rule in CLAUDE.md**, beside the product principle, and its short form as principle 15 of
  `docs/PRINCIPLES.md`.
- **A guard that fails**: `CatalogCustomizationTests` reads every component of the catalog (`eQuantic.UI.Components`,
  `eQuantic.UI.Charts`, `eQuantic.UI.Spreadsheets`, derived from the build output) and every item type a
  component takes, and fails on a member that carries text and on a component without a `style` of its own,
  unless a family's file under `tests/eQuantic.UI.Web.Tests/Coverage/catalog-customization/` lists it by hand with
  its reason. The list may only shrink, it is split one file per family so the family slices never edit one
  file, and a new component belongs to a family before the test passes.
- **The prerequisite cut as its own slice**, #1577: the type and the icon size a component hands its content,
  beside the colour `BoxStyle.Foreground` already hands down, and the `string` conversion. A family slice's style
  and builders do not wait for it; its content does.
- **The families re-cut by slot record**, as #1545 allowed: each component that no family slice named joins the
  slice whose theme slot styles it (`FormSubmit`, `ProgressBar`, `PullToRefresh` and `Skeleton` join #1547;
  `Calendar`, `DateTimePicker`, `FormInput`, `FileUpload` and `CultureSwitcher` join #1548; `PageIndicator`
  joins #1549; `Avatar`, `Badge`, `ContextMenu` and `CookieConsent` join #1550; `List`, `ListView`, `ListDetail`,
  `Divider` and `SwipeableRow` join #1551). The tree's builder and style stay with #1532 and #1533; what the
  charts and the spreadsheet owe is the parent's.

Nothing reaches eqc, the runtime, the realizers, the shells, the SDKs or the templates. Neither the public surface
nor the developer surface moves: this slice changes no component.

## Capabilities

### New Capabilities

- `catalog-customization`: what the catalog's components owe a developer who customizes them, and the guard that
  holds it.

### Modified Capabilities

None.

## Impact

- `CLAUDE.md`, `docs/PRINCIPLES.md`: the rule.
- `tests/eQuantic.UI.Web.Tests/Coverage/CatalogCustomizationTests.cs` and `catalog-customization/`: the guard and
  its nine family files, 173 entries.
- Principles followed: 2 (an SDK, not a solution made to measure: a component a developer cannot customize is
  one they copy), 6 (Flutter's answer first), 9 (an instrument that fails), 13 (in preview, the family slices
  widen each constructor and leave nothing behind), and the new 15.
