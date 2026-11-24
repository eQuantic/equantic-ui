## Context

The vocabulary is logical already: `EdgeInsets` holds `Start` and `End`, `Positioned` has `Start`/`End`
(and fractions), `TextAlignment` has `Start`/`End`, flex alignment is start and end. The web realizer, in both
twins (C# `WebLoweringVisitor*`/`TokenCss`, TypeScript `lowering.ts`), turns those into physical CSS in a few
places, each matching value for value across the twins:

- `TokenCss.Padding` / `paddingValue`: the 4-value `padding: Top End Bottom Start` (Box, Flex, Grid padding).
- `SideWidths` / `sideWidths`: `border-width: Top End Bottom Start` (base box and state diffs).
- Positioned `Start`/`End` through `TokenCss.Edge` / `edge()` to `left`/`right`; `ShiftX` to `translate(x%, y%)`.
- `ThemeStylesheet`: the anchored placements already use `inset-inline-start/end`; the centre placement mixes
  `inset-inline-start: 50%` with a physical `translateX(-50%)`.

Already direction-aware through CSS once a `dir` is set: flex rows and their alignment, `text-align: start/end`,
the anchored placements. Genuinely physical and left as they are: pointer points (`Anchored.At`, context
requests), caret marks, drag offsets, `LoopMotion`, `Transform2D`, corner radii and gradient directions (whose
vocabulary names are physical), safe-area insets (device edges). The table viewport, the spreadsheet surface and
the code surface place their content by physical x and scroll by `scrollLeft`, whose origin moves under
`dir="rtl"`.

The culture reaches the shell (`<html lang>`, `UIExtensions.ServeAppShell`) and the browser
(`window.__EQ_CULTURE__`, `utils/culture.ts`, `/_equantic/culture/{name}.json` on a switch), never the build
context. Components read a value of their subtree through `InheritedComponent` (`Theme`, `context.Theme`,
`DependOn<T>`).

## How Flutter solves it

`TextDirection { rtl, ltr }` and `Directionality(textDirection, child)`, an `InheritedWidget` that
`WidgetsApp` places at the root from the locale (`Localizations` decides it). `Directionality.of(context)` reads
it. Directional values (`EdgeInsetsDirectional`, `AlignmentDirectional`, `PositionedDirectional`) resolve against
it at layout. Here the vocabulary's values are directional already, so only the resolution is new.

## Decisions

1. **`TextDirection` and `Directionality`, Flutter's names.** The enum is appended-only (`LeftToRight = 0`,
   `RightToLeft = 1`). `Directionality` is an `InheritedComponent`, as `Theme` is, so a component reading it is
   rebuilt when it changes, and an app writes it exactly as the catalog does.

2. **The default is the culture's, from .NET.** `ComponentContext.TextDirection` is the nearest `Directionality`,
   else the UI culture's (`CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft` in C#; in the browser, the flag the
   culture data carries). The server writes it on `<html dir>` beside `lang`, the culture JSON (`__EQ_CULTURE__` and
   `/_equantic/culture/{name}.json`) carries `rightToLeft`, and `installCulture` sets `document.documentElement.dir`.
   Alternative refused: a list of right-to-left languages in TypeScript, a second source that drifts from ICU.

3. **The web resolves start and end in CSS, not in the realizer.** Logical properties make an atomic class mean the
   same in both directions, so the server and the browser write the same classes whatever the direction, and a
   `Directionality` that changes writes one attribute rather than lowering its subtree again. Alternative refused:
   resolving start to `right` in the lowering, which ties every class to a direction and every descendant to a
   relowering when it changes.

4. **Symmetric stays physical.** Padding and border widths whose start equals their end keep the physical shorthand,
   which reads the same in both directions, and the classes of most of the catalog do not move. Only an asymmetric
   value is written with `*-inline` / `*-block`. `Positioned.Start`/`End` are always `inset-inline-*`.

5. **A shift moves toward the end.** `Positioned.ShiftX` is the half of the centring idiom (`StartFraction = 0.5`,
   `ShiftX = -0.5`) that a physical `translate` breaks under `rtl`. It is written as
   `translate(calc(var(--eq-inline, 1) * x%), y%)`; the token sheet sets `--eq-inline: 1` on `:root` and `[dir=ltr]`
   and `-1` on `[dir=rtl]`, inherited, so a nested `Directionality` flips it. Its documentation changes from "right"
   to "toward the end". The anchored centre placement becomes `left: 50%; transform: translateX(-50%)`.

6. **A `Directionality` adds no element.** It writes `dir` on its child's own element, which keeps it when it already
   says a direction, so the nearest `Directionality` wins, and so does a surface that lays out left to right. A host
   of its own, even one with `display: contents`, would take what a parent writes on its child (an `align-self`, a
   grid span, a stretched height, a drag's offset), and a box that draws nothing ignores all of it; every code view
   builds inside one, so the host lost those on every code block. A partial render keeps the attribute, since it
   replays on a component's new root what its parents wrote there.

7. **What places content by physical x reads left to right.** `TableViewport`, `SheetSurface` and `CodeSurface`
   hosts carry `dir="ltr"`: under `rtl` their overflow would open to the left and their `scrollLeft` would change
   origin. `CodeEditor`, `CodeBlock`, `Mermaid` and the spreadsheet's entry lines, which place overlays with
   `Positioned.Start` from physical x, build inside `Directionality(LeftToRight)`: code and diagrams read left to
   right in every language. A table that mirrors is its own slice.

8. **Photon reads, and does not mirror yet.** The context member resolves the same way on Photon, so #1569's
   components can read it on both targets; Photon's layout and shaping stay left to right, outside 1.0.

## Risks / Trade-offs

- `SafeArea`'s start and end stay the physical left and right device insets on the web.
- A horizontal `ScrollView`'s offset is `scrollLeft`, whose origin moves under `rtl`: a requested offset or a
  reported one reads from the right there. #1569 (gestures and keys) owns the direction of scrolling.
- An app's own escape-hatch markup keeps whatever sides it writes.
