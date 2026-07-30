# Style Semantics Plan — Track S

**The directive (Edgar, 2026-07-30):** the way you write UI for mobile IS the way you write it for
web. That requires a well-consolidated technique for representing style WITHOUT writing CSS — a
semantics complete enough to reproduce ANY layout with no restrictions — and an SDK rendering
strategy that is highly performant, with no constant rewriting.

This document is that technique. It has three laws:

1. **One semantics.** A style is an abstract C# value (`BoxStyle`, layout props, roles). It never
   mentions CSS properties, pixels-as-strings, class names or platform concepts. The SAME value
   drives the DOM realizer and the Photon realizer.
2. **No expressiveness ceiling.** Anything a designer can draw must be expressible: flow, wrap,
   grid, constraints, aspect ratio, overlay, sticky, state variants, responsive variants, motion.
   When something cannot be expressed, that is a vocabulary bug — we extend the semantics, never
   tell the developer "drop to CSS".
3. **No rewrite, by construction.** Styles are immutable value structs → identical styles are THE
   SAME value. The web dedupes them into atomic rules generated once; the native engine memoizes
   layout/paint by style identity. Rendering cost grows with DISTINCT styles, never with element
   count or render count.

---

## 0. Audit — where we stand today (2026-07-30, v0.1.6)

**Vocabulary (Primitives):** `Box(BoxStyle)` — background token, 2-stop gradient, uniform inside
border, per-corner radius, Elevation 0–5, Clip, Padding, Width/Height (Hug/Fill/Fixed), Min/Max.
`Row`/`Column` — Gap, Main (Start/Center/End/SpaceBetween), Cross (Start/Center/End/Stretch),
`Flexible(flex)`, `Spacer`. `Stack`/`Positioned` (anchored overlay), `ScrollView` (one axis),
`Pressable` (pressed state), `LoopMotion` (SlideX), value transitions (Base 200ms). `Text` via
`TypeRole` only (spec A8). Tokens: Space/Radius/IconSize/Touch/Motion. Theme: `IAppTheme`
(colors/type/shape/elevation) + the SSR→client bridge.

**Missing for "any layout":** Wrap · Grid · AspectRatio · AlignSelf · Opacity · static Transform ·
state overlays (Hover/Focus/Disabled) · responsive/adaptive values · Sticky · explicit z-order ·
per-axis overflow.

**Emission today (the performance gap):** the web realizer and the runtime lowering emit an INLINE
STYLE OBJECT per element, with colors RESOLVED to `light-dark(#l,#d)` literals. Consequences:
repeated bytes in every SSR response; the reconciler diffs style attributes property-by-property;
zero reuse across elements ("100 cards = 100 identical style strings"); and the theme is baked into
markup instead of referenced. The token stylesheet (`--eq-*` vars, `.eq-type-*`, `.eq-elevation-*`)
exists but layout/paint does not reference it.

**Machinery we already have for the fix:** eqc compile-time evaluation (`CompileTimeEvaluator`) +
cross-assembly constant inlining (icon glyphs prove it); `ExtractedStyles` build channel (Tailwind
safelist rides it); the generated app stylesheet slot; immutable `record struct` styles.

---

## 1. The semantics — six orthogonal axes

A node's appearance is fully described by six axes. Every axis is abstract; every axis lowers to
both targets. App code composes axes; it never sees a platform.

### S-A · Layout axis

The flex model stays the backbone (one axis, Gap, Flexible). It gains:

```csharp
// WRAP — flow layouts (chip rows, tag clouds, galleries). RunGap spaces the wrapped lines.
new Row(gap: Space.S2) { Wrap = true, RunGap = Space.S2 } // Photon: line-breaking in LayoutEngine

// GRID — true 2D tracks. Track sizes reuse SizeValue (Fixed/Flex/Hug); spans are per-child.
new Grid(columns: [GridTrack.Flex(1), GridTrack.Flex(1), GridTrack.Fixed(240)],
         gap: Space.S3, rowGap: Space.S3)
{
    new Card(hero)   { GridSpan = new GridSpan(columns: 2) },   // spans two columns
    new Card(side),
    new Card(a), new Card(b), new Card(c),
};
// Auto-flow fills rows; explicit GridArea(col, row, colSpan, rowSpan) pins when needed.

// CONSTRAINTS — AspectRatio joins Min/Max (already present).
new Box(new BoxStyle { AspectRatio = 16f / 9f, Width = SizeValue.Fill }, media)

// SELF-ALIGNMENT — a child overrides the container's Cross for itself.
new Text("badge") { AlignSelf = CrossAlign.End }

// STICKY — scroll-anchored chrome (section headers). Lives on ScrollView children.
new Sticky(new SectionHeader("A")) // web: position:sticky; native: scroll compositor pins it

// Z-ORDER — explicit stacking inside Stack.
new Positioned(fab) { ZIndex = 1 }
```

