## Why

Closes #1568, a sub-issue of #197 (semantics and text direction), part of the block catalog 1.0 waits for.

The vocabulary already speaks in start and end (`EdgeInsets(Start, Top, End, Bottom)`, `Positioned.Start/End`,
`TextAlignment.Start/End`, `MainAlign.Start`), so a layout written for one direction is meant to read in the
other. Nothing makes it so: no page says which way it reads, the web writes start as `left` and end as `right`,
and a component cannot ask which way its text runs. An app in Arabic, Hebrew, Persian or Urdu lays out as if
it were English. `docs/FLUTTER-PARITY.md` marks `TextDirection.ltr/rtl` a GAP, and `docs/I18N-PLAN.md` (D9)
fenced right to left out of the i18n track and into the layout vocabulary: this is that work, for the web.

## What Changes

- **New vocabulary enum `TextDirection`** (`LeftToRight`, `RightToLeft`), appended-only like every enum of the
  vocabulary.
- **New vocabulary node `Directionality(TextDirection direction, VisualNode child)`**, an `InheritedComponent`,
  Flutter's name and shape: the subtree under it reads in that direction. Factory: `Directionality(direction, child)`.
- **`ComponentContext.TextDirection`** (C# and its TypeScript twin): the nearest `Directionality` above, or the
  page culture's direction when there is none. Reading it makes the component depend on it, as reading `Theme` does.
- **The culture says its direction**: the server writes `dir` beside `lang` on the page from
  `CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft`, the culture data it hands the browser carries the
  direction, and a culture switch in the browser sets the document's `dir` from the data it fetches. .NET is the one
  source; the browser keeps no list of languages.
- **The web writes start and end as the reader's start and end**, in both producers (the server's C# and the
  browser's TypeScript):
  - a `Directionality` lowers to a `display: contents` host carrying `dir`, so it adds no box;
  - padding and border widths whose start and end differ are written with `padding-inline` / `padding-block` and
    `border-inline-width` / `border-block-width`; equal ones keep the physical shorthand, which reads the same
    either way;
  - `Positioned.Start` and `End` are written as `inset-inline-start` and `inset-inline-end`;
  - `Positioned.ShiftX` moves toward the end: `translate(calc(var(--eq-inline) * x%), y%)`, where the stylesheet
    sets `--eq-inline` to `1` under `ltr` and `-1` under `rtl`, so a start-anchored child centred by a shift stays
    centred;
  - an anchored panel's centre placement is written with a physical centre (`left: 50%`), which reads the same
    either way.
- **What lays out by physical coordinates reads left to right**: the hosts of `TableViewport`, `SheetSurface`
  and `CodeSurface` carry `dir="ltr"`, and `CodeEditor`, `CodeBlock`, `Mermaid` and the spreadsheet's entry lines
  build inside `Directionality(TextDirection.LeftToRight)`. Code and diagrams read left to right in every language;
  a table and a sheet that mirror are a later slice.
- **Photon** reads the direction (`TextDirection` resolves the same way) and lays out as before: right-to-left
  shaping and mirrored layout on the native track stay outside 1.0, as the issue says.
- The dashboard sample gains Arabic (`ar`), and the `TextDirection` row of `docs/FLUTTER-PARITY.md` reads SAME
  for the web.
- No break: a type, a node, a factory, a context member and a visitor-free `InheritedComponent` are added.

## Capabilities

### New Capabilities

- `text-direction`: which way a page and a subtree read, and how the web lays out start and end in each.

## Impact

- `eQuantic.UI.Primitives` (enum, node, context member), `eQuantic.UI.Components` (factory, the left-to-right
  components), `eQuantic.UI.Spreadsheets`.
- `eQuantic.UI.Web` (lowering, `TokenCss`, `HtmlStyle`'s logical members, `ThemeStylesheet`),
  `eQuantic.UI.Runtime` (`lowering.ts`, `photon-context.ts`, `vocabulary.ts`, `utils/culture.ts`).
- `eQuantic.UI.Server` (the shell's `dir`, the culture data), `samples/DefaultUIDashboard` (Arabic).