Grid is what removes the last "you can't build that" — CSS Grid on the web, and a deterministic
track-sizing pass in `Native.Framework/LayoutEngine` (same math, golden-tested). Wrap and Grid are
the only layout additions Photon needs; everything else is arrangement over existing passes.

### S-B · Paint axis

`BoxStyle` gains the remaining paint primitives, all engine-fence-shaped:

```csharp
new BoxStyle
{
    Opacity = 0.85f,                                   // group opacity (engine layer alpha)
    Transform = Transform.Rotate(3).Scale(1.02f),      // static transform (composes; GPU-only)
    // Existing: Background/Gradient/BorderWidth+Color/CornerRadius/Elevation/Clip
}
```

Per-side borders stay OUT for now (the Photon SDF border is uniform-inside by design); a `Divider`
or a nested Box expresses the same visuals. Revisit only if a real screen can't be built.

### S-C · Type axis — unchanged

Roles only (`TypeRole`), resolved by the theme. Free-form font sizes remain outside the API.

### S-D · State axis — declarative variants, not event handlers

A state style is a DIFF applied over the base when the state is active. No listeners in app code;
each realizer implements the state natively (CSS pseudo-classes on web — zero JS — and the
interaction system on Photon):

```csharp
new Box(new BoxStyle
{
    Background = theme.Surface,
    Hover   = new StyleDiff { Background = theme.SurfaceSubtle, Elevation = 2 },
    Focus   = new StyleDiff { BorderWidth = 2, BorderColor = theme.FocusRing },
    Pressed = new StyleDiff { Background = theme.Colors(Variant.Primary).Pressed },
}, child)
```

Hover simply never fires on touch — same tree, correct behavior per input class. `Disabled`
remains token-level (`DisabledOpacity`), as today.

### S-E · Adaptive axis — responsive without media queries in app code

Responsiveness is a VALUE that varies by window size class, not a stylesheet concern. Size classes
follow Material's Compact / Medium / Expanded (already our design language):

```csharp
// Any style-bearing value can be adaptive:
var columns = Adaptive.Of(compact: 1, medium: 2, expanded: 4);
var pad     = Adaptive.Of(compact: Space.S4, expanded: Space.S8);

new Grid(columns: GridTrack.Repeat(columns, GridTrack.Flex(1)), gap: Space.S3)
{ … }

// Whole-subtree swaps use the same primitive at the component level:
body.Add(Adaptive.Node(
    compact:  new BottomNavigation(items, nav, onNav),
    expanded: new NavigationRail(items, nav, onNav)));
```

Lowering is the key: on the web, `Adaptive` values become STATIC media-query variants of the same
atomic rules at build time — no resize listeners, no JS re-render on breakpoint cross. On Photon,
the host resolves the window class and re-lays-out only when the class actually changes.

### S-F · Motion axis — already tracked

LoopMotion, shimmer, value transitions and the (native) transition animator stay as specced in
SHARED-COMPONENTS-PLAN §06/B14–B16. The atomic pipeline below carries their keyframes the same way.

---

## 2. The rendering technique — "no rewrite" made mechanical

The insight that makes everything fall out: **a style value is immutable and hashable, so identical
styles are the same style.** Each target exploits that its own way.

### Web: three tiers, one atomic stylesheet

**Tier 1 — build-time atomic extraction (the default path).** eqc already evaluates constant
expressions at compile time. Every style declaration it can evaluate statically is hashed
declaration-by-declaration into ATOMIC RULES — one rule per distinct declaration, app-wide:

```
C#:   new BoxStyle { Padding = EdgeInsets.All(Space.S4), CornerRadius = new(Radius.Md),
                     Background = theme.Surface }
emit: class="eq-p4 eq-rMd eq-bgSurface"
css:  .eq-p4{padding:16px} .eq-rMd{border-radius:10px} .eq-bgSurface{background-color:var(--eq-color-surface)}
```

Rules reference `--eq-*` variables, never resolved colors. Consequences, all structural:
- The stylesheet grows O(distinct declarations) — the 100th card adds ZERO bytes of CSS and three
  short class names of HTML, instead of a full style string.
- The stylesheet is THEME-INDEPENDENT and BUILD-CONSTANT → served once, cached immutably; the theme
  bridge keeps swapping only `:root` var values (`UseTheme` already emits them).
- State variants (S-D) and adaptive variants (S-E) are just more rules of the same atomic family
  (`.eq-bgSubtle:hover`, `@media …{.eq-gS8{gap:32px}}`) — generated at build, zero runtime JS.
- SSR and hydration agree by CLASS IDENTITY: the client lowering computes the same hashes, so
  hydration compares one string per element instead of a style object.

**Tier 2 — runtime memoized rule cache (dynamic-but-recurring).** A style computed from data at
runtime (`width: item.Progress`) goes through the SAME hash → class map, held by the runtime:
first occurrence inserts one CSSOM rule, every later occurrence (any element, any render) reuses
the class. Never removed, never rewritten — the map only grows with distinct values, and repeat
renders cost a dictionary hit.

**Tier 3 — per-element custom properties (continuously changing).** Values that change every frame
(drag offsets, animated progress) never touch rules: the rule is stable
(`transform: translateX(var(--eq-x))`) and the element updates ONLY the variable. The reconciler
diffs one var; the browser style engine fast-paths it; no rule churn, ever.

The reconciler's job shrinks accordingly: class-string compare (tier 1/2) + var compare (tier 3),
instead of property-by-property style diffing.

### Native: identity-keyed memoization

Photon has no CSS to generate — the same laws land as caches:
- Layout memoization keyed by (style value, constraints): unchanged subtrees skip Measure.
- Paint-object cache keyed by style identity (SDF params, gradients, shadows built once).
- Retained display lists: an unchanged subtree re-emits its recorded commands.
- Adaptive: re-layout only on a size-class threshold cross, not on every pixel of resize.

### The compiler's role

eqc is the hinge. Static styles ride the existing `CompileTimeEvaluator` + `ExtractedStyles`
channel: evaluation → atomic hashing → rules appended to the generated app stylesheet → the
transpiled node carries class references. What cannot be evaluated statically stays a plain value
and takes tier 2/3 at runtime — same semantics, graceful degradation, and the fail-on-unsupported
rule (EQ1xxx) keeps anything untranslatable a BUILD error, never silent.

---

## 3. Slices

- **S1 — Paint & constraint completeness (additive):** `Opacity`, static `Transform`,
  `AspectRatio`, `AlignSelf` — Primitives + both realizers + eqc lowering + goldens/cross-pins.
- **S2 — The atomic emission engine (the heart) ✅ (2026-07-30, core):** tiers 1½–3 landed.
  `StyleAtomizer`/`StyleSink`/`ThemeVarMap` (C#, eQuantic.UI.Web) + the byte-identical
  `style-atomizer.ts` twin (FNV-1a/base36, same var-rewrite, sorted classes) — cross-pinned by the
  shared `style-atomizer.fixture.json`. `HtmlStyle` split into `EnumerateDeclarations()` (regular)
  vs `CustomProperties` (tier-3 inline tail). Emission migrated at the two choke points
  (`AtomizeTree` post-pass in WebRealizer; `atomicAttrs` in the TS lowering) — the ~40 per-node
  style computations untouched. SSR arms an ambient per-page `StyleSink` (AsyncLocal) so bridges
  composed inside Core pages contribute to ONE `<style id="eq-atomic">` asset the client registry
  ADOPTS at boot (never re-inserts). Theme colors ride `var(--eq-color-*, resolved-fallback)`.
  Measured on the showroom: the WHOLE page = 118 rules / 4.8 KB, 178 class refs, 11 inline tails;
  re-renders after interaction added only the 6 genuinely-new declarations (memoization proven);
  the 100-card test pins O(distinct) growth. Remaining S2b: eqc BUILD-TIME extraction of static
  styles into the generated stylesheet (today rules are collected at SSR render / client runtime —
  same dedup and identity, one render later than build).
- **S3 — Wrap** (Row/Column `Wrap`/`RunGap`; Photon line-breaking pass; web flex-wrap atomic rules).
- **S4 — Grid** (`Grid`/`GridTrack`/`GridSpan`; Photon track-sizing pass, golden-tested; web CSS
  Grid; spans/areas).
- **S5 — State overlays** (`StyleDiff` Hover/Focus/Pressed on BoxStyle; web pseudo-class atomic
  rules; Photon interaction states; Pressable folds in).
- **S6 — Adaptive** (`WindowSizeClass`, `Adaptive<T>`, `Adaptive.Node`; web build-time media-query
  rule variants; Photon host window class + threshold-cross relayout).
- **S7 — Scroll semantics** (`Sticky`, `ZIndex`, per-axis overflow) — closes the "any layout" list.

Order rationale: S1 is a fast additive win; S2 lands the engine early so S3–S7 are born atomic
(each later slice pins its output once, already in final form).

## 4. Fences & honesty

- S2 re-pins every web realizer/lowering test once — planned churn, one slice, never again.
- Per-side borders, arbitrary clip paths, backdrop filters: out until a real screen demands them.
- The size-class thresholds ship with Material defaults; custom thresholds are a theme-level knob,
  not a per-component one.
- Photon Grid/Wrap must match CSS behavior where both exist — conformance via shared golden
  scenarios rendered by BOTH realizers (the Material-gallery pattern extended to layout).
