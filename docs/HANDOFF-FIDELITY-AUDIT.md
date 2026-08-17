# Handoff fidelity audit — Photon Design System vs the shipped SDK

Generated 2026-08-16 by a 16-agent audit over the handoff's 47 component blocks
(`Photon DS - Phase A/B/C`, Claude Design project `6c1e920b`), one agent per component
family, each comparing the block's checkable claims against the component source and
resolving every token through `Primitives/Theme/Tokens.cs` before judging.

**Read the status column before acting on a row.** A second pass re-checked each claim
against the code adversarially; it covered 78 of the 294 before the run hit its session
limit. So:

- `CONFIRMED` — a skeptic reproduced the divergence from the code. 73 rows.
- `REFUTED` — the skeptic could not: the code actually matches, or the token resolves to
  the handoff's value. 5 rows, left in deliberately so the same claim is not re-filed.
- `unverified` — audited but never re-checked. 216 rows. The confirmed/refuted split above
  ran at roughly 14 refutations per 100, so expect most to be real and none to be trusted
  without reading the code.

`documented-deviation` means the component's own doc comment names and justifies the
difference. Those are review items, not bugs.

## Closed since the audit ran

Each of these has a test that names its handoff block, so the figure cannot drift back.

| Block | What was wrong | Where |
| --- | --- | --- |
| B2 ListItem | 52/68 heights only; divider inset a flat 16 instead of 16 + leading + gap | `ListHandoffFidelityTests` |
| C16 NavigationRail | the bar's metrics (56×26, Md 24, 11/700) and no trailing edge | `NavigationHandoffFidelityTests` |
| A13 IconButton · B8 Chip | `Selected` changed the paint and never reached `Pressable.Selected`, so no `aria-pressed` | `ToggleHandoffFidelityTests` |
| B2 ListItem · B8 Chip | no §10 hover on an interactive row or on any chip kind | `PointerContractFidelityTests` |
| B10 SearchField | the clear affordance was a 20dp glyph with a 20dp hit rect | `PointerContractFidelityTests` |
| A10 Icon | a labelled glyph carried a name and no `role="img"` | `DestinationSemanticsTests` |
| A11 Image | no `case Image` in the native semantics walk — alt text was silent on Photon | `GraphicSemanticsTests` |

### Found while fixing, not by the audit

**Hit slop is a promise nothing keeps.** `Touch.MinTarget`'s own doc says "visuals may be smaller —
the framework expands hit-slop symmetrically", and no realizer implements it: `grep` for
`MinTarget`/`HitTarget` outside `Tokens.cs` finds only components sizing their own target by hand
(`Slider.cs:98`, `PageIndicator.cs:88`, and now `SearchField`). Every small pressable that does NOT
do that by hand ships with its visual as its hit rect. That is a framework-wide §08 gap, larger than
any row in this file, and it is why the search field's clear button reaches 48 across and stops at
the pill's 40 down.

## Visible — a user or a designer can see this

83 rows.

### A3 Stack · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Native.Framework/Layout/LayoutEngine.cs`
- **Handoff**: Positioned(top/end/bottom/start, width?, height?) — inset-anchored children; two opposite insets stretch the child.
- **Code**: The native MeasureStack never stretches: with both Start and End set, End is discarded (`Start ??` short-circuits) and the child keeps the width it measured intrinsically; same for Top/Bottom (LayoutEngine.cs:591-593). The web realizer DOES honour it — it emits both left and right so CSS spans the box (WebRealizer.cs:250-253) — so the same tree has two geometries.
- **Evidence**:

  ```
  var x = positioned.Start ?? (positioned.End is { } end ? width - cw - end : alignX);
  var y = positioned.Top ?? (positioned.Bottom is { } bottom ? height - ch - bottom : alignY);
  ```

### A5 SafeArea · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Primitives/Nodes/VisualNode.cs`
- **Handoff**: minimum merges via max(inset, minimum) — gutters never collapse to zero on rectangular screens.
- **Code**: The slot is named Extra and is ADDED to the host inset instead of being a floor under it. src/eQuantic.UI.Native.Framework/Layout/LayoutEngine.cs:359 and src/eQuantic.UI.Web/WebRealizer.cs:524 both compute inset+extra, and the intent is pinned by a test named ExtraPaddingAddsToTheInset_NotInsteadOfIt (tests/eQuantic.UI.Native.Engine.Tests/SafeAreaTests.cs:50). On a rectangular screen the two rules agree (0+16 == max(0,16)), so the divergence only shows on a device that HAS an inset: with a 54dp notch and minimum 16 the handoff wants 54, the code produces 70.
- **Evidence**:

  ```
  VisualNode.cs:1773-1774  /// <summary>Added to whatever the host reports — a bar's own padding on top of the inset.</summary>
      public EdgeInsets Extra { get; init; }
  LayoutEngine.cs:359  var top = (safeArea.Edges.HasFlag(SafeEdges.Top) ? host.Top : 0) + safeArea.Extra.Top;
  WebRealizer.cs:524  return extra == 0 ? env : $"calc({env} + {TokenCss.Px(extra)})";
  ```

### A5 SafeArea · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Primitives/Nodes/VisualNode.cs`
- **Handoff**: Consumption: a SafeArea zeroes the inset for its subtree — nested SafeAreas never double-pad.
- **Code**: Nothing consumes the inset. Native: MeasureSafeArea reads ctx.SafeAreaInsets and passes the SAME ctx to the child, and LayoutContext.SafeAreaInsets is init-only (LayoutEngine.cs:105) so it cannot be zeroed for the subtree. Web/TS: every SafeArea emits its own env(safe-area-inset-*) padding, and env() is not scoped, so a nested pair pads twice. A screen SafeArea wrapping a bar that also uses one gets 2x the notch.
- **Evidence**:

  ```
  LayoutEngine.cs:358  var host = ctx.SafeAreaInsets;
  LayoutEngine.cs:105  public EdgeInsets SafeAreaInsets { get; init; }
  WebRealizer.cs:522  var env = $"env(safe-area-inset-{name}, 0px)";
  lowering.ts:2581  const env = `env(safe-area-inset-${name}, 0px)`;
  ```

### A5 SafeArea · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Native.Shell.iOS/PhotonViewController.cs`
- **Handoff**: Insets update per-frame from the shell: rotation, in-call status bar, foldable posture.
- **Code**: An inset-only change stores the new value but schedules no frame, so the tree keeps the old padding until something else dirties it. ViewDidLayoutSubviews writes _host.SafeAreaInsets and then returns early when the bounds are unchanged (which is exactly the in-call-banner case: safe area grows, bounds do not), and PhotonHost.SafeAreaInsets is a plain auto-property that never sets NeedsRender (PhotonHost.cs:128). The iOS clock only draws when NeedsRender/IsFrameDue (PhotonViewController.cs:229). Android has the same gate — ApplyInsets runs per frame but only AFTER the NeedsRender early-return (PhotonActivity.cs:263-266), under a comment that says insets can change without a resize.
- **Evidence**:

  ```
  PhotonViewController.cs:139-142
          _host.SafeAreaInsets = new EdgeInsets(
              (float)insets.Left, (float)insets.Top, (float)insets.Right, (float)insets.Bottom);
  
          if (bounds.Width == _lastWidth && bounds.Height == _lastHeight) return;
  PhotonHost.cs:128  public EdgeInsets SafeAreaInsets { get; set; }
  ```

### A5 SafeArea · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/BottomSheet.cs`
- **Handoff**: AppBar, BottomNavigation and BottomSheet handle their own insets (they paint under the inset and pad inside; §08).
- **Code**: BottomSheet pads its bottom with a flat Space.S6 (24dp) and never reads the host inset — no SafeArea, no SafeEdges anywhere in the file. On a phone with a 34dp home indicator the sheet's last row sits under it. Unlike AppBar and BottomNavigation, the class doc names no fence for this: its v1 fences are "enter/exit slide (state-transition system), drag-to-dismiss (gesture polish), detents".
- **Evidence**:

  ```
  BottomSheet.cs:62  Padding = new EdgeInsets(Space.S5, Space.S3, Space.S5, Space.S6),
  ```

### A6 ScrollView · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Native.Components/PhotonHost.cs`
- **Handoff**: Tap during deceleration stops dead, second tap interacts.
- **Code**: PressDown arms a pan candidate on the topmost scroll region but never stops an in-flight glide — ScrollStore keeps advancing its _targets, and nothing clears them on press (the only clear is inside ScrollTo, which the pointer path reaches only once the pan ARMS at 12dp of travel). Release with no active pan falls straight through to region.Node.OnPressed?.Invoke(). So the first tap on a decelerating list neither stops it nor is swallowed: the content keeps gliding AND the row under the finger activates.
- **Evidence**:

  ```
  PhotonHost.cs:1508-1519  _pan = null;
          if (_drag is null)
          {
              var scrollRegions = _lastFrame.ScrollRegions;
              … _pan = (region.Path, region.Axis, region.MaxOffset, along,
                      _scrolls.Get(region.Path) ?? region.Fallback, along, _lastTimeMs, 0, false);
  ```

### A8 Text · semantics · **unverified**

- **Component**: `src/eQuantic.UI.Native.Components/Semantics.cs`
- **Handoff**: Semantics — "StaticText — the run's text is its accessible value." / A11y — "Role: static text; label = full untruncated string (readers speak past the ellipsis)."
- **Code**: The native semantics walk keys on Text.Content and IGNORES Text.Spans, so a rich-run paragraph (Content empty, runs carrying the text) produces NO semantic node at all — the guard `when text.Content.Length > 0` fails and Walk falls through to a node with no children. Text.PlainContent exists for exactly this and its own doc says it is "what accessibility reads" (src/eQuantic.UI.Primitives/Nodes/VisualNode.cs:1351-1355), but nothing calls it here. The framework's own Markdown builds every inline paragraph this way (src/eQuantic.UI.Components/Markdown.cs:160), so on Photon every markdown paragraph with bold/code/link runs is silent to VoiceOver/TalkBack. The same Content-only rule at Semantics.cs:165 empties the derived name of a Pressable whose only text is a rich-run Text.
- **Evidence**:

  ```
  src/eQuantic.UI.Native.Components/Semantics.cs:139-141 —
              case Text text when text.Content.Length > 0:
                  nodes.Add(new(SemanticRole.StaticText, node.Path ?? "", node.Bounds,
                      text.Content, null, false));
  src/eQuantic.UI.Components/Markdown.cs:160 —
          return new Text("", style.Body, theme.TextSecondary, maxLines: 0) { Spans = spans };
  ```

### A8 Text · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Native.Shell.Apple/CoreTextService.cs`
- **Handoff**: "maxLines: 1 · Ellipsis" / "the ellipsis glyph replaces the last cluster that fits, so bidi and emoji boundaries are respected instead of cutting mid-cluster."
- **Code**: The Apple text service (macOS + iOS) truncates to maxLines but draws NO ellipsis glyph — the last shown line simply stops; Measure only reports the cut through MeasuredLine.Ellipsized. The class doc names it as a v1 fence and gives the reason: the CoreText path measures and rasterizes the same CTFrame, and the trailing ellipsis was deferred with the rest of the W4 shaping work. Android does append it (src/eQuantic.UI.Native.Shell.Android/AndroidTextService.cs:95 `text = text[..start].TrimEnd() + "…"`), and web gets it from text-overflow, so Apple is the odd target.
- **Evidence**:

  ```
  src/eQuantic.UI.Native.Shell.Apple/CoreTextService.cs:13 —
  /// device scale. v1 fences: no trailing ellipsis on truncation (measure reports the cut), weight
  src/eQuantic.UI.Native.Shell.Apple/CoreTextService.cs:187-192 —
              var shown = maxLines > 0 ? Math.Min(count, maxLines) : count;
                  lines.Add(new MeasuredLine(width, Ellipsized: i == shown - 1 && shown < count));
  ```

### A9 Heading · Label · missing-component · **unverified**

- **Component**: `MISSING`
- **Handoff**: new Heading("Portfolio", level: 1) — "Heading — semantic Text. Level 1 → Heading role, 2 → Title, 3 → Label-strong… Announced as 'heading, level N' — VoiceOver rotor / TalkBack heading navigation jump between them. Exactly one level-1 per screen (debug assert)." Semantics: "Web emits real heading levels; native marks the node as a header so readers can jump by heading."
- **Code**: No Heading component exists — there is no `class Heading` in the repo (only the TypeRole.Heading enum member, icon glyphs of that name, and stale `new Heading(...)` calls in dead template content that also references a non-existent `Container` and the vanished namespace eQuantic.UI.Web.Components). Nothing carries a heading LEVEL: Text has no level/semantics slot, the web realizer lowers every Text to a bare <span> with only a type class (no h1-h6, no role="heading", no aria-level), and the native semantics enum has no header role at all — so neither the VoiceOver rotor nor TalkBack heading navigation has anything to jump between, and there is no level-1 uniqueness assert.
- **Evidence**:

  ```
  src/eQuantic.UI.Web/WebRealizer.cs:1611-1613 —
          var element = new RealizedElement("span")
          {
              ClassName = $"eq-type-{text.Role.ToString().ToLowerInvariant()}",
  src/eQuantic.UI.Native.Components/Semantics.cs:11-26 (no header/heading member) —
      StaticText,
  ```

### A9 Heading · Label · missing-component · **unverified**

- **Component**: `MISSING`
- **Handoff**: new Label("Email", target: emailInput) — "Label role type (13/16/600), TextSecondary by default; 6dp gap above its control. target: binds it as the control's accessible name — the reader speaks 'Email, edit text' focusing the input, and the label itself is skipped. Tapping a Label forwards the press to its target (extends the hit area upward)."
- **Code**: No Label component exists (no `class Label` outside the compiler's LabeledStatementStrategy and a TS test fixture), so `target:` binding, reader-skipping and press forwarding are unimplemented; there is no way to caption a control that is not the control's own property. The three figures the block states DO hold where a label is built inline: TextInput draws its caption as TypeRole.Label — 13/16/SemiBold(600) at src/eQuantic.UI.Primitives/Theme/PhotonTheme.cs:119 — in theme.TextSecondary, inside a Column with gap 6. What is missing is the standalone component and its target contract; a caption sitting beside a control (a Switch, a RadioGroup) can neither name it nor be tapped to reach it.
- **Evidence**:

  ```
  src/eQuantic.UI.Components/TextInput.cs:127-128 (the only implementation of the caption contract, and it is private to TextInput) —
          var top = new Column(gap: 6) { Width = SizeValue.Fill };
          if (Label.Length > 0) top.Add(new Text(Label, TypeRole.Label, theme.TextSecondary));
  src/eQuantic.UI.Primitives/Theme/PhotonTheme.cs:119 —
          TypeRole.Label => new TypeStyle(13, 16, FontWeight.SemiBold, 0.1f, 1.3f),
  ```

### A12 Button · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/Button.cs`
- **Handoff**: Hit rect: Small "≥48 (slop)", Medium "≥48 (slop)" — "Sizes — toggle "Hit areas" in the top bar: Small 32 · hit 48 / Medium 40 · hit 48".
- **Code**: The Button never asks for the hit rect: Button.cs:59 destructures the size table and DISCARDS the Hit slot (`_, _`), and no min-size reaches the tree. Only the Photon realizer expands (PhotonRealizer.cs:1658-1665 ExpandHitRect, called at :835). The web path has no equivalent: `Touch.MinTarget` has zero references in src/eQuantic.UI.Web and src/eQuantic.UI.Runtime, and neither lowerPressable (lowering.ts:2038-2131) nor LowerPressable (WebRealizer.cs:1777-1808) nor the generated `.eq-pressable` rules (TokenCss.cs:317-332) set any minimum. On web a Small button's tap target is 32×32 and a Medium's is 40×40.
- **Evidence**:

  ```
  Button.cs:59  var (height, padX, gap, labelSize, iconSize, _, _) = ButtonStyles.Metrics(Size, context.Density);   //  vs PhotonRealizer.cs:1662  var minimum = density == Density.Compact ? 0 : Touch.MinTarget;
  ```

### A12 Button · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/Button.cs`
- **Handoff**: "Keys: Tab focuses (double ring §01) · Space or Enter activates on key-up · disabled stays in the walk, announced dimmed, activation refused."
- **Code**: Disabled (and Loading, via `inert`) sets Pressable.Disabled, which both realizers lower to the NATIVE html `disabled` attribute on a real <button>. A disabled button element is removed from the tab order and is not announced at all by most ATs, so it does not "stay in the walk" and is never "announced dimmed". The codebase has the alternative shape (Menu.cs:91-98 keeps a disabled row perceivable), but Button does not use it.
- **Evidence**:

  ```
  Button.cs:71  var inert = Disabled || Loading;  → Button.cs:132 `Disabled = inert` → WebRealizer.cs:1801  Disabled = pressable.Disabled && !wrapping ? true : null,  /  lowering.ts:2111  if (disabled && !wrapping) node.attributes['disabled'] = '';
  ```

### A13 IconButton · semantics · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/IconButton.cs`
- **Handoff**: "Toggle form: role toggle button, announces "selected"" · "Toggle form: aria-pressed — state stays out of the name."
- **Code**: IconButton builds its Pressable with only Disabled/Label/PressedBackground — `Selected` is never handed to the Pressable, and `Pressable.Selected` is the ONLY route to aria-pressed on both realizers (WebRealizer.cs:1804 `AriaPressed = pressable.Selected is { } selected ? …`; lowering.ts:2108-2109 `else if (pressable.selected !== undefined && pressable.selected !== null) node.attributes['aria-pressed'] = …`). A selected IconButton therefore announces exactly like an unselected one; the toggle state exists only as a tint and a glyph swap.
- **Evidence**:

  ```
  IconButton.cs:111-116  return new Pressable(box, Disabled ? null : OnPressed)\n        {\n            Disabled = Disabled,\n            Label = Label,\n            PressedBackground = Disabled ? null : pressedFill,\n        };
  ```

### A13 IconButton · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/IconButton.cs`
- **Handoff**: "32/icon16 · 40/20 · 48/24 · 56/24 — hit ≥ 48 always".
- **Code**: The visual sizes and icon steps are right (side = Sizing.Height → 32/40/48/56, iconSize → 16/20/24/24), but "hit ≥ 48 always" holds only on Photon. IconButton hands the Pressable a Box whose Width/Height are the side, and the web realizer applies no hit expansion anywhere (no use of Touch.MinTarget in src/eQuantic.UI.Web or the TS runtime; no min-width/min-height in TokenCss.cs:317-332). A Small (32) or Medium (40) IconButton — the AppBar/toolbar default the block recommends — is a sub-48 tap target on the web.
- **Evidence**:

  ```
  IconButton.cs:54  var side = Sizing.Height(Size, context.Density);  … IconButton.cs:101-103  Width = side,\n            Height = side,   (no minimum reaches the Pressable; cf. PhotonRealizer.cs:1662 which is the only place Touch.MinTarget is applied)
  ```

### B1 Card · missing-feature · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/Card.cs`
- **Handoff**: new Card(kind: CardKind.Elevated, header: CardHeader?, body: ..., footer: CardFooter?, onPressed: fn?) ... "Pressable card: whole surface is the target; pressed = scale 0.985 + fill shift to SurfaceSubtle, Fast 100ms; trailing IconButton stays independently pressable." / "Pressable Card: hover lifts elevation E1→E2 (Shadow channel, Motion.Press)" / "A pressable Card is one tab stop; Enter/Space activates." / "Pressable = button role named by its title"
- **Code**: Card has no onPressed parameter or property at all, and Build returns a bare Box — no Pressable, no press scale, no hover elevation change, no button role, no tab stop. Nothing in src, tests or samples ever constructs a pressable Card (Card.cs:23, Card.cs:49-58).
- **Evidence**:

  ```
  Card.cs:23  public Card(VisualNode child, CardKind kind = CardKind.Elevated)
  Card.cs:49  return new Box(new BoxStyle
  Card.cs:57      Elevation = Kind == CardKind.Elevated ? 1 : 0,
  Card.cs:58  }, Child);
  ```

### B2 List · ListItem · behaviour · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/ListItem.cs`
- **Handoff**: "Whole row = one hit target (≥ 52 > 48 ✓); an interactive trailing control splits: row activates on the row area, Switch on its own 48dp rect." (and B1's rule: "never nest two activation targets on one surface")
- **Code**: The Trailing node is added INSIDE the Row, and the whole Row (Trailing included) is then wrapped in the row's Pressable — so the trailing control sits inside the row's activation area rather than beside it. The web realizer degrades the outer element to span[role=button] (WebRealizer.cs:1785-1786) so the markup is legal, but the click listener is attached directly with no propagation stop (reconciler.ts:422-425), so a tap on a trailing Switch fires its OnChanged AND the row's OnPressed.
- **Evidence**:

  ```
  ListItem.cs:109  if (Trailing is { } trailing) row.Add(trailing);
  ListItem.cs:118-120  return OnPressed is null ? body : new Pressable(body, Disabled ? null : OnPressed)
  reconciler.ts:422-425  if (eventName === 'click') { (handler as () => void)(); return; }
  ```

### B2 List · ListItem · behaviour · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/ListItem.cs`
- **Handoff**: "Pointer — Interactive rows: hover = SurfaceSubtle wash (§10); cursor pointer."
- **Code**: The row's BoxStyle declares Width, MinHeight and a Selected background, but no Hover diff, so an interactive row has no hover wash. The mechanism exists and is used by every sibling row-like component (Menu.cs:88, Table.cs:54, DataTable.cs:199, Accordion.cs:76 all set `Hover = new StyleDiff { Background = theme.SurfaceSubtle }`); ListItem is the one that does not. Cursor pointer is satisfied (WebRealizer.cs:1795).
- **Evidence**:

  ```
  ListItem.cs:111-116  var body = new Box(new BoxStyle
  {
      Width = SizeValue.Fill,
      MinHeight = MinHeight,
      Background = Selected ? theme.Colors(Variant.Primary).Subtle : null,
  }, row);
  ```

### B3 AppBar · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/AppBar.cs`
- **Handoff**: Alignment: platform default (iOS center, Android leading) via titleAlign: Platform.
- **Code**: No titleAlign parameter or alignment property exists (no TabMode/TitleAlign type anywhere in src/). The title is always leading-aligned: it goes into a Flexible that eats all leftover space, so the render is `padding: 0 8px 0 12px` with a start-aligned span. The class doc names the fence at AppBar.cs:10-11 — "titleAlign Platform (iOS center) joins the platform services — v1 renders leading-aligned".
- **Evidence**:

  ```
  AppBar.cs:62  row.Add(new Flexible(titlePad));
  ```

### B3 AppBar · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/AppBar.cs`
- **Handoff**: Bar 56dp + safe-area top painted in the bar's fill (§08).
- **Code**: The root is a bare 56dp Box — no SafeArea node, no top inset, so nothing of the bar's fill extends under the status bar. Stated reason (AppBar.cs:10): "safe-area top painting joins the host insets".
- **Evidence**:

  ```
  AppBar.cs:72  Height = 56,
  ```

### B4 BottomNavigation · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/BottomNavigation.cs`
- **Handoff**: item hit = full column ≥ 48 wide · Equal-width columns, whole column = hit target.
- **Code**: The item Column asks for Height Fill but never Width Fill, so the Pressable's <button> gets no width:100% (WebRealizer.cs:1798 `Width = fills.Width ? "100%" : null`; the inline-block fence is stated at WebRealizer.cs:1357). Verified render: `<div style="flex: 1 1 0%"><button style="height: 100%; padding: 0; ...">` — the flex CELL is equal-width, but the button inside hugs its 56dp pill and sits at the start of the column, so the hit rect is 56dp wide, not the column, and the pill/label are left-aligned instead of centred. Height is lost the same way: the row's default Cross=Center leaves the flex item content-tall, so `height: 100%` resolves against auto (~42dp, not 56). The native engine stretches through Flexible and has a test for it (tests/eQuantic.UI.Native.Engine.Tests/StretchLayoutTests.cs:71 StretchReachesTHROUGHaFlexible), so this is the web target only.
- **Evidence**:

  ```
  BottomNavigation.cs:71-76  var column = new Column(gap: 2) { Height = SizeValue.Fill, Main = MainAlign.Center, Cross = CrossAlign.Center };
  ```

### B4 BottomNavigation · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/BottomNavigation.cs`
- **Handoff**: Bar: Surface + E2 (top shadow).
- **Code**: The root Box sets Background only; Elevation stays 0 and the rendered root has a background-color but no box-shadow. Stated reason (BottomNavigation.cs:17-18): "the E2 top shadow joins the engine shadow primitive".
- **Evidence**:

  ```
  BottomNavigation.cs:95-100  return new Box(new BoxStyle { Width = SizeValue.Fill, Height = 56, Background = theme.Surface, }, row);
  ```

### B5 Tabs · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/Tabs.cs`
- **Handoff**: row 48 · indicator 3dp rrect, inset 16 · Fixed mode: 2–4 tabs, equal width · Hit: full cell height 48.
- **Code**: The cell Column asks for Height Fill but not Width Fill, so the tab <button> gets height:100% and NO width (WebRealizer.cs:1798). Verified render: `<div style="flex: 1 1 0%; min-width: 0"><button ... role="tab">` — the flex cell is equal-width, but everything inside hugs the label, so the 3dp indicator (Width Fill, padding 0 16px) spans (label width − 32) start-aligned instead of (cell width − 32), and the label is not centred in its cell. This is exactly the bug the native engine already fixed and pinned (tests/eQuantic.UI.Native.Engine.Tests/StretchLayoutTests.cs:88 ATabLabelSitsOverItsOwnIndicator, whose comment says the old behaviour "put every tab label against the left edge of its tab"); the web target still has it.
- **Evidence**:

  ```
  Tabs.cs:45  var cell = new Column(gap: 0) { Height = SizeValue.Fill };
  ```

### B6 Avatar · missing-feature · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/Avatar.cs`
- **Handoff**: "Group stack: overlap −25%, 2dp Surface ring, max 4 + \"+n\" counter chip." (also drawn in the block: "AB TK +3 group · overlap −8 · 2dp ring")
- **Code**: Avatar renders exactly ONE face and has no group/stack surface at all; no AvatarGroup type exists in the repo (grep -rn "AvatarGroup" over src/ and tests/ returns nothing), and the only public factory is the single-face one at src/eQuantic.UI.Components/UI.cs:255-257.
- **Evidence**:

  ```
  Avatar.cs:19  public sealed class Avatar : StatelessComponent
  Avatar.cs:26  public Avatar(string initials, SizeVariant size = SizeVariant.Medium, string? name = null)
  UI.cs:255  public static Avatar Avatar(string initials, SizeVariant size = SizeVariant.Medium,
  UI.cs:257      new Avatar(initials, size, name);
  ```

### B8 Chip · semantics · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/Chip.cs`
- **Handoff**: "A11y: Filter = toggle button \"Income, filter, selected\"" / "Filter/toggle chip = button + aria-pressed"
- **Code**: The filter chip's Pressable never sets Selected, so no aria-pressed is emitted and the selection lives only in the fill colour (src/eQuantic.UI.Components/Chip.cs:90-96). Pressable.Selected exists precisely for this and lowers to aria-pressed (src/eQuantic.UI.Primitives/Nodes/VisualNode.cs:1098; src/eQuantic.UI.Web/WebRealizer.cs:1804 emits the attribute only when Selected is non-null).
- **Evidence**:

  ```
  Chip.cs:91  ? new Pressable(box, OnPressed)
  Chip.cs:92    {
  Chip.cs:93        Label = Label,
  Chip.cs:95        PressedBackground = Selected ? primary.Pressed.WithOpacity(0.24f) : theme.SurfaceSubtle,
  WebRealizer.cs:1804  AriaPressed = pressable.Selected is { } selected ? (selected ? "true" : "false") : null,
  ```

### B8 Chip · behaviour · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/Chip.cs`
- **Handoff**: "Keys — A chip is a tab stop; Space/Enter toggles or activates; Delete/Backspace removes a removable chip — the ✕ is not a separate stop."
- **Code**: Exactly inverted for Input chips. The chip body is wrapped in a Pressable only for Filter kind (Chip.cs:90), so a removable Input chip is NOT a tab stop; the ✕ IS one, because the remove glyph is its own Pressable (Chip.cs:73) and Pressable lowers to a real <button> (src/eQuantic.UI.Web/WebRealizer.cs:1786). There is no Delete/Backspace handling anywhere in the component — no Shortcut node, no key binding.
- **Evidence**:

  ```
  Chip.cs:73  content.Add(new Pressable(new Icon(Icons.Close, IconSize.Dense, textColor), OnRemove)
  Chip.cs:90  return Kind == ChipKind.Filter && OnPressed != null
  WebRealizer.cs:1786  var element = new RealizedElement(wrapping ? "span" : "button")
  ```

### B8 Chip · behaviour · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/Chip.cs`
- **Handoff**: "Hover: outlined/quiet chips = SurfaceSubtle; filled chips = fill→pressed midpoint (§10). The remove ✕ is its own hover target inside the chip."
- **Code**: The chip's BoxStyle sets no Hover diff at all (Chip.cs:79-87), so a pointer gets no hover feedback on any chip kind. The capability exists and is used by the sibling control: BoxStyle.Hover (src/eQuantic.UI.Primitives/Nodes/VisualNode.cs:222) is set by Button (src/eQuantic.UI.Components/Button.cs:126). The generated stylesheet has no chip/pressable hover rule either — only :active and :focus-visible (src/eQuantic.UI.Web/TokenCss.cs:317-332).
- **Evidence**:

  ```
  Chip.cs:79  var box = new Box(new BoxStyle
  Chip.cs:82      Background = fill,
  Button.cs:126  Hover = inert || hoverFill is null ? null : new StyleDiff { Background = hoverFill },
  ```

### B9 TextInput · missing-feature · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/TextInput.cs`
- **Handoff**: States: ... error = Destructive border + error glyph + Destructive helper
- **Code**: The error branch swaps only the border colour and the caption colour — no glyph is ever added to the row. TextInput.cs:81-84 and :132 are the entire error treatment; the row (TextInput.cs:95-114) holds the leading Icon and the Flexible entry and nothing else. This is NOT covered by the class doc's fence, which names only "the trailing slot (clear/eye/counter)". Icons.Error exists in the vocabulary (src/eQuantic.UI.Primitives/Nodes/IconGlyph.cs:37), so nothing blocks it.
- **Evidence**:

  ```
  var borderColor = hasError ? theme.Colors(Variant.Destructive).Base : _focused ? theme.Colors(Variant.Primary).Base : theme.BorderStrong;  // TextInput.cs:82-84 — the only use of hasError on the container
  ```

### B9 TextInput · behaviour · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/TextInput.cs`
- **Handoff**: Helper/error below: 12/500, 5dp gap — the line is always reserved so error swaps never shift layout.
- **Code**: The caption line is a plain Text whose content is "" when there is neither helper nor error (TextInput.cs:91, :136). On the web target that lowers to an EMPTY span (src/eQuantic.UI.Web/WebRealizer.cs:1614 `InnerHtml = text.Spans is null ? text.Content : null`) which, with maxLines:1 forcing `display:block` (src/eQuantic.UI.Runtime/src/shared/lowering.ts:1943), generates no line box and measures 0dp tall — so a field authored without a helper grows by the Caption line height (16dp) the first time an error is set. The native target does reserve it: the measurer floors at one line (src/eQuantic.UI.Native.Framework/Text/ITextMeasurer.cs:92 `if (lines.Count == 0) lines.Add(new MeasuredLine(0, false));`), so the two targets also disagree with each other on the same tree.
- **Evidence**:

  ```
  var caption = hasError ? Error! : Helper ?? "";  // TextInput.cs:91
  column.Add(new Text(caption, TypeRole.Caption, captionColor, maxLines: 1));  // TextInput.cs:136
  ```

### B10 SearchField · metric · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/SearchField.cs`
- **Handoff**: clear button appears when non-empty (glyph 20 in Full circle, hit 48)
- **Code**: The clear button is a bare Pressable around a 20dp Icon — no Radius.Full container Box behind the glyph and no hit-target expansion, so the pressable measures 20x20. The web lowering emits a <button> with `padding: '0'` (src/eQuantic.UI.Runtime/src/shared/lowering.ts:2053-2063) and lowerPressable applies no minimum, so the hit rect is the 20dp glyph, not Touch.MinTarget (48, src/eQuantic.UI.Primitives/Theme/Tokens.cs:184). Components that need the 48 do it explicitly (e.g. PageIndicator.cs:88, Slider.cs:98).
- **Evidence**:

  ```
  row.Add(new Pressable(
      new Icon(Icons.Close, IconSize.Dense, theme.TextMuted),
      () => OnChanged?.Invoke(""))
  {
      Label = SdkStrings.ClearSearch,
  });  // SearchField.cs:49-54
  ```

### B11 Checkbox · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/Checkbox.cs`
- **Handoff**: "the whole row is the target (hit ≥ 48 tall)"
- **Code**: The row is laid out with no height and no min-height, so it measures its tallest child — the 22dp box (the BodyM label's line box is 20) — giving a 22dp-tall target. The Photon realizer rescues this (PhotonRealizer.cs:1662 `var minimum = density == Density.Compact ? 0 : Touch.MinTarget;` expands the hit rect to 48), but the web realizer emits no minimum at all: WebRealizer.cs:1786-1800 sets only padding/border/background/font/cursor/text-align, and TokenCss.cs:317-332 (.eq-pressable rules) adds no sizing. Checkbox.cs:60. The component's own doc comment (Checkbox.cs:9) asserts "hit ≥ 48 via the Pressable contract", which holds on Photon and not on web.
- **Evidence**:

  ```
  Checkbox.cs:60 — `var row = new Row(gap: Space.S3) { Cross = CrossAlign.Center };`  /  WebRealizer.cs:1786 — `var element = new RealizedElement(wrapping ? "span" : "button")` (Style sets Padding/Border/Background/FontFamily/Cursor/TextAlign only)
  ```

### B12 Switch · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/Switch.cs`
- **Handoff**: "Hit rect 48, extends over the paired label row in ListItems."
- **Code**: The pressable's subtree is the 52×32 track, so the web target's hit rect is 32dp tall: WebRealizer.LowerPressable emits no min sizing (WebRealizer.cs:1786-1800) and TokenCss's .eq-pressable rules add none (TokenCss.cs:317-332). Photon does honour it (PhotonRealizer.cs:1662 expands to Touch.MinTarget = 48), so the contract holds on native and breaks on web. Switch.cs:46-52, :72-87.
- **Evidence**:

  ```
  Switch.cs:47-49 — `Width = Sizing.SwitchWidth(density),` / `Height = Sizing.SwitchHeight(density),` (52×32 Comfortable) with no MinHeight anywhere in Build; WebRealizer.cs:1789-1800 — Style sets `Padding`, `Border`, `Background`, `FontFamily`, `Cursor`, `TextAlign` only
  ```

### B14 ProgressBar · semantics · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/ProgressBar.cs`
- **Handoff**: role=progressbar + aria-valuenow/valuemin/valuemax; indeterminate omits valuenow.
- **Code**: Build (ProgressBar.cs:50-109) returns a bare Row/Box tree — no role, no value attributes on either branch. The vocabulary has no node that could carry them: the whole NodeKind set is box/row/column/text/pressable/adjustable/... and grepping the repo for "progressbar"/"aria-valuenow" outside eQuantic.UI.Core/HtmlElement.cs (the legacy raw-HTML layer, lines 502-504) returns nothing. A screen reader gets an unlabelled pair of divs.
- **Evidence**:

  ```
  ProgressBar.cs:62  var track = new Row(gap: 0)
  ProgressBar.cs:101 return new Box(new BoxStyle { Width = SizeValue.Fill, Height = height, ... Clip = true }, new LoopMotion(...));
  ```

### B18 Banner · semantics · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/Banner.cs`
- **Handoff**: role=status (polite) for info/success · role=alert for error severity. Content change re-announces. Warning/Destructive = assertive alert role; Info/Success = polite status.
- **Code**: Build returns an unannotated Box — the Status variant picks a glyph and a fill and nothing else. No role, no aria-live, and no node in the vocabulary can carry them (the only 'alert' in the write-once path is the alertdialog on Overlay, WebRealizer.cs:985). A Banner that appears or changes announces nothing, which is the whole point of the component.
- **Evidence**:

  ```
  Banner.cs:75-81  return new Box(new BoxStyle { Width = SizeValue.Fill, Padding = new EdgeInsets(14, 12, 14, 12), Background = tint.Subtle, CornerRadius = new CornerRadii(context.Theme.Shape(ShapeScale.Large)), }, content);
  ```

### C1 BottomSheet · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/BottomSheet.cs`
- **Handoff**: Detents: Peek (content height, ≤ 40%) · Half 50% · Full (top inset + 8). Drag follows 1:1; release snaps to nearest detent by position + velocity — and the ctor is `new BottomSheet(detents: [Detent.Peek, Detent.Half, Detent.Full], initial: Detent.Half, dismissible: true, child: ...)`
- **Code**: There is no detent system and no `Detent` type anywhere in the repo (grep -rni detent finds only fence comments). The ctor at BottomSheet.cs:17 takes only (content, onDismiss, dismissible) — no `detents`, no `initial` — so the sheet is always one fixed height hugging its content. The component's own doc comment names the fence: "v1 fences: enter/exit slide (state-transition system), drag-to-dismiss (gesture polish), detents." (BottomSheet.cs:12-13). Consequences that also fail: "Scrim from Half up", "inner ScrollView scrolls only at Full", and "detent changes announce (half expanded, full)".
- **Evidence**:

  ```
  BottomSheet.cs:17  public BottomSheet(VisualNode content, Action? onDismiss = null, bool dismissible = true)
  BottomSheet.cs:12-13  /// drag-to-dismiss (gesture polish), detents.
  ```

### C3 ActionSheet · missing-component · **unverified**

- **Component**: `MISSING`
- **Handoff**: new ActionSheet(title: "Statement PDF"?, actions: [SheetAction ×≤6], cancelLabel: "Cancel") — NOT YET IN SDK · REQUEST ... the interim in code is a BottomSheet (C1) carrying the action list
- **Code**: Confirmed absent. There is no ActionSheet.cs in src/eQuantic.UI.Components/ and no ActionSheet or SheetAction type anywhere in src/ — the name appears only inside error strings that point AT it (AppBar.cs:36 throws "overflow belongs in an ActionSheet"; Dialog.cs:28 throws "a third means an ActionSheet or a screen"). So both referring components fence at a target that does not exist. The stated interim is also weaker than the block assumes: the block wants "a BottomSheet preset at Peek", and BottomSheet has no detents (see C1), so the interim can only be a full-height sheet. Everything else in the block is consequently unimplemented: the 52dp rows, the Radius.Xl 16 group container (note: the block's own §04 ladder puts Xl at 20, and this row says 16), the ≤6 action cap, the separate Full-radius Cancel surface 8dp below at weight 700, destructive-last ordering, the 100ms SurfaceSubtle row flash, role=menu/menuitem with "Share, 1 of 3" positional announcements, ↑/↓/Enter/Esc/Home/End, and the anchored-context-menu presentation on pointer targets.
- **Evidence**:

  ```
  src/eQuantic.UI.Components/AppBar.cs:36            ? throw new ArgumentException("AppBar takes at most 3 actions (spec B3) — overflow belongs in an ActionSheet.", nameof(Actions))
  src/eQuantic.UI.Components/Dialog.cs:28                "A Dialog carries 1-2 actions — a third means an ActionSheet or a screen (spec C2).");
  ```

### C4 Toast · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/Toast.cs`
- **Handoff**: Radius.Lg 14
- **Code**: The pill is fully rounded: theme.Shape(ShapeScale.Full) resolves to Radius.Full = 999 (PhotonTheme.cs:149 → Tokens.cs:38), engine-clamped to min(w,h)/2 — a stadium, not a 14dp rounded rectangle. The doc comment states "Radius.Full" (Toast.cs:7) but never names it as a departure from the block or gives a reason, so it reads as drift rather than a decision.
- **Evidence**:

  ```
  Toast.cs:57            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
  ```

### C4 Toast · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/Toast.cs`
- **Handoff**: one action max, 14/700 in InverseAction (opposite-theme Primary — audited both modes)
- **Code**: The action label is TypeRole.Label = 13dp / SemiBold(600) (PhotonTheme.cs:119) in theme.TextInverse — the SAME colour as the message text, because no InverseAction token exists on IAppTheme (the only inverse token is TextInverse, IAppTheme.cs:71). So the action is not tinted at all: size, weight and colour all miss, and the only thing separating the action from the body copy is the weight difference between BodyM and Label.
- **Evidence**:

  ```
  Toast.cs:48            }, new Text(label, TypeRole.Label, theme.TextInverse)), OnAction)
  ```

### C4 Toast · semantics · **unverified**

- **Component**: `src/eQuantic.UI.Components/Toast.cs`
- **Handoff**: A11y: polite live announcement ... Semantics: Polite live region (role=status)
- **Code**: The toast is never announced. It lowers to `new Overlay(anchor) { Modal = false }` (Toast.cs:74), and BOTH realizers gate every semantic attribute on the layer being modal: WebRealizer.cs:983 `if (overlay.Modal && overlay.Open)` and the TS twin lowering.ts:775 `const modalAndOpen = node.modal !== false && ...`. A non-modal layer therefore emits a bare `<div class="eq-overlay eq-overlay-passthrough">` — no role=status, no aria-live. Overlay has no live-region property at all, so native cannot announce it either.
- **Evidence**:

  ```
  Toast.cs:74        return new Overlay(anchor) { Modal = false };
  WebRealizer.cs:983        if (overlay.Modal && overlay.Open)
  ```

### C4 Toast · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/Toast.cs`
- **Handoff**: Toast.Show("Card removed", action: ("Undo", fn)?, duration: Duration.Default) ... Timing: 4s default, 6s with action ... Hover pauses the auto-dismiss timer ... the timer pauses while a reader focuses the toast
- **Code**: No Show(), no duration parameter and no timer: the only entry point is the instance ctor Toast(message, status, actionLabel, onAction) (Toast.cs:16) and the UI factory UI.cs:369 mirrors it. The doc comment names the deviation and its reason: "the auto-dismiss TIMER is the app's (or the host clock's) concern, not the component's" (Toast.cs:9-10). Consequence worth reviewing: every timer-dependent rule in the block — 4s/6s, hover-pause, reader-focus-pause — has nothing to attach to, and the pause behaviours cannot be implemented by an app that only controls presence.
- **Evidence**:

  ```
  Toast.cs:16    public Toast(string message, Variant status = Variant.Info,
  Toast.cs:17        string? actionLabel = null, Action? onAction = null)
  ```

### C5 Drawer · missing-component · **unverified**

- **Component**: `src/eQuantic.UI.Components/Drawer.cs`
- **Handoff**: new Drawer(header: Widget?, items: [DrawerItem], footer: Widget?, edge: Edge.Start) — "Item: 40dp pill row (icon Dense 20 + label 13.5/500, gap 10, padding X 12) — selected = Primary-subtle pill + filled glyph + 700 weight, same vocabulary as B4." · "Selected item aria-current="page"" · "Item hover = SurfaceSubtle pill" · "↑/↓ move items, Enter navigates"
- **Code**: There is no DrawerItem type anywhere in the repo (grep for DrawerItem across all .cs/.ts returns zero hits). The Drawer takes one opaque VisualNode and draws no rows at all, so none of the item metrics, the selected pill, aria-current="page", the hover pill or the ↑/↓/Enter item keyboard model exist.
- **Evidence**:

  ```
  Drawer.cs:25  public Drawer(VisualNode content, bool open, Action? onDismiss = null)
  Drawer.cs:32  public VisualNode Content { get; init; }
  ```

### C5 Drawer · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/Drawer.cs`
- **Handoff**: "header slot + items + footer slot"
- **Code**: The Drawer has exactly one content slot. No Header, Items or Footer property exists, so the caller must hand-assemble the three regions and the component enforces nothing about their order or styling.
- **Evidence**:

  ```
  Drawer.cs:32-35  public VisualNode Content { get; init; }
      public bool Open { get; init; }
      public Action? OnDismiss { get; init; }
      public DrawerEdge Edge { get; init; } = DrawerEdge.Start;
  ```

### C5 Drawer · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/Drawer.cs`
- **Handoff**: "Panel: min(320, 85% width)"
- **Code**: Width is a flat 320dp with no viewport clamp. On a 360dp-wide phone the panel takes 89% of the screen and on a 320dp device it covers the viewport entirely, leaving no scrim strip to tap for dismissal.
- **Evidence**:

  ```
  Drawer.cs:38  public float Width { get; init; } = 320;
  Drawer.cs:57  Width = Width,
  ```

### C5 Drawer · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/Drawer.cs`
- **Handoff**: "Panel: min(320, 85% width) · Surface · E4"
- **Code**: The panel paints elevation 3, not 4. These are distinct rungs of the §05 ladder — E3 is ShadowSpec(6, 16, -2, 0.16α) and E4 is ShadowSpec(12, 28, -4, 0.20α) — so the drawer sits visibly flatter than specified and lower than the BottomSheet (E4), which is the surface it is meant to match.
- **Evidence**:

  ```
  Drawer.cs:62  Elevation = 3,
  PhotonTheme.cs:135-136  3 => new ShadowSpec(6, 16, -2, Shadow(0.16f, 0.52f)),
          4 => new ShadowSpec(12, 28, -4, Shadow(0.20f, 0.56f)),
  ```

### C5 Drawer · semantics · **unverified**

- **Component**: `src/eQuantic.UI.Components/Drawer.cs`
- **Handoff**: "A11y: navigation-dialog semantics; … announce "navigation drawer, open"."
- **Code**: The Overlay is constructed with no Label, so no aria-label is emitted and the layer announces as a bare "dialog". The Overlay's own doc names this exact consequence: "a modal layer without one is announced as just 'dialog', which tells the user a wall appeared but not which."
- **Evidence**:

  ```
  Drawer.cs:76  var overlay = new Overlay(layer);
  lowering.ts:781  if (node.label) layer.attributes['aria-label'] = node.label;
  ```

### C5 Drawer · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/Drawer.cs`
- **Handoff**: "Gestures: 20dp edge-swipe capture opens, follows finger 1:1; scrim fades proportionally; release settles open/closed by position + velocity"
- **Code**: No edge-swipe capture region, no drag tracking and no proportional scrim exist. The scrim is a static full-bleed Pressable at a fixed fill, and Open is a pure boolean the caller flips — the panel can only snap. The component's fence list names "drag-to-close (the DragDismiss horizontal axis)" but never the 20dp edge-swipe-to-OPEN, so that half is undocumented as well as unbuilt.
- **Evidence**:

  ```
  Drawer.cs:48-53  layer.Add(new Pressable(new Box(new BoxStyle
          {
              Width = SizeValue.Fill,
              Height = SizeValue.Fill,
              Background = theme.Scrim,
          }), OnDismiss) { Label = SdkStrings.Dismiss });
  ```

### C6 SegmentedControl · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/SegmentedControl.cs`
- **Handoff**: heights 36 (Medium ★) / 44 (Large)
- **Code**: SegmentedControl.cs:39 takes its height from the control ladder, which resolves to 40 (Medium) / 48 (Large) at the default Comfortable density and 32 / 40 at Compact (Tokens.cs:81-87) — no density produces 36 / 44.
- **Evidence**:

  ```
  SegmentedControl.cs:39  var height = Sizing.Height(Size, context.Density);
  Tokens.cs:84  SizeVariant.Medium => density == Density.Compact ? 32 : 40,
  Tokens.cs:85  SizeVariant.Large => density == Density.Compact ? 40 : 48,
  ```

### C6 SegmentedControl · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/SegmentedControl.cs`
- **Handoff**: label 13/600 — active TextPrimary, inactive TextSecondary
- **Code**: the theme's Label role IS 13/600 (PhotonTheme.cs:119), but SegmentedControl.cs:68 overrides the size with Sizing.LabelSize, which is 15 at Medium/Comfortable and 16 at Large/Comfortable (Tokens.cs:151-157); only Compact/Medium lands on 13. Weight stays SemiBold (600), and the TextPrimary/TextSecondary pairing at line 66 is correct.
- **Evidence**:

  ```
  SegmentedControl.cs:68  StyleOverride = theme.Type(TypeRole.Label).WithSize(Sizing.LabelSize(Size, context.Density)),
  Tokens.cs:154  SizeVariant.Medium => density == Density.Compact ? 13 : 15,
  ```

### C6 SegmentedControl · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/SegmentedControl.cs`
- **Handoff**: Thumb: Surface + E1, radius = track − inset (7). Slides Base 200ms standard
- **Code**: there is no travelling thumb — every segment owns a Box whose Background/Elevation flip between null/0 and Surface/1 (SegmentedControl.cs:77-79), so selection CROSSFADES in place instead of sliding. The transition also runs Motion.Press (FastMs = 100ms, Tokens.cs:239/255) rather than Base 200ms standard, which is Motion.State. Surface + E1 and radius 7 themselves are correct.
- **Evidence**:

  ```
  SegmentedControl.cs:77  Background = selected ? theme.Surface : null,
  SegmentedControl.cs:79  Elevation = selected ? 1 : 0,
  SegmentedControl.cs:80  Transition = TransitionSpec.Of(StyleChannels.Colors | StyleChannels.Shadow, Motion.Press),
  ```

### C7 Slider · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/Slider.cs`
- **Handoff**: Thumb 24dp white + E2 + 1dp Border
- **Code**: Slider.cs:22 fixes the thumb at 20dp — a hardcoded const, not a token lookup, so no size or density resolves it to 24.
- **Evidence**:

  ```
  Slider.cs:22  private const float ThumbSize = 20;
  ```

### C7 Slider · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/Slider.cs`
- **Handoff**: Track 4dp Radius.Full: active Primary, rest SurfaceSubtle (two rrects).
- **Code**: the REST half is painted theme.BorderStrong (Slider.cs:73), not SurfaceSubtle — a much darker rail than specified. Track height 4 (line 21) and the active Primary half (lines 51-52, 70) are correct.
- **Evidence**:

  ```
  Slider.cs:73  row.Add(new Flexible(TrackHalf(theme.BorderStrong, filled: false, enabled: !Disabled,
  Slider.cs:74      onPressed: () => OnChanged?.Invoke(Math.Min(Max, Value + step))), Weight(1 - fraction)));
  ```

### C7 Slider · semantics · **unverified**

- **Component**: `src/eQuantic.UI.Components/Slider.cs`
- **Handoff**: role=slider + aria-valuenow/min/max (+ valuetext for units) ... announces "Limit, R$ 400"; live value announced with 200ms debounce while dragging.
- **Code**: the Adjustable node carries only Child, OnAdjust, Label and Role (VisualNode.cs:1006-1029) — it has no value fields, so neither realizer can emit aria-valuenow/valuemin/valuemax/valuetext. Both emit role="slider" + tabindex="0" + aria-label and nothing else, which is invalid ARIA (role=slider requires aria-valuenow) and means the value is never announced at all, debounced or otherwise. Slider.cs:106-110 passes only Label.
- **Evidence**:

  ```
  lowering.ts:2377-2380  const role = node.role ?? 'slider'; host.attributes['role'] = role; host.attributes['tabindex'] = '0'; if (node.label) host.attributes['aria-label'] = node.label;
  WebRealizer.cs:1099-1108  ["role"] = ... "slider", ["tabindex"] = "0" ... element.RawAttributes["aria-label"] = label;
  Slider.cs:106-110  new Adjustable(box, direction => OnChanged?.Invoke(Quantize(Value + direction * step, step))) { Label = Label, };
  ```

### C7 Slider · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/Slider.cs`
- **Handoff**: Drag: no slop on the thumb (immediate capture) ... thumb drag has no slop.
- **Code**: the web Draggable controller arms only after 12dp of travel, and Slider.cs:80 wraps the whole row (thumb included) in that node, so the first 12dp of every drag is swallowed before the value moves.
- **Evidence**:

  ```
  src/eQuantic.UI.Runtime/src/dom/draggable.ts:13  const SLOP = 12; // Touch.PressCancelSlop — cross-pinned with the C# host
  draggable.ts:61  if (!active && Math.abs(raw) > SLOP) {
  Slider.cs:80  VisualNode surface = Disabled ? row : new Draggable(row)
  ```

### C7 Slider · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/Slider.cs`
- **Handoff**: Steps: 4dp dots — OnPrimary over active track, BorderStrong over rest
- **Code**: a stepped slider draws no detent dots: TrackHalf (Slider.cs:128-149) builds exactly one Box (the bar) inside one centring Column inside one press target, with no per-detent children, and Step is only ever read as an arithmetic quantum (lines 50 and 116).
- **Evidence**:

  ```
  Slider.cs:130-140  var bar = new Box(new BoxStyle { Width = SizeValue.Fill, Height = TrackHeight, Background = color, CornerRadius = ..., Transition = ... });
  Slider.cs:142-143  var centered = new Column(gap: 0) { Height = SizeValue.Fill, Main = MainAlign.Center }; centered.Add(bar);
  ```

### C7 Slider · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/Slider.cs`
- **Handoff**: bubble = inverse surface + Radius.Sm, Caption tnum, 10dp above
- **Code**: there is no value bubble — the entire tree is Box → Draggable → Row(TrackHalf, thumb, TrackHalf) (Slider.cs:68-100), with no overlay, no Anchored node, and no Text node anywhere in the component.
- **Evidence**:

  ```
  Slider.cs:91-100  var box = new Box(new BoxStyle { Width = SizeValue.Fill, MinWidth = 120, Height = Touch.MinTarget, Opacity = Disabled ? theme.DisabledOpacity : 1f, }, surface);
  ```

### C8 Stepper · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/Stepper.cs`
- **Handoff**: Joined form: 3 cells 40×32 in an Outline container ... joined · 120×32
- **Code**: the cell is square off the control ladder: Sizing.Height(Medium) = 40 at the default Comfortable density (Tokens.cs:84) and the arm's Width is set to that same height (Stepper.cs:92), so the control measures 120×40, not 120×32. Under Compact the height is 32 but the arms narrow to 32 too, giving 96×32 — no density yields the specified 120×32. Radius.Md, the 1dp outline, min-width 40 and value 15/600 tnum all match.
- **Evidence**:

  ```
  Stepper.cs:39  var height = Sizing.Height(Size, context.Density);
  Stepper.cs:92  var box = new Box(new BoxStyle { Width = height, Height = SizeValue.Fill }, centered);
  Stepper.cs:62  row.Add(new Box(new BoxStyle { MinWidth = height, Height = SizeValue.Fill }, reading));
  ```

### C8 Stepper · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/Stepper.cs`
- **Handoff**: hairline separators — three Boxes, not per-side borders
- **Code**: there are no separators at all between the three cells — the row adds arm, value, arm with gap 0 and nothing between them (Stepper.cs:43-65), and the only border in the component is the 1dp outline on the frame (line 73).
- **Evidence**:

  ```
  Stepper.cs:43  var row = new Row(gap: 0) { Height = SizeValue.Fill, Cross = CrossAlign.Center };
  Stepper.cs:44  row.Add(Arm(theme, Icons.Minus, height, canDecrement, ...));
  Stepper.cs:62  row.Add(new Box(new BoxStyle { MinWidth = height, Height = SizeValue.Fill }, reading));
  Stepper.cs:64  row.Add(Arm(theme, Icons.Plus, height, canIncrement, ...));
  ```

### C8 Stepper · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/Stepper.cs`
- **Handoff**: Long-press auto-repeat: starts at 500ms, 8 steps/s, ×4 after 2s; haptic per tick.
- **Code**: the arm is a plain Pressable whose only callback is a single OnPressed (Stepper.cs:94) — no timer, no hold state, no repeat, and no haptic API in the framework. Holding either arm changes the value exactly once.
- **Evidence**:

  ```
  Stepper.cs:94  return new Pressable(box, enabled ? onPressed : null) { Disabled = !enabled, Label = label };
  Stepper.cs:44-45  row.Add(Arm(theme, Icons.Minus, height, canDecrement, () => OnChanged?.Invoke(Value - Step), $"Decrease {Label}"));
  ```

### C8 Stepper · semantics · **unverified**

- **Component**: `src/eQuantic.UI.Components/Stepper.cs`
- **Handoff**: A11y: adjustable role — "Quantity, 2"; increment/decrement actions; each change announced. ... Group named by its label
- **Code**: Build returns a bare Box (Stepper.cs:67-76) — there is no Adjustable wrapper, unlike SegmentedControl.cs:109 and Slider.cs:106 — so the control has no adjustable role, no group, and no accessible name from Label: the Label property is only ever interpolated into the two button names (lines 45 and 65). Nothing announces "Quantity, 2"; the value renders as loose text. The increment/decrement actions themselves are present and correctly named.
- **Evidence**:

  ```
  Stepper.cs:67-76  return new Box(new BoxStyle { Width = SizeValue.Hug, Height = height, Background = ..., CornerRadius = ..., BorderWidth = 1, BorderColor = theme.BorderStrong, Opacity = ... }, row);
  Stepper.cs:45  () => OnChanged?.Invoke(Value - Step), $"Decrease {Label}"));
  ```

### C8 Stepper · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/Stepper.cs`
- **Handoff**: The value group is one stop: ↑/→ increment, ↓/← decrement, Home/End clamp; the −/+ buttons are also plain stops.
- **Code**: the −/+ buttons are plain stops as specified, but the value group is not a stop at all and no arrow key does anything: with no Adjustable in the tree (Stepper.cs:67) there is no keydown handler, and the value cell is a plain Box, not a Pressable (line 62), so it is not focusable. Home/End are unimplemented framework-wide in any case (lowering.ts:2387-2392, PhotonHost.cs:1919).
- **Evidence**:

  ```
  Stepper.cs:62  row.Add(new Box(new BoxStyle { MinWidth = height, Height = SizeValue.Fill }, reading));   // not focusable
  Stepper.cs:67  return new Box(new BoxStyle   // no Adjustable wrapper
  ```

### C9 PullToRefresh · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/PullToRefresh.cs`
- **Handoff**: commit threshold 72dp ... Release ≥ 72dp: settles at 72dp
- **Code**: The commit threshold and the settled height are both 64dp (PullToRefresh.cs:19), and the Draggable's Max is clamped to that same 64 (PullToRefresh.cs:59), so the content can never travel the spec'd 72dp at all.
- **Evidence**:

  ```
  public const float Threshold = 64;
  ```

### C9 PullToRefresh · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/PullToRefresh.cs`
- **Handoff**: Indicator: Spinner Md 24 (Primary) in a 40dp Surface circle + E2.
- **Code**: The spinner is dropped straight into a bare full-width Row with no background, no 40dp circle and no elevation (PullToRefresh.cs:36-43) — the BoxStyle that would carry Background/CornerRadius/Elevation is never created for the indicator.
- **Evidence**:

  ```
  var indicator = new Row(gap: 0)
  {
      Width = SizeValue.Fill,
      Height = Threshold,
      Main = MainAlign.Center,
      Cross = CrossAlign.Center,
  ```

### C9 PullToRefresh · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/PullToRefresh.cs`
- **Handoff**: A11y: refresh exposed as a custom action on the scroll region (readers can't pull); announces "refreshing" → "refreshed"
- **Code**: The whole build is Box > Stack > (Positioned indicator + Draggable content) — no Pressable, no Label, no live-region or status node anywhere (PullToRefresh.cs:36-64). A reader gets no refresh action and no announcement; the gesture is the only path.
- **Evidence**:

  ```
  var stack = new Stack();
  stack.Add(new Positioned(indicator, top: 0, start: 0, end: 0));
  stack.Add(new Draggable(content, OnReleased)
  ```

### C9 PullToRefresh · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/PullToRefresh.cs`
- **Handoff**: Desktop surfaces an explicit refresh control (toolbar IconButton or menu item) + ⌘R/F5. The refresh action must be reachable as a real control — the gesture is never the only path (§10).
- **Code**: The component exposes only the drag gesture; nothing in Build emits a control, and there is no shortcut registration (PullToRefresh.cs:32-65). On a pointer target there is no way to refresh at all — the web Draggable controller arms on pointermove past a 12dp slop and never on wheel (src/eQuantic.UI.Runtime/src/dom/draggable.ts:61).
- **Evidence**:

  ```
  return new Box(new BoxStyle { Width = SizeValue.Fill, Clip = true }, stack);
  ```

### C10 SwipeableRow · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/SwipeableRow.cs`
- **Handoff**: actions revealed behind: 72dp panes
- **Code**: The revealed pane is 96dp wide (SwipeableRow.cs:23), and that constant also drives the pane Box width (line 61), the drag limit (line 81) and the rest offset (line 83) — every measurement of the reveal is 96 rather than 72.
- **Evidence**:

  ```
  public const float ActionWidth = 96;
  ```

### C10 SwipeableRow · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/SwipeableRow.cs`
- **Handoff**: new SwipeableRow(child: ListItem, trailing: [RowAction ×≤2], leading: [RowAction ×≤2]?) ... ≤ 2 actions per side; destructive always at the outer edge.
- **Code**: The component takes exactly ONE action (label + icon + callback) and reveals it only on the trailing edge (SwipeableRow.cs:25-32, 77); there is no RowAction type in the repo (grep for `RowAction` across src returns nothing) and no leading side. The doc comment names and justifies this: "A list row that reveals ONE action when swiped towards the end (spec B21). One, deliberately: a hidden action is already hard to find, and a drawer of three is a menu nobody can aim at — if there are several, the row belongs in a long-press menu instead."
- **Evidence**:

  ```
  public SwipeableRow(VisualNode child, string actionLabel, Icons actionIcon,
      Action? onAction = null)
  ```

### C10 SwipeableRow · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/SwipeableRow.cs`
- **Handoff**: Full swipe > 60% width commits the outer action — pane stretches to full width first (Fast).
- **Code**: Travel is hard-clamped at one action width, so a swipe past 60% of the row is impossible: the Draggable's Min is -96 (SwipeableRow.cs:81) and the web controller clamps to it (src/eQuantic.UI.Runtime/src/dom/draggable.ts:56). Release can only ever mean open-or-closed (line 91) — there is no commit branch and no pane stretch.
- **Evidence**:

  ```
  Axis = DragAxis.Horizontal,
  Min = -ActionWidth,
  Max = 0,
  ```

### C11 Accordion · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/Accordion.cs`
- **Handoff**: Header 52dp
- **Code**: The header box is Sizing.Height(SizeVariant.Large), which resolves to 48 under Comfortable density and 40 under Compact (src/eQuantic.UI.Primitives/Theme/Tokens.cs:85) — never 52. The component's own doc comment states 48dp as the intent (Accordion.cs:13) without reference to the handoff figure.
- **Evidence**:

  ```
  Height = Sizing.Height(SizeVariant.Large),
  ```

### C11 Accordion · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/Accordion.cs`
- **Handoff**: title 15/600
- **Code**: The header title uses TypeRole.Label, which the theme resolves to 13dp / 16 line / SemiBold (src/eQuantic.UI.Primitives/Theme/PhotonTheme.cs:119). The weight (600) matches; the size is 13 instead of 15. The 15/600-shaped rung is TitleSmall (15/20) at Bold, and BodyM is 15 at Regular — neither is used.
- **Evidence**:

  ```
  header.Add(new Text(item.Title, TypeRole.Label));
  ```

### C11 Accordion · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/Accordion.cs`
- **Handoff**: content padding 0/16/16
- **Code**: EdgeInsets is (Start, Top, End, Bottom) (src/eQuantic.UI.Primitives/Layout/LayoutTypes.cs:7), so the content pads 12 / 0 / 12 / 12 with Space.S3 = 12 (Tokens.cs:12). The top of 0 matches; the sides and bottom are 12 where the handoff says 16 (Space.S4).
- **Evidence**:

  ```
  Padding = new EdgeInsets(Space.S3, 0, Space.S3, Space.S3),
  ```

### C11 Accordion · semantics · **unverified**

- **Component**: `src/eQuantic.UI.Components/Accordion.cs`
- **Handoff**: Header = button + aria-expanded + aria-controls → its region.
- **Code**: aria-expanded is emitted (Pressable.Expanded lowers to it — src/eQuantic.UI.Runtime/src/shared/lowering.ts:2072, src/eQuantic.UI.Web/WebRealizer.cs:1806), but aria-controls is not: Pressable has no Controls property (VisualNode.cs:1067-1135), the only aria-controls in the runtime belongs to Anchored panels (lowering.ts:2230), and the content Box (Accordion.cs:85-89) carries no id and no region role for a header to point at.
- **Evidence**:

  ```
  // The chevron is paint; this is the answer a screen reader gets.
  Expanded = open,
  ```

### C11 Accordion · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/Accordion.cs`
- **Handoff**: Headers are tab stops · Enter/Space toggles · ↑/↓ move between headers · Home/End first/last.
- **Code**: Each header is a plain Pressable, which lowers to a native <button> — tab stop and Enter/Space come for free — but nothing wires the arrow keys or Home/End. The headers are not wrapped in an Adjustable and there is no key handler anywhere in the component (Accordion.cs:55-96); a grep for `accordion` across src/ finds no keyboard controller in the runtime either.
- **Evidence**:

  ```
  column.Add(new Pressable(new Box(new BoxStyle
  {
      Height = Sizing.Height(SizeVariant.Large),
  ```

### C11 Accordion · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/Accordion.cs`
- **Handoff**: new Accordion(items: [(header, content)], singleOpen: false)
- **Code**: The default is inverted: Multiple defaults to false (Accordion.cs:31) and IsOpen then routes through the single-open field (line 38), so out of the box opening one section closes the previous one — the handoff's default (singleOpen: false) is independent sections. The doc comment states "single-open by default" (line 16) but does not reconcile it with the handoff signature.
- **Evidence**:

  ```
  public bool Multiple { get; init; }
  ```

### C12 PageIndicator · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/PageIndicator.cs`
- **Handoff**: Dot 8dp Radius.Full BorderStrong; active = 20×8 Primary pill
- **Code**: Every dot is 6dp tall and inactive dots are 6×6, not 8×8 (PageIndicator.cs:58-59). Radius.Full and BorderStrong do match (lines 60-61: theme.Shape(ShapeScale.Full) resolves to Radius.Full = 999 — src/eQuantic.UI.Primitives/Theme/PhotonTheme.cs:149).
- **Evidence**:

  ```
  Width = current ? 18 : 6,
  Height = 6,
  ```

### C12 PageIndicator · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/PageIndicator.cs`
- **Handoff**: active = 20×8 Primary pill
- **Code**: The active pill measures 18×6 (PageIndicator.cs:58-59) instead of 20×8. The Primary tint is correct (line 37).
- **Evidence**:

  ```
  Width = current ? 18 : 6,
  ```

### C12 PageIndicator · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/PageIndicator.cs`
- **Handoff**: gap S2 8
- **Code**: The dot row is built with Space.S1 = 4 (src/eQuantic.UI.Primitives/Theme/Tokens.cs:10), half the spec'd S2 = 8 (Tokens.cs:11). The tap-target wrapper also pads with Space.S1 (line 88), so the visual gap between two tappable dots is 4+4+4 rather than 8.
- **Evidence**:

  ```
  var row = new Row(gap: Space.S1) { Cross = CrossAlign.Center };
  ```

### C13 Tooltip · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/Tooltip.cs`
- **Handoff**: "provided by IconButton(label:)" · "Trigger: long-press 500ms on icon-only controls (IconButton provides it automatically from label:)."
- **Code**: IconButton never builds a Tooltip. Its Build returns a Pressable whose Label becomes the accessible name only — nothing visual is attached, so no icon-only control in the library shows a tip on hover, focus or long-press unless the app wraps it by hand.
- **Evidence**:

  ```
  IconButton.cs:111-116  return new Pressable(box, Disabled ? null : OnPressed)
          {
              Disabled = Disabled,
              Label = Label,
              PressedBackground = Disabled ? null : pressedFill,
          };
  ```

### C13 Tooltip · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/Tooltip.cs`
- **Handoff**: "long-press 500ms — bubble 8dp above" · "Trigger: long-press 500ms on icon-only controls" · "dismiss on release + 1.5s, scroll, or tap elsewhere."
- **Code**: The reveal is hover/focus only and is explicitly inert on touch, so the touch trigger the block specifies as the primary one has no implementation and no dismissal timers. The Anchored doc states the exclusion outright.
- **Evidence**:

  ```
  Tooltip.cs:44  OpenOnHover = true,
  VisualNode.cs:615-616  leaves = closed); never fires on touch. Composes with <see cref="Open"/> (either shows
      /// the panel).
  ```

### C13 Tooltip · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/Tooltip.cs`
- **Handoff**: "Inverse surface … · Caption 12/500 · padding 6/10 · Radius.Sm 6 · E3 · max-width 200, ≤ 2 lines." — E3.
- **Code**: The pill's BoxStyle sets no Elevation, so it paints at level 0 (ShadowSpec(0,0,0,transparent)). The block's own rationale for having no caret — "proximity + shadow anchor it" — depends on that shadow, which is not drawn.
- **Evidence**:

  ```
  Tooltip.cs:34-38  var pill = new Box(new BoxStyle
          {
              Background = theme.TextPrimary,
              CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Small)),
              Padding = EdgeInsets.Symmetric(Space.S2, Space.S1),
  ```

### C14 Select · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/Select.cs`
- **Handoff**: "No floating dropdown menus in v1 — every Select opens a BottomSheet picker (C1): one pattern, thumb-reachable, keyboard-safe." The anchored popover is explicitly scoped to the pointer tier only: "The pointer tier presents an anchored popover listbox (match-anchor width) instead of the BottomSheet".
- **Code**: Select builds an Anchored popover unconditionally — there is no density/tier branch and no BottomSheet path anywhere in the file. Touch users get the pointer-tier presentation the block rules out, and with it none of the sheet's contract (title, 52dp rows, commit-and-dismiss).
- **Evidence**:

  ```
  Select.cs:125-135  VisualNode select = new Anchored(trigger, panel)
          {
              Open = _open && !Disabled,
              OnDismiss = () => SetState(() => _open = false),
              MatchAnchorWidth = true,
              PanelRole = AnchorPanelRole.Listbox,
  ```

### C14 Select · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/Select.cs`
- **Handoff**: new Select(options, selected, onChanged, label: "Account", …) · "Sheet: title = label" · "A11y: trigger = button "Account, Checking 4821, collapsed""
- **Code**: The constructor has no label parameter and the class has no Label property — the fourth argument is placeholder. Nothing renders the "Account" caption above the field (TextInput's B9 anatomy puts it there at TextInput.cs:128), and the trigger Pressable carries no Label, so its accessible name is just the value text with the field's purpose missing.
- **Evidence**:

  ```
  Select.cs:27-28  public Select(IReadOnlyList<string> options, int selectedIndex = -1,
          Action<int>? onChanged = null, string? placeholder = null)
  Select.cs:123  : (VisualNode)new Pressable(field, Toggle) { Expanded = _open };
  ```

### C14 Select · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/Select.cs`
- **Handoff**: new Select(…, searchable: auto) · "> 8 options auto-pins a SearchField (B10) under the title"
- **Code**: No searchable parameter and no option count threshold exist. The panel is an unfiltered Column built by a straight loop over every option, so a 40-option Select renders 40 rows with no way to narrow them.
- **Evidence**:

  ```
  Select.cs:75-76  var list = new Column(gap: 0) { Width = SizeValue.Fill };
          for (var i = 0; i < Options.Count; i++)
  ```

### C14 Select · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/Select.cs`
- **Handoff**: "Keys — Combobox pattern: Space/Enter/↓ open · ↑/↓ move · type-ahead jumps · Enter commits · Esc closes without changing · Home/End."
- **Code**: Only ↓/↑/Enter are bound, and only while the panel is already open — the Shortcut nodes are inside `if (_open …)`. So ↓ on a closed Select does not open it, and Home/End are bound nowhere in the component (Space/Enter open via the Pressable, and Esc closes via the Anchored's own binding, so those two hold).
- **Evidence**:

  ```
  Select.cs:140-147  if (_open && !Disabled && Options.Count > 0)
          {
              select = new Shortcut(select, KeyChord.ArrowDown,
                  () => SetState(() => _highlight = Math.Min(Options.Count - 1, _highlight + 1)));
              select = new Shortcut(select, KeyChord.ArrowUp,
                  () => SetState(() => _highlight = Math.Max(0, _highlight - 1)));
  ```

### C16 NavigationRail · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/NavigationRail.cs`
- **Handoff**: "Pointer — Item hover = SurfaceSubtle pill (§10); cursor pointer."
- **Code**: The destination carries only a PRESSED fill (line 123). BoxStyle.Hover (the StyleDiff that lowers to CSS :hover — VisualNode.cs:222) is never set on the pill, the cell or the Pressable, and the generated stylesheet gives .eq-pressable only :active and :focus-visible rules (TokenCss.cs:317-332). Result: on a desktop pointer the rail gives no hover feedback at all — the fill only appears while the mouse button is held. The other pointer-tier components do set it (Menu.cs:88, Pagination.cs:108, Accordion.cs:76, DataTable.cs:199 all use `Hover = new StyleDiff { Background = theme.SurfaceSubtle }`). The `cursor: pointer` half of the clause IS satisfied — lowerPressable emits it unconditionally (lowering.ts:2059).
- **Evidence**:

  ```
  PressedBackground = theme.SurfaceSubtle,
  ```

## Subtle — real, but you have to look

126 rows.

### A1 Box · missing-component · **unverified**

- **Component**: `MISSING`
- **Handoff**: A11y — None by default (invisible to readers). Attach Semantics(label, role) to promote; interactive Boxes are a spec smell — use Button.
- **Code**: There is no Semantics node in the abstract vocabulary: the node list in src/eQuantic.UI.Primitives/Nodes/ has no such class, and nothing in eQuantic.UI.Components or the web realizer references one. The only promotion routes are Pressable (button/checkbox/switch roles), Link, TextEntry and Adjustable — each of which carries its own Label. A Box therefore cannot be given a label or a role; `SemanticRole` exists only on the native side, derived from those node types (src/eQuantic.UI.Native.Components/Semantics.cs:88-137).
- **Evidence**:

  ```
  case Pressable pressable:
  case Link link:
  case TextEntry entry:
  case Adjustable adjustable:   // Semantics.cs — the complete set of promotable sources
  ```

### A2 Row · Column · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Native.Framework/Layout/LayoutEngine.cs`
- **Handoff**: Truncation contract: text children shrink to ellipsis before any sibling is pushed out; fixed children (icons, avatars) never shrink.
- **Code**: The whole truncation block (text-to-ellipsis pass AND the flex-shrink pass that follows it at LayoutEngine.cs:1120-1167) is gated on `&& horizontal`, so it runs for Row only. An overflowing Column never clamps its text and never shrinks a child — siblings are pushed past the bottom edge and clipped. The comment two lines above claims the opposite ("Applies whenever the available extent is finite"), and the web realizer emits a plain column flex whose items shrink by default.
- **Evidence**:

  ```
  if (!float.IsPositiveInfinity(mainAvail) && rigidSum + gapTotal > mainAvail && horizontal)
  ```

### A2 Row · Column · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Primitives/Layout/LayoutTypes.cs`
- **Handoff**: RTL — Row mirrors automatically in RTL locales; reading/focus order stays = child order.
- **Code**: No realizer mirrors. The native layout maps Start to X unconditionally (LayoutEngine.cs:1266, and Padding.Start→X at 891/927/1473), and the web shell emits lang but never dir (src/eQuantic.UI.Server/Templates/app-shell.html:2 with the culture at UIExtensions.cs:561), so an ar/he culture renders LTR there too. LayoutTypes.cs:5 documents the v1 limit for the insets, while Row's own doc (VisualNode.cs:1418) still asserts mirroring happens.
- **Evidence**:

  ```
  /// Start/End instead of Left/Right so RTL mirroring is a realizer concern (v1 maps Start→left).
  var cursor = (horizontal ? flex.Padding.Start : flex.Padding.Top) + flex.Main switch
  ```

### A3 Stack · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Primitives/Nodes/VisualNode.cs`
- **Handoff**: Positioned(top/end/bottom/start, width?, height?)
- **Code**: Positioned takes only the four insets plus ZIndex — there is no width/height slot, so a positioned child cannot be given an explicit size and must carry a sized Box of its own (VisualNode.cs:1705-1723).
- **Evidence**:

  ```
  public Positioned(VisualNode child, float? top = null, float? end = null,
      float? bottom = null, float? start = null)
  ```

### A6 ScrollView · metric · **unverified**

- **Component**: `src/eQuantic.UI.Native.Components/PhotonHost.cs`
- **Handoff**: Drag slop 8dp before capture; captures from pressed children (press cancels into scroll).
- **Code**: The scroll pan captures at Touch.PressCancelSlop, which is 12dp, not 8dp. The capture-and-cancel half of the rule is implemented exactly as the handoff describes (the press clears when the pan arms) — only the figure differs. Note the same constant also serves the press-cancel rule, so a fix has to reconcile the two.
- **Evidence**:

  ```
  Tokens.cs:187  public const float PressCancelSlop = 12;
  PhotonHost.cs:1296  if (!pan.Active && MathF.Abs(travelled) > Touch.PressCancelSlop)
  ```

### A6 ScrollView · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Primitives/Nodes/VisualNode.cs`
- **Handoff**: contentPadding merges safe-area bottom (§A5). Keyboard: bottom inset grows by IME height; focused input kept visible (M4).
- **Code**: ScrollView exposes no contentPadding slot at all — its whole surface is Child, Axis, Width, Height, Offset, OnScrolled, OnViewportChanged — and "ContentPadding" appears nowhere in src/. With no such prop there is nothing for the safe-area bottom to merge into, so A5's "Don't wrap ScrollView in a bottom SafeArea — pass the inset as content padding instead" has no supported spelling. No IME-height inset either (no KeyboardHeight/ImeHeight anywhere). The keep-focused-input-visible half IS present: PhotonHost.ScrollIntoView (PhotonHost.cs:360).
- **Evidence**:

  ```
  VisualNode.cs:1837-1843
      public VisualNode Child { get; }
      public ScrollAxis Axis { get; init; }
      public SizeValue Width { get; init; }
      public SizeValue Height { get; init; }
  
  ```

### A6 ScrollView · semantics · **unverified**

- **Component**: `src/eQuantic.UI.Web/WebRealizer.cs`
- **Handoff**: Exposed as a scrollable region; VoiceOver 3-finger / TalkBack 2-finger swipes page by 80% viewport and announce "Page X of Y" when paging is enabled.
- **Code**: Nothing exposes the viewport to assistive tech on either target. Web: the element is a bare div carrying only Style — no role, no aria, no tabindex (the TS twin at lowering.ts:1150 is identical), while other nodes in the same file do set element.TabIndex/["role"] when they need them. Native: SemanticRole has no scrollable/region member and SemanticsTree.Walk has no ScrollView case, so the viewport is invisible to the bridges. No paging notion exists either. The one half that IS honoured is offscreen content staying in the tree (Semantics.cs:65-68).
- **Evidence**:

  ```
  WebRealizer.cs:607-609  var element = new RealizedElement("div")
          {
              Style = new HtmlStyle
  Semantics.cs:9-11  public enum SemanticRole : byte
  {
      StaticText,
  ```

### A6 ScrollView · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Native.Components/PhotonHost.cs`
- **Handoff**: Focused region: ↑/↓ line · PgUp/PgDn viewport · Home/End extremes. Space stays with the focused control, not the scroll.
- **Code**: No key ever moves a scroll region in the Photon host. The only writers into ScrollStore are the wheel entry point ScrollBy(x, y, delta) (PhotonHost.cs:1394), the pointer pan (1313), the fling on release (1775) and focus-driven ScrollIntoView (389). The "Home"/"End"/"ArrowUp"/"ArrowDown" cases at PhotonHost.cs:988-993 belong to the text-entry caret, not to a focused scroll region, and PageUp/PageDown appear nowhere outside the code editor's keymap. On web the div is not focusable either (no tabindex), so keyboard scrolling depends entirely on browser defaults.
- **Evidence**:

  ```
  PhotonHost.cs:1394  public bool ScrollBy(float x, float y, float delta)
  PhotonHost.cs:988-991  case "Home" or "ArrowUp":
                  MoveCaret(0, selecting);
                  return true;
              case "End" or "ArrowDown":
  ```

### A7 Divider · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/Divider.cs`
- **Handoff**: Consumes exactly its thickness — vertical rhythm comes from the parent's gap, never from Divider "spacing" props (it has none).
- **Code**: True for every horizontal case, but a VERTICAL divider with an inset consumes the parent's whole width instead of its 1dp thickness: the inset wrapper is hardcoded Width = SizeValue.Fill and pads on the horizontal axis regardless of Axis, so Divider(DividerInset.Middle, DividerAxis.Vertical) in a toolbar Row eats all the remaining space and pushes the line off-centre. Reachable straight from the public factory UI.Divider(inset, axis) (UI.cs:300-302). In-repo callers happen to dodge it — ListDetail.cs:107 uses the vertical divider with the default None inset.
- **Evidence**:

  ```
  Divider.cs:57-60  var padding = Inset == DividerInset.Leading
              ? new EdgeInsets(LeadingInset ?? Space.S4, 0, 0, 0)
              : EdgeInsets.Symmetric(Space.S4, 0);
          return new Box(new BoxStyle { Width = SizeValue.Fill, Padding = padding }, line);
  ```

### A8 Text · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Primitives/Theme/Typography.cs`
- **Handoff**: Dynamic Type — "Scales by OS factor to the role cap; re-shape + re-layout, atlas re-uses whitelist sizes. Never scales below ×1."
- **Code**: The scaling helper clamps the OS factor to a FLOOR of 0.5, not 1 — an OS factor below ×1 (iOS xSmall ≈ 0.82, Android fontScale 0.85) shrinks the type down to half the role's dp size. ScaledLineHeight applies the same 0.5 floor at Typography.cs:88, and no caller re-clamps: the shells pass the factor straight through (src/eQuantic.UI.Native.Shell.Apple/CoreTextService.cs:180-181, src/eQuantic.UI.Native.Shell.Android/AndroidTextService.cs:27-28) and the summary comment only documents `Size × min(factor, MaxScale)`, so the extra lower bound is unstated as well as wrong.
- **Evidence**:

  ```
  src/eQuantic.UI.Primitives/Theme/Typography.cs:63 —
          var scaled = Size * MathF.Min(MathF.Max(osFactor, 0.5f), MaxScale);
  src/eQuantic.UI.Primitives/Theme/Typography.cs:88 —
          var scaled = LineHeight * MathF.Min(MathF.Max(osFactor, 0.5f), MaxScale);
  ```

### A8 Text · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Native.Framework/Layout/LayoutEngine.cs`
- **Handoff**: Truncation — live (resize-safe): "maxLines: 1 · Ellipsis", "maxLines: 2 · Ellipsis"; "Wrapping happens at shaping time; the ellipsis glyph replaces the last cluster that fits". Rich runs: "Span(text, weight?, color?) children for inline emphasis."
- **Code**: MaxLines is honoured only on the plain-content path (passed to the measurer at line 714). The rich-run path branches away one line earlier and MeasureRuns never reads text.MaxLines — it wraps to as many lines as the words need and reports `lines.Count * lineHeight` as the height. The draw path agrees (src/eQuantic.UI.Native.Components/PhotonRealizer.cs:1272-1290 emits every fragment), so on Photon a Text with Spans and maxLines: 2 renders unlimited lines, un-ellipsised, and overflows the box the card reserved for it. The web/TS realizers clamp with CSS, so the two targets disagree on the same tree.
- **Evidence**:

  ```
  src/eQuantic.UI.Native.Framework/Layout/LayoutEngine.cs:713-714 —
          if (text.Spans is { Count: > 0 } spans) return MeasureRuns(result, text, spans, style, maxW, ctx);
          var measurement = ctx.Measurer.Measure(text.PlainContent, style, ctx.TypeScale, maxW, text.MaxLines);
  src/eQuantic.UI.Native.Framework/Layout/LayoutEngine.cs:778-779 (MeasureRuns, no MaxLines anywhere in the method) —
          result.Text = new TextMeasurement(widest, lines.Count * lineHeight, lineHeight, lines);
          result.Bounds = new Rect(0, 0, widest, lines.Count * lineHeight);
  ```

### A8 Text · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Primitives/Layout/LayoutTypes.cs`
- **Handoff**: Layout — "Baseline exposed to parents for cross-align."
- **Code**: There is no baseline anywhere in the layout contract: CrossAlign offers only Start/Center/End/Stretch, so a parent cannot ask for baseline alignment, and TextMeasurement (the only thing a Text hands back to its parent) carries Width/Height/LineHeight/Lines with no baseline member — a Row mixing a Display number and a Caption unit centers their line boxes instead of sitting them on a shared baseline. Grepping the repo, the word `baseline` appears only inside the shells' own raster math (CoreTextService.cs:247, AndroidTextService.cs:51), never in the framework layout.
- **Evidence**:

  ```
  src/eQuantic.UI.Primitives/Layout/LayoutTypes.cs:50-56 —
  public enum CrossAlign : byte
  {
      Start = 0,
      Center = 1,
      End = 2,
  ```

### A10 Icon · semantics · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Primitives/Nodes/Icon.cs`
- **Handoff**: "With label → image role." / "Semantics: Labelled Icon announces its label (image role)"
- **Code**: Both web lowerings put only aria-label on the bare <svg> and never emit role="img", so the labelled icon has no image role in the accessibility tree (src/eQuantic.UI.Web/WebRealizer.cs:919, twin at src/eQuantic.UI.Runtime/src/shared/lowering.ts:1374). No role is added downstream either — WebRealizer sets element.Role for dialog/button/radio/tab/menuitem/option/switch/checkbox/tooltip/listbox but never for a glyph.
- **Evidence**:

  ```
  if (label is { }) svg.RawAttributes["aria-label"] = label;
  else svg.RawAttributes["aria-hidden"] = "true";   // WebRealizer.cs:919-920
  if (label) attributes['aria-label'] = label;
  else attributes['aria-hidden'] = 'true';           // lowering.ts:1374-1375
  ```

### A11 Image · missing-feature · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Primitives/Nodes/Image.cs`
- **Handoff**: "Semantics: Labelled = image role + label; unlabelled = decorative, hidden." / "Alt read as image role."
- **Code**: The native semantics walk has a case for a labelled Icon but none for Image, so on Photon (iOS/macOS/Android) an Image with alt text is never emitted as a SemanticNode and is invisible to assistive tech — SemanticRole.Image is produced in exactly one place, the Icon case (src/eQuantic.UI.Native.Components/Semantics.cs:145-148). Web is correct (<img alt> carries the role).
- **Evidence**:

  ```
  // A labelled icon announces; an unlabelled one is decoration and stays silent.
  case Icon { Label: { Length: > 0 } label }:
      nodes.Add(new(SemanticRole.Image, node.Path ?? "", node.Bounds,
          label, null, false));
      return;                                   // Semantics.cs:144-148 — no `case Image` anywhere in Walk
  ```

### A11 Image · missing-feature · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Primitives/Nodes/Image.cs`
- **Handoff**: "alt: required or explicit decorative: true — omitting both is a debug warning."
- **Code**: There is no `decorative` parameter or property on Image, and alt defaults to the empty string, so omitting both is silently treated as decorative with no warning of any kind — grep for "decorative" across src/ turns up only doc comments. The author cannot distinguish "I meant decorative" from "I forgot the alt".
- **Evidence**:

  ```
  public Image(string source, float width, float height, ImageFit fit = ImageFit.Cover, string alt = "")   // Image.cs:25
  /// <summary>Empty string = decorative (per HTML semantics).</summary>
  public string Alt { get; init; }                                                                          // Image.cs:48-49
  ```

### A12 Button · documented-deviation · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/Button.cs`
- **Handoff**: "Web role=button, name = label; loading = aria-busy=true — a state, not a name change." · "Announces: "Continue, button" · disabled: "…dimmed" · loading: "…busy"."
- **Code**: Loading is folded into `inert` and lowered as DISABLED, not busy: nothing on the tree can even carry aria-busy for a Pressable (no Busy field on Pressable; `aria-busy` appears only on HtmlElement.cs:463, the raw-HTML escape hatch). The component's own doc names the choice and justifies it: "The button reads disabled to assistive tech for the duration, which is the truth: it cannot be actioned." Stated reason accepted — but it also means a loading button silently loses keyboard focus (see the disabled/tab-order finding).
- **Evidence**:

  ```
  Button.cs:71  var inert = Disabled || Loading;   (Button.cs:49-51 doc: "The button reads disabled to assistive tech for the duration, which is the truth: it cannot be actioned.")
  ```

### A12 Button · behaviour · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/Button.cs`
- **Handoff**: "Pressed fill applies on touch-down in the same frame (≤ 50ms budget); release fades back over Fast 100ms."
- **Code**: The generated stylesheet puts a symmetric 100ms background-color transition on the pressed child, so the pressed fill fades IN over Motion.FastMs as well as out — there is no rule suppressing the transition on :active. The press-in is animated over 100ms instead of landing in the same frame. (Photon is the mirror image: PhotonRealizer.cs:554-555 swaps the fill instantly with no release fade.)
- **Evidence**:

  ```
  TokenCss.cs:318  css.AppendLine(".eq-pressable > :first-child { transition: background-color var(--eq-motion-fast) ease-out; }");   with TokenCss.cs:283  --eq-motion-fast: {Motion.FastMs}ms;
  ```

### A12 Button · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/Button.cs`
- **Handoff**: new Button("Continue", variant: Variant.Primary, size: SizeVariant.Medium, icon: Icons.ArrowRight?, loading: false, onPressed: fn)
- **Code**: The constructor takes only (label, variant, size, onPressed) and the mandated factory surface mirrors it exactly (UI.cs:226-228), so the handoff's call does not compile. `icon` and `loading` exist only as init-only properties reachable through `new Button(…) { Leading = …, Loading = true }` — i.e. unreachable from the `using static UI` factory form the SDK prescribes. The icon slot is also typed IconGlyph, not Icons: callers must write `CuratedIcons.Resolve(Icons.Plus)` (samples/WalletMobile/WalletApp.cs:617).
- **Evidence**:

  ```
  Button.cs:18-19  public Button(string label, Variant variant = Variant.Primary, SizeVariant size = SizeVariant.Medium,\n        Action? onPressed = null)   /  UI.cs:226-228  public static Button Button(string label, Variant variant = Variant.Primary,\n        SizeVariant size = SizeVariant.Medium, Action? onPressed = null) =>\n        new Button(label, variant, size, onPressed);
  ```

### A13 IconButton · missing-feature · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/IconButton.cs`
- **Handoff**: new IconButton(Icons.Heart, label: "Favorite", kind: IconButtonKind.Standard, selected: bool?, onPressed: fn)
- **Code**: `selected` is not a constructor parameter and is absent from the factory (UI.cs:318-321), so the handoff's call does not compile; the toggle state is only settable through an object initializer on `new`. It is also `bool`, not `bool?` — the component cannot distinguish "not a toggle at all" from "a toggle currently off", which is precisely the distinction Pressable.Selected (bool?) exists to carry (VisualNode.cs:1098).
- **Evidence**:

  ```
  IconButton.cs:27-28  public IconButton(Icons glyph, string label, IconButtonKind kind = IconButtonKind.Standard,\n        SizeVariant size = SizeVariant.Medium, Action? onPressed = null)   /  IconButton.cs:45  public bool Selected { get; init; }
  ```

### A13 IconButton · missing-feature · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/IconButton.cs`
- **Handoff**: "The label surfaces as a Tooltip (C13) on ~500ms hover — the pointer twin of long-press."
- **Code**: IconButton returns the Pressable directly — it never composes a Tooltip, so the required label surfaces only as aria-label and a pointer user gets no hint. Tooltip (C13) does exist in the SDK (src/eQuantic.UI.Components/Tooltip.cs, hover-reveal via Anchored), so this is wiring the component does not do, not a missing dependency; the block's separate long-press form is explicitly marked "(Phase C)" and is out of scope. Tooltip.cs also fences the delay: "v1 fences: show/hide delay, arrow caret" — the ~500ms is not implemented anywhere.
- **Evidence**:

  ```
  IconButton.cs:111  return new Pressable(box, Disabled ? null : OnPressed)   (no Tooltip/Anchored anywhere in IconButton.cs)
  ```

### B1 Card · missing-feature · **REFUTED**

- **Component**: `src/eQuantic.UI.Components/Card.cs`
- **Handoff**: "Footer: right-aligned Small buttons over a Divider, 10dp vertical padding." (constructor slot `footer: CardFooter?`)
- **Code**: There is no footer slot and no CardFooter type anywhere in the repo (grep for CardHeader/CardFooter across src, tests and samples returns nothing). The Card constructor takes one undifferentiated `child`. Unlike the header, the footer is NOT named in the doc comment's delegation clause, so the 10dp vertical padding and the Divider above the button row exist nowhere.
- **Evidence**:

  ```
  Card.cs:23  public Card(VisualNode child, CardKind kind = CardKind.Elevated)
  Card.cs:29  public VisualNode Child { get; init; }
  ```

### B2 List · ListItem · metric · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/ListItem.cs`
- **Handoff**: "content (title 15/500 + subtitle 13, each 1 line; 2 lines for the 3-line item)"
- **Code**: The subtitle rides TypeRole.Caption, which resolves to 12dp in PhotonTheme (and 12dp in MaterialTheme too), not 13. The title next to it was deliberately pinned to the handoff figure with an exact StyleOverride of 15/Medium — the subtitle did not get the same treatment, so it renders one dp small under every theme.
- **Evidence**:

  ```
  ListItem.cs:98  content.Add(new Text(subtitle, TypeRole.Caption, theme.TextSecondary,
  PhotonTheme.cs:120  TypeRole.Caption => new TypeStyle(12, 16, FontWeight.Medium, 0.2f, 1.3f),
  (cf. ListItem.cs:95  StyleOverride = new TypeStyle(15, 20, FontWeight.Medium, 0, 1.3f),)
  ```

### B2 List · ListItem · semantics · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/ListItem.cs`
- **Handoff**: "A11y: one merged node per row — \"Wi-Fi, Photon-5G, button\""
- **Code**: The Pressable's Label is set to the Title alone, and the realizer emits it as aria-label on the row element (WebRealizer.cs:1802). aria-label REPLACES the element's text content as the accessible name, so the subtitle inside the row is never announced: the row reads "Wi-Fi, button", not "Wi-Fi, Photon-5G, button".
- **Evidence**:

  ```
  ListItem.cs:123  Label = Title,
  WebRealizer.cs:1802  AriaLabel = pressable.Label,
  ```

### B2 List · ListItem · semantics · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/ListItem.cs`
- **Handoff**: "Semantics: list/listitem"
- **Code**: List.Build emits a plain Column (a div) holding the rows, and a row is either a bare Box (div) or a Pressable (button). Neither role="list" nor role="listitem" is emitted anywhere — grep for "listitem" across the whole of src returns only icon glyph names. Assistive tech gets no list structure, so no item count and no "item 3 of 12".
- **Evidence**:

  ```
  ListItem.cs:156  var column = new Column(gap: 0) { Width = SizeValue.Fill };
  ListItem.cs:164  return column;
  ```

### B2 List · ListItem · behaviour · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Web/TokenCss.cs`
- **Handoff**: "Pressed = SurfaceSubtle full-bleed fill, instant on/Fast off."
- **Code**: The generated pressed mechanics put a symmetric transition on the fill, so the press fill fades IN over Motion.FastMs as well as out. There is no instant-on side (no `transition: none` on :active, no separate in/out duration), so the row's press feedback is 100ms late rather than immediate. The fill token and the full-bleed target are correct.
- **Evidence**:

  ```
  TokenCss.cs:318  css.AppendLine(".eq-pressable > :first-child { transition: background-color var(--eq-motion-fast) ease-out; }");
  TokenCss.cs:319  css.AppendLine(".eq-pressable:active > :first-child { background-color: var(--eq-pressed-bg) !important; }");
  ```

### B2 List · ListItem · missing-feature · **CONFIRMED**

- **Component**: `MISSING`
- **Handoff**: "Right-click on a row opens the SAME actions its swipe/long-press exposes — never new ones."
- **Code**: There is no context-menu or long-press path in the framework: grep for ContextMenu / contextmenu / OnContextMenu / LongPress across src/eQuantic.UI.Primitives and src/eQuantic.UI.Components returns nothing. SwipeableRow exists and exposes exactly one action (SwipeableRow.cs:23-35), but nothing lets a right-click reach it, so the §10 pointer contract for rows is unimplemented.
- **Evidence**:

  ```
  src/eQuantic.UI.Components/SwipeableRow.cs:26  public SwipeableRow(VisualNode child, string actionLabel, Icons actionIcon, Action? onAction = null)  — no context-menu hook on this or any node type
  ```

### B3 AppBar · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/AppBar.cs`
- **Handoff**: Elevation reacts to scroll offset > 0: Background → Surface fill + E2.
- **Code**: The swap itself is right (rendered Scrolled root: background-color Surface + box-shadow 0 2px 8px), but nothing observes scroll — Scrolled is an owner-set bool the caller must flip. Stated reason (AppBar.cs:9-10): "the scrolled Surface+E2 elevation swap joins the scroll-linking system".
- **Evidence**:

  ```
  AppBar.cs:41  public bool Scrolled { get; init; }
  ```

### B3 AppBar · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/AppBar.cs`
- **Handoff**: Background → Surface fill + E2, animated Base 200ms standard.
- **Code**: The root BoxStyle carries no TransitionSpec, so flipping Scrolled SNAPS — the rendered scrolled root has no `transition` declaration at all. The facility exists and is used elsewhere (SegmentedControl.cs:80 `TransitionSpec.Of(StyleChannels.Colors | StyleChannels.Shadow, Motion.Press)`), and the doc fence covers the scroll LINKING, not the animation.
- **Evidence**:

  ```
  AppBar.cs:74-75  Background = Scrolled ? theme.Surface : null,
              Elevation = Scrolled ? 2 : 0,
  ```

### B3 AppBar · semantics · **unverified**

- **Component**: `src/eQuantic.UI.Components/AppBar.cs`
- **Handoff**: Semantics: Header landmark.
- **Code**: The root lowers to a plain <div> with no role and no <header> element (verified render: `<div style="...height: 56px; padding: 0 4px 0 4px...">`, no role attribute). No landmark facility exists in the Primitives vocabulary — the same gap is named in ListItem.cs:53.
- **Evidence**:

  ```
  AppBar.cs:69-76  return new Box(new BoxStyle { Width = SizeValue.Fill, Height = 56, ... }, row);
  ```

### B3 AppBar · semantics · **unverified**

- **Component**: `src/eQuantic.UI.Components/AppBar.cs`
- **Handoff**: A11y: title is the screen's level-1 Heading and the focus anchor after navigation.
- **Code**: The title is an ordinary Text node, which lowers to <span> (WebRealizer.cs:1611 `new RealizedElement("span")`) — no heading role, no aria-level, and nothing moves focus to it after navigation. The Text node has no heading/level property at all, so this is not expressible today.
- **Evidence**:

  ```
  AppBar.cs:54  var title = new Text(Title, TypeRole.Title, theme.TextPrimary, maxLines: 1)
  ```

### B4 BottomNavigation · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/BottomNavigation.cs`
- **Handoff**: bottom safe-area painted in bar fill.
- **Code**: No SafeArea node or bottom inset anywhere in the tree — the bar is exactly 56dp. Stated reason (BottomNavigation.cs:18): "the bottom safe-area inset joins the host insets".
- **Evidence**:

  ```
  BottomNavigation.cs:98  Height = 56,
  ```

### B4 BottomNavigation · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/BottomNavigation.cs`
- **Handoff**: Select: pill scales-in from 0.6 + fades, Base 200ms standard; glyph crossfades outline→filled 100ms. Press-down shows the pill at 40% instantly.
- **Code**: The pill BoxStyle has no Transform and no TransitionSpec, and the glyph swap is a bare conditional — the only motion in the rendered output is the global `.eq-pressable > :first-child` background-color transition (TokenCss.cs:318). Press-down is also the wrong shape: that :first-child is the item COLUMN, so the SurfaceSubtle wash covers the whole (hugged) column rather than showing the pill at 40%. Neither is among the two fences the class doc names (E2 shadow, safe-area); PageIndicator.cs:62 shows the Colors|Size + Motion.State facility this needs.
- **Evidence**:

  ```
  BottomNavigation.cs:86  PressedBackground = theme.SurfaceSubtle,
  ```

### B4 BottomNavigation · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/BottomNavigation.cs`
- **Handoff**: Pointer: Item hover = SurfaceSubtle pill (§10); cursor pointer.
- **Code**: No BoxStyle.Hover anywhere in the component and no `.eq-pressable:hover` rule in the generated stylesheet (TokenCss.cs:317-332 covers only :active, :focus-visible and tap-highlight), so the item has NO hover state on pointer devices; the rendered item carries no hover declaration. Hover-as-StyleDiff is the established pattern in eight sibling components (IconButton.cs:108, Menu.cs:88, Pagination.cs:108…). `cursor: pointer` is present and correct.
- **Evidence**:

  ```
  BottomNavigation.cs:63-69  var pill = new Box(new BoxStyle { Width = 56, Height = 26, Background = isActive ? primary.Subtle : null, CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)), }, pillContent);
  ```

### B4 BottomNavigation · semantics · **unverified**

- **Component**: `src/eQuantic.UI.Components/BottomNavigation.cs`
- **Handoff**: Semantics: Navigation landmark; selected = aria-current="page" on web.
- **Code**: aria-current="page" is delivered exactly as specified (Role = PressableRole.Destination → rendered `aria-current="page"` on the selected button only), but the bar's root is a plain <div> — no <nav>, no role="navigation", so the landmark half of the line is missing.
- **Evidence**:

  ```
  BottomNavigation.cs:95-99  return new Box(new BoxStyle { Width = SizeValue.Fill, Height = 56, Background = theme.Surface, }, row);
  ```

### B4 BottomNavigation · semantics · **unverified**

- **Component**: `src/eQuantic.UI.Components/BottomNavigation.cs`
- **Handoff**: Badge value appended: "Cards, tab, 2 of 4, 2 new items".
- **Code**: Pressable.Label is the bare item label, and because it lowers to aria-label it also SUPPRESSES the badge text that is inside the button — the verified render is `<button aria-label="Cards">` wrapping a badge span containing "2". A screen reader hears "Cards" and never the count. Nothing appends the BadgeCount to the accessible name.
- **Evidence**:

  ```
  BottomNavigation.cs:85  Label = item.Label,
  ```

### B4 BottomNavigation · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/BottomNavigation.cs`
- **Handoff**: Dynamic Type: labels scale to ×1.3, bar grows; icons fixed.
- **Code**: Labels do carry MaxScale 1.3 (BottomNavigation.cs:80) and icons are fixed at IconSize.Md, but the bar is a FIXED Height, not a MinHeight, so it cannot grow — at ×1.3 the 11/14 label grows inside an unchanged 56dp bar (pill 26 + gap 2 + line 18 = 46 before any padding) and clips rather than pushing the bar taller. BoxStyle.MinHeight is the facility for this and is used elsewhere (ListItem.cs:114, Table.cs:52).
- **Evidence**:

  ```
  BottomNavigation.cs:98  Height = 56,
  ```

### B5 Tabs · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/Tabs.cs`
- **Handoff**: new Tabs(labels: [...], selected: 0, mode: TabMode.Fixed | Scrollable, onSelect: fn) · Scrollable: 5+, hug + padding X 16, leading-aligned, edge fade via 24dp gradient overlay.
- **Code**: There is no TabMode type in the repo (grep over src/ finds none) and the constructor has no mode parameter — every strip is Fixed. Stated reason (Tabs.cs:9-10): "v1 fences: Scrollable mode (edge-fade gradient) … join the animation/gesture systems".
- **Evidence**:

  ```
  Tabs.cs:14  public Tabs(IReadOnlyList<string> labels, int selected, Action<int>? onSelect = null)
  ```

### B5 Tabs · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/Tabs.cs`
- **Handoff**: Keys: The tablist is ONE tab stop: ←/→ move focus and select (wraps) · Home/End · Tab leaves to the active panel.
- **Code**: ←/→ and the wrap are correct (Tabs.cs:78 `(Selected + direction + count) % count`), but Home and End do nothing: the Adjustable keydown maps only the four arrows and returns on direction 0, and the C# SSR twin emits no keydown at all (WebRealizer.cs:1088-1111). Nothing else in the tree handles Home/End.
- **Evidence**:

  ```
  src/eQuantic.UI.Runtime/src/shared/lowering.ts:2387-2392  const direction = event.key === 'ArrowRight' ? 1 : event.key === 'ArrowLeft' ? -1 : event.key === 'ArrowUp' ? (downIsNext ? -1 : 1) : event.key === 'ArrowDown' ? (downIsNext ? 1 : -1) : 0;  if (direction === 0) return;
  ```

### B5 Tabs · semantics · **unverified**

- **Component**: `src/eQuantic.UI.Components/Tabs.cs`
- **Handoff**: tablist / tab / tabpanel; selected tab = aria-selected=true + aria-controls → its panel. Panel change moves reader focus to the panel.
- **Code**: role=tablist (one Tab stop), role=tab and aria-selected are all correct in the render, but aria-controls is never emitted and cannot be: Tabs owns no panel and exposes no panel/id parameter, so there is no tabpanel to point at and no focus move on change. The lowering emits role + aria-selected + tabindex and nothing else. The doc's fences name Scrollable, the indicator translation, panel SWIPES and the inline Badge — not the panel association.
- **Evidence**:

  ```
  src/eQuantic.UI.Runtime/src/shared/lowering.ts:2084-2086  node.attributes['role'] = 'tab'; node.attributes['aria-selected'] = pressable.selected === true ? 'true' : 'false'; node.attributes['tabindex'] = '-1';
  ```

### B5 Tabs · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/Tabs.cs`
- **Handoff**: Pointer: Tab hover = SurfaceSubtle wash behind the label (§10); cursor pointer.
- **Code**: The cell carries PressedBackground but no BoxStyle.Hover, and there is no `.eq-pressable:hover` rule in the generated stylesheet (TokenCss.cs:317-332) — the rendered tab has no hover declaration, so pointer hover shows nothing. Hover-as-StyleDiff is used by eight sibling components (e.g. Pagination.cs:108, which is the closest analogue). `cursor: pointer` is present.
- **Evidence**:

  ```
  Tabs.cs:65  PressedBackground = theme.SurfaceSubtle,
  ```

### B5 Tabs · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/Tabs.cs`
- **Handoff**: Indicator = 3dp rrect bar … drawn once, translated between tabs · Indicator slides Base 200ms standard, stretching ~15% mid-flight; panel swipe drags the indicator proportionally.
- **Code**: Every cell draws its own indicator slot and the inactive ones draw an empty 3dp box (verified render: three `height: 3px; padding: 0 16px` divs, only the active one holding a filled child), so nothing is translated and there is no TransitionSpec on it. Stated reason (Tabs.cs:9-10): "the indicator TRANSLATION between tabs and panel swipes join the animation/gesture systems".
- **Evidence**:

  ```
  Tabs.cs:47-60  cell.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 3, Padding = EdgeInsets.Symmetric(Space.S4, 0), }, isActive ? new Box(...) : null));
  ```

### B5 Tabs · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/Tabs.cs`
- **Handoff**: Optional count Badge inline after the label (B7 inline form).
- **Code**: Labels is IReadOnlyList<string>, so no node can be placed after a label and no badge slot exists. The doc's stated reason is "the inline count Badge composes externally" (Tabs.cs:10) — but with a string-only API there is no external composition point either; B4's NavItem, by contrast, does carry a BadgeCount.
- **Evidence**:

  ```
  Tabs.cs:21  public IReadOnlyList<string> Labels { get; init; }
  ```

### B6 Avatar · behaviour · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/Avatar.cs`
- **Handoff**: "initials (2 chars, tinted 2-stop gradient hashed from the name)"
- **Code**: The tint is picked by the LENGTH of the name, not by a hash of the name: src/eQuantic.UI.Components/Avatar.cs:71 indexes a 5-entry palette with seed.Length % 5. Every name of the same length gets the same tint ("Ana Beatriz" and "Carlos Mesqu" both land on index 1), and renaming a person without changing the character count never changes the colour.
- **Evidence**:

  ```
  Avatar.cs:70  var seed = Name ?? Initials;
  Avatar.cs:71  var tint = theme.Colors(TintPalette[seed.Length % TintPalette.Length]);
  ```

### B6 Avatar · semantics · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/Avatar.cs`
- **Handoff**: "Semantics — Image role named by the person or entity; the initials fallback keeps the same name." / "Non-interactive by default (image role, name as label)"
- **Code**: Only the IMAGE tier carries the name (passed as Image alt, Avatar.cs:63). The initials tier renders a bare Text of the 2 clipped characters with no accessible name (Avatar.cs:83-84), and the person-glyph tier renders an Icon with no Label, which Icon documents as decorative/aria-hidden (src/eQuantic.UI.Primitives/Nodes/Icon.cs:142 "Accessibility label; null = decorative (aria-hidden on web)"). A screen reader gets "AB" — or nothing at all on the glyph tier — instead of "Ana Beatriz".
- **Evidence**:

  ```
  Avatar.cs:63  var photo = new Image(source, side, side, ImageFit.Cover, Name ?? Initials)
  Avatar.cs:84  ? new Text(clipped, TypeRole.Caption, tint.OnSubtle, maxLines: 1)
  Avatar.cs:88  : new Icon(Icons.Person, glyphSize, theme.TextMuted);
  ```

### B6 Avatar · missing-feature · **REFUTED**

- **Component**: `src/eQuantic.UI.Components/Avatar.cs`
- **Handoff**: "new Avatar(image?, initials: \"AB\", size: SizeVariant.Medium, status: Status.Online?)" — image and status are constructor slots
- **Code**: The constructor takes only (initials, size, name) (Avatar.cs:26); ImageSource and Status are init-only PROPERTIES (Avatar.cs:37, Avatar.cs:46), and the generated declarative factory mirrors the constructor (src/eQuantic.UI.Components/UI.cs:255-257). Under the repo's own authoring rule (factories mirror the ctor, no `new`), the photo tier and the presence dot are unreachable from the declarative surface — a caller must fall back to `new Avatar(...) { ImageSource = …, Status = … }`.
- **Evidence**:

  ```
  Avatar.cs:26  public Avatar(string initials, SizeVariant size = SizeVariant.Medium, string? name = null)
  Avatar.cs:37  public string? ImageSource { get; init; }
  Avatar.cs:46  public PresenceStatus Status { get; init; }
  UI.cs:255  public static Avatar Avatar(string initials, SizeVariant size = SizeVariant.Medium,
  ```

### B7 Badge · metric · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/Badge.cs`
- **Handoff**: "attach: Stack + Positioned(top: −4, end: −4)"
- **Code**: The shared attach helper pins the badge at end: -8, twice the spec's horizontal offset (src/eQuantic.UI.Components/Badge.cs:46). Nothing in the doc comment (Badge.cs:37-41) names or justifies the change, and both consumers inherit it — src/eQuantic.UI.Components/BottomNavigation.cs:60 and src/eQuantic.UI.Components/NavigationRail.cs:93.
- **Evidence**:

  ```
  Badge.cs:46  stack.Add(new Positioned(new Badge(count) { Ring = true }, top: -4, end: -8));
  ```

### B7 Badge · semantics · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/Badge.cs`
- **Handoff**: "Decorative; the count folds into the host's announcement (\"Notifications, 3 new\")."
- **Code**: The count never reaches the host's accessible name. The badge is attached inside the host's Pressable (BottomNavigation.cs:60 / NavigationRail.cs:93), and the Pressable's Label is the plain item label (BottomNavigation.cs:85 `Label = item.Label`), which lowers to aria-label (src/eQuantic.UI.Web/WebRealizer.cs:1802) and therefore OVERRIDES the badge text inside the button. The button announces "Cards" with no mention of the 2 unread items.
- **Evidence**:

  ```
  BottomNavigation.cs:60  var iconNode = item.BadgeCount > 0 ? Badge.Over(icon, item.BadgeCount) : (VisualNode)icon;
  BottomNavigation.cs:85  Label = item.Label,
  WebRealizer.cs:1802  AriaLabel = pressable.Label,
  ```

### B8 Chip · semantics · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/Chip.cs`
- **Handoff**: "Input remove = custom action \"remove rio-trip.pdf\""
- **Code**: The remove control's accessible name is the bare, unparameterised string "Remove" — it never names the chip it removes (Chip.cs:76; SdkStrings.Remove resolves to the resx value "Remove", src/eQuantic.UI.Components/SdkResources.resx:15-17). In a row of five removable chips every ✕ announces identically.
- **Evidence**:

  ```
  Chip.cs:76  Label = SdkStrings.Remove,
  SdkResources.resx:16  <value>Remove</value>
  ```

### B8 Chip · behaviour · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/Chip.cs`
- **Handoff**: "Input (removable; close 20dp visual, 48dp hit)"
- **Code**: The 20dp visual is right (IconSize.Dense = 20, src/eQuantic.UI.Primitives/Theme/Tokens.cs:48) but the 48dp hit does not exist on the WEB target. The chip relies on Pressable's §08 contract (Chip.cs:16-17 "48dp hit through Pressable"), and the native realizer honours it (PhotonRealizer.ExpandHitRect, src/eQuantic.UI.Native.Components/PhotonRealizer.cs:1658-1666, grows the rect to Touch.MinTarget), but WebRealizer.LowerPressable emits a <button> with padding 0 and no min-width/min-height, and TokenCss adds no sizing rule for .eq-pressable — so the web ✕ is a ~20×20 target. Components that need the guarantee on web build it themselves (Slider.cs:98, PageIndicator.cs:88 both set Height = Touch.MinTarget).
- **Evidence**:

  ```
  Chip.cs:73  content.Add(new Pressable(new Icon(Icons.Close, IconSize.Dense, textColor), OnRemove)
  WebRealizer.cs:1791  Padding = "0",
  PhotonRealizer.cs:1662  var minimum = density == Density.Compact ? 0 : Touch.MinTarget;
  ```

### B9 TextInput · metric · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/TextInput.cs`
- **Handoff**: Container: Radius.Md 10 · border 1dp BorderStrong on Surface · padding X 14 · slot gap 10 · text 16/400
- **Code**: The entry text rides TypeRole.BodyL — TextInput never sets Role, and TextEntry's default is BodyL (src/eQuantic.UI.Primitives/Nodes/VisualNode.cs:934), which the theme resolves to 17/400, not 16/400 (src/eQuantic.UI.Primitives/Theme/PhotonTheme.cs:117 `TypeRole.BodyL => new TypeStyle(17, 24, FontWeight.Regular, 0f, 1.3f)`). Cross-pinned as bodyl by tests/eQuantic.UI.Web.Tests/TextInputRealizerTests.cs:54. Every other B9 container figure resolves correctly (radius 10 via Shape(Medium), 1dp BorderStrong on Surface, padding 14/13, gap 10, leading Icon Dense 20 TextMuted).
- **Evidence**:

  ```
  row.Add(new Flexible(new TextEntry(Value, OnChanged) { ... }, 1));  // TextInput.cs:100-114 — Role never set, so BodyL (17dp) applies
  ```

### B9 TextInput · semantics · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/TextInput.cs`
- **Handoff**: error announced assertively on appear and appended to the field's description
- **Code**: The description twin that carries the error is a POLITE live region on both realizers, so an error appearing mid-form queues behind whatever is speaking instead of interrupting: src/eQuantic.UI.Web/WebRealizer.cs:1063 and src/eQuantic.UI.Runtime/src/shared/lowering.ts:861. The region is shared with the non-error helper text (TextInput.cs:91 feeds one string into TextEntry.Description at :104), so there is no seam to raise politeness only for the error. The append-to-description half of the claim IS met (aria-describedby, WebRealizer.cs:1070).
- **Evidence**:

  ```
  RawAttributes = new Dictionary<string, string> { ["aria-live"] = "polite" },  // WebRealizer.cs:1063
  ```

### B9 TextInput · behaviour · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/TextInput.cs`
- **Handoff**: Focus: tap anywhere in the container (whole box = hit target)
- **Code**: The container is an inert Box (TextInput.cs:116-125) with no Pressable, no label association and no focus forwarding; the entry only fills the Flexible slot inside it. The 14dp horizontal padding and the leading icon are therefore dead to a tap/click — nothing in the runtime forwards a container press to the input either (lowerTextEntry attaches focus handlers to the <input> alone, src/eQuantic.UI.Runtime/src/shared/lowering.ts:847-851).
- **Evidence**:

  ```
  var container = new Box(new BoxStyle { ... Padding = EdgeInsets.Symmetric(paddingX, 0), }, row);  // TextInput.cs:116-125 — a Box, not a Pressable, and no <label> wrapper on either target
  ```

### B9 TextInput · behaviour · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/TextInput.cs`
- **Handoff**: Pointer: I-beam over the field; hover swaps the border Border→BorderStrong (Motion.Press).
- **Code**: There is no hover state at all: the border colour is a pure function of hasError and _focused, and the resting colour is already BorderStrong, so the hover swap the pointer contract describes can never happen. The component does not wrap the container in a Hoverable (the node exists — src/eQuantic.UI.Runtime/src/shared/lowering.ts:353 lowerHoverable) and sets no Transition, so Motion.Press is unused here too.
- **Evidence**:

  ```
  var borderColor = hasError ? theme.Colors(Variant.Destructive).Base : _focused ? theme.Colors(Variant.Primary).Base : theme.BorderStrong;  // TextInput.cs:82-84 — no hover term
  ```

### B9 TextInput · missing-feature · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/TextInput.cs`
- **Handoff**: Keys: ... Enter commits single-line
- **Code**: TextInput exposes no submit hook and never sets TextEntry.OnSubmit when it builds the entry (TextInput.cs:100-114 sets Placeholder/Label/Description/Invalid/Disabled/Obscure/Autofocus/OnFocusChanged only), so Enter has nothing to commit to. The plumbing exists on both targets and goes unused: TextEntry.OnSubmit (src/eQuantic.UI.Primitives/Nodes/VisualNode.cs:923), the web keydown (lowering.ts:841-846) and the native Enter case (src/eQuantic.UI.Native.Components/PhotonHost.cs:996-999). SearchField wires it (SearchField.cs:44); TextInput does not.
- **Evidence**:

  ```
  public TextInput(string value, Action<string>? onChanged = null, string label = "", string? placeholder = null, string? helper = null, string? error = null, Icons? leading = null, SizeVariant size = SizeVariant.Large)  // TextInput.cs:20-22 — no onSubmit parameter, and no OnSubmit property on the class
  ```

### B9 TextInput · behaviour · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/TextInput.cs`
- **Handoff**: Keys: ... Esc drops focus, never the value.
- **Code**: Honoured on native (src/eQuantic.UI.Native.Components/PhotonHost.cs:1001-1003 `case "Escape": EndEditing(); return true;`) but absent on web: lowerTextEntry's only keydown handler is created when onSubmit is set and matches Enter alone. Since TextInput sets no onSubmit at all, a web TextInput has no keydown handler whatsoever and Escape does nothing.
- **Evidence**:

  ```
  input.events['keydown'] = ((e: KeyboardEvent) => { if (e.key === 'Enter') onSubmit(); }) as unknown as EventHandler;  // src/eQuantic.UI.Runtime/src/shared/lowering.ts:843-845
  ```

### B10 SearchField · semantics · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/SearchField.cs`
- **Handoff**: A11y: search-field role ... Semantics: role=searchbox
- **Code**: SearchField composes a plain TextEntry, and TextEntry has no way to say "search": the web lowering hardcodes the input type to password-or-text and never emits role=searchbox (src/eQuantic.UI.Runtime/src/shared/lowering.ts:820; identical in src/eQuantic.UI.Web/WebRealizer.cs:1042), so the pill announces as a generic textbox. The native side is the same — every TextEntry maps to SemanticRole.TextField (src/eQuantic.UI.Native.Components/Semantics.cs:115-117).
- **Evidence**:

  ```
  input.attributes['type'] = node.obscure === true ? 'password' : 'text';  // lowering.ts:820 — no 'search', and no role attribute anywhere in lowerTextEntry
  ```

### B10 SearchField · behaviour · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/SearchField.cs`
- **Handoff**: Esc clears the query first; a second Esc blurs · Enter submits · ↓ moves into the suggestion list where present.
- **Code**: The two-step Escape is implemented nowhere. SearchField wires only OnSubmit (SearchField.cs:44); the web entry has no Escape branch (src/eQuantic.UI.Runtime/src/shared/lowering.ts:843-845 matches 'Enter' only), and native Escape blurs immediately on the FIRST press without clearing (src/eQuantic.UI.Native.Components/PhotonHost.cs:1001-1003) — the opposite order to the spec. Enter→onSubmit is correct.
- **Evidence**:

  ```
  OnSubmit = OnSubmit,  // SearchField.cs:44 — the only key contract the component declares; no Escape hook exists on TextEntry
  ```

### B11 Checkbox · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/Checkbox.cs`
- **Handoff**: "Tab stop · Space toggles (Enter does not) · mixed → checked → unchecked."
- **Code**: The pressable lowers to a real `<button>` (the child Row holds no interactive, so `wrapping` is false) carrying role=checkbox and a click handler. A native button fires click on BOTH Space and Enter, and neither realizer installs a keydown filter, so Enter toggles the checkbox. WebRealizer.cs:1786 + WebRealizer.cs:1895-1904; TS twin lowering.ts:2053 + 2096-2102.
- **Evidence**:

  ```
  WebRealizer.cs:1786 — `var element = new RealizedElement(wrapping ? "span" : "button")`; WebRealizer.cs:1897 — `element.Role = pressable.Role == PressableRole.Switch ? "switch" : "checkbox";` (no keydown handling anywhere in LowerPressable)
  ```

### B11 Checkbox · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/Checkbox.cs`
- **Handoff**: "Hover = SurfaceSubtle wash over the hit area (§10)"
- **Code**: Neither the box nor the row declares the `Hover` StyleDiff that BoxStyle exposes for exactly this (VisualNode.cs:222 `public StyleDiff? Hover { get; init; }`), and there is no framework-wide hover rule for pressables — TokenCss.cs emits .eq-pressable rules for :active and :focus-visible only. Other components do use the mechanism (Accordion.cs:76), so the wash is a per-component opt-in that Checkbox never takes. Checkbox.cs:50-58.
- **Evidence**:

  ```
  Checkbox.cs:50-58 — `var box = new Box(new BoxStyle { Width = …, Height = …, Background = …, CornerRadius = …, BorderWidth = …, BorderColor = borderColor, }, boxContent);` (no Hover)  vs  Accordion.cs:76 — `Hover = new StyleDiff { Background = theme.SurfaceSubtle },`
  ```

### B11 Checkbox · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/Checkbox.cs`
- **Handoff**: "press-down tints the box border Primary instantly"
- **Code**: No pressed feedback of any kind is wired: the Pressable is built without `PressedBackground`, and the box's BorderColor is a pure function of Error (Checkbox.cs:43) — nothing swaps it on press. The mechanism exists and is used elsewhere (Chip.cs:95, BottomNavigation.cs:86 set PressedBackground; the CSS swap lives at TokenCss.cs:319). The doc comment's v1 fence (Checkbox.cs:10-11) covers only the scale-pop motion, not the press tint.
- **Evidence**:

  ```
  Checkbox.cs:68-75 — `return new Pressable(row, Disabled ? null : OnChanged) { Disabled = Disabled, Role = PressableRole.Checkbox, Selected = Checked, Mixed = Indeterminate, Label = Label, };` (no PressedBackground)
  ```

### B11 Checkbox · semantics · **unverified**

- **Component**: `src/eQuantic.UI.Components/Checkbox.cs`
- **Handoff**: "A11y: checkbox role … error appended to description."
- **Code**: `Error` changes the border colour and nothing else — it never reaches the accessibility tree. The Pressable carries no description/invalid slot and none is emitted: WebRealizer.cs:1777-1908 writes aria-label / aria-checked / aria-pressed / aria-expanded / aria-current only, so a checkbox in error announces identically to one that is not.
- **Evidence**:

  ```
  Checkbox.cs:43 — `var borderColor = Error ? theme.Colors(Variant.Destructive).Base : theme.BorderStrong;` (the only use of Error in the file)
  ```

### B11 Checkbox · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/Checkbox.cs`
- **Handoff**: "inline links inside the label keep their own hit rects" (the specimen's "I agree to the / terms of service")
- **Code**: The label slot is a plain string rendered as one Text node, so a label cannot contain an inline link at all — there is no rich-label/child slot on the component. Checkbox.cs:24 + Checkbox.cs:62-63.
- **Evidence**:

  ```
  Checkbox.cs:24 — `public string? Label { get; init; }`; Checkbox.cs:63 — `row.Add(new Text(label, TypeRole.BodyM, Disabled ? theme.TextMuted : theme.TextPrimary, maxLines: 2));`
  ```

### B12 Switch · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/Switch.cs`
- **Handoff**: "Keys — Tab stop · Space toggles · ←/→ set off/on explicitly."
- **Code**: The switch is a bare Pressable with no key handling: it lowers to a `<button role="switch">` whose only handler is click, so Tab and Space work and the arrows do nothing. Nothing wraps it in an Adjustable (the node that owns arrow dispatch, VisualNode.cs:1006), and even that node maps arrows to a ±1 nudge rather than to explicit off/on. Switch.cs:81-87.
- **Evidence**:

  ```
  Switch.cs:81 — `return new Pressable(stack, Disabled ? null : OnChanged)` (no Adjustable, no Shortcut, no keydown)
  ```

### B12 Switch · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/Switch.cs`
- **Handoff**: "press-down stretches the thumb to 30dp wide (rrect, engine-honest)"
- **Code**: The thumb is a fixed square: width and height both come from Sizing.SwitchThumb(density) (26 Comfortable / 20 Compact) and no press state touches them — the Pressable carries no PressedBackground and there is no pressed-size path. Unlike the slide/crossfade, this is NOT named in the component's fence (Switch.cs:12 fences only the two-end motion). Switch.cs:54-61.
- **Evidence**:

  ```
  Switch.cs:56-57 — `Width = Sizing.SwitchThumb(density),` / `Height = Sizing.SwitchThumb(density),`
  ```

### B12 Switch · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/Switch.cs`
- **Handoff**: "Hover = SurfaceSubtle wash over the hit area (§10)"
- **Code**: Neither the track nor the thumb declares BoxStyle.Hover (VisualNode.cs:222), and the framework has no blanket hover rule for pressables — TokenCss.cs:317-332 emits :active and :focus-visible only. Switch.cs:46-61.
- **Evidence**:

  ```
  Switch.cs:46-52 — `var track = new Box(new BoxStyle { Width = …, Height = …, Background = trackFill, CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)), });` (no Hover)
  ```

### B13 RadioGroup · semantics · **unverified**

- **Component**: `src/eQuantic.UI.Components/RadioGroup.cs`
- **Handoff**: "radiogroup / radio + aria-checked; the group is named by its label." and "single focus stop for the group"
- **Code**: The radiogroup wrapper is conditional. When the group is Disabled, or OnChanged is null, or Options is empty, the Adjustable is never built: the tree is a bare Column of `role="radio"` buttons with `tabindex="-1"` (WebRealizer.cs:1848-1854) and no owning `role="radiogroup"` and no aria-label — orphan radios, no group name, and no focus stop at all, because the rows have deliberately left the tab order. The visible Label Text is a sibling and is not programmatically associated. RadioGroup.cs:85-95.
- **Evidence**:

  ```
  RadioGroup.cs:86 — `if (!Disabled && OnChanged is not null && Options.Count > 0)` guarding `group = new Adjustable(options, …) { Role = AdjustableRole.Radiogroup, Label = Label, }`
  ```

### B13 RadioGroup · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/RadioGroup.cs`
- **Handoff**: "Row hover = SurfaceSubtle wash (§10); cursor pointer; the label row toggles."
- **Code**: Neither the row nor the circle declares BoxStyle.Hover (VisualNode.cs:222), and there is no framework-level hover wash for pressables (TokenCss.cs:317-332 covers :active and :focus-visible only). The pointer cursor IS set (WebRealizer.cs:1795). RadioGroup.cs:56-65.
- **Evidence**:

  ```
  RadioGroup.cs:65 — `var row = new Row(gap: Space.S3) { Cross = CrossAlign.Center, Width = SizeValue.Fill, Height = 44 };` (no Hover; the circle Box at :56-63 has none either)
  ```

### B14 ProgressBar · missing-feature · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/ProgressBar.cs`
- **Handoff**: determinate announces at 25% steps + completion; indeterminate announces "in progress" once, not per frame.
- **Code**: No announcement path exists. AdoptConfig (ProgressBar.cs:41-48) only records the snap flag and copies Value/Variant; nothing crosses a 25% threshold or fires a live-region update, and there is no live-region node in Primitives/Nodes at all (the only aria-live in the write-once path is the TextInput description, WebRealizer.cs:1063).
- **Evidence**:

  ```
  ProgressBar.cs:45  _snapNext = fresh.Value is { } incoming && Value is { } current && incoming < current;
  ProgressBar.cs:46  Value = fresh.Value;
  ```

### B15 Spinner · missing-feature · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Primitives/Nodes/Spinner.cs`
- **Handoff**: Hidden from the tree — the OWNING region announces busy, never the spinner itself ... inside Button the host announces busy (A12).
- **Code**: The spinner half is correct (aria-hidden="true", WebRealizer.cs:677 / lowering.ts:1317), but no owner ever announces. Button.Loading only swaps the leading icon for a Spinner and dims the tokens — it sets no busy state, and HtmlElement.AriaBusy (HtmlElement.cs:73) is never assigned by any realizer or component in the write-once path. Net effect: a loading Button says nothing at all to a screen reader.
- **Evidence**:

  ```
  Button.cs:93  if (Loading) content.Add(new Spinner(iconSize, textColor));
  ```

### B16 Skeleton · behaviour · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/EmptyState.cs`
- **Handoff**: All shapes share one 1.4s phase (single global clock — no sparkle chaos).
- **Code**: Native does share a global clock (PhotonRealizer.cs:1114, `motion.TimeMs % loop.DurationMs / loop.DurationMs`). Web does not: LowerLoopMotion emits a plain CSS animation with no animation-delay, so each element's phase starts at its own mount time. Skeletons for a region that begins loading later shimmer out of phase with the ones already on screen — the sparkle chaos the rule forbids — and web disagrees with native.
- **Evidence**:

  ```
  WebRealizer.cs:1440  Animation = $"eq-slide-x {motion.DurationMs}ms linear infinite",
  ```

### B16 Skeleton · missing-feature · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/EmptyState.cs`
- **Handoff**: Shapes: Line (12dp, Radius.Full, widths 100/70/45%)
- **Code**: Width is a `float` dp, not a SizeValue, and is passed straight into BoxStyle.Width — so a Line cannot be asked to fill its container (100%) or take a fraction of it. Every skeleton line must be a hardcoded dp, which then cannot mirror a responsive real layout (the same block's "Mirror the real layout's dimensions — content must replace skeleton with zero shift").
- **Evidence**:

  ```
  EmptyState.cs:91  public Skeleton(SkeletonShape shape, float width, float height = 0)
  EmptyState.cs:99  public float Width { get; init; }
  ```

### B17 EmptyState · missing-feature · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/EmptyState.cs`
- **Handoff**: illustration slot is an Image (bitmap atlas), optional.
- **Code**: There is no illustration slot. The only visual input is `Icons icon`, always rendered as a 32dp glyph inside the 64dp well; the class exposes Icon/Title/Body/Action/SecondaryAction and nothing that accepts an Image. The UI factory (UI.cs:324) mirrors the same three parameters.
- **Evidence**:

  ```
  EmptyState.cs:13  public EmptyState(Icons icon, string title, string? body = null)
  EmptyState.cs:30  var wellContent = new Icon(Icon, IconSize.Lg, theme.TextMuted).Centered();
  ```

### B17 EmptyState · behaviour · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/EmptyState.cs`
- **Handoff**: centers in the space the missing content would occupy — never floats mid-scroll.
- **Code**: The Column sets Width = Fill and Cross = Center (horizontal centring only). Height is left at its default and Main at its default MainAlign.Start (FlexNode, VisualNode.cs:1400), so the block hugs its content at the TOP of the empty region instead of centring in it. Only the S12 padding pushes it down.
- **Evidence**:

  ```
  EmptyState.cs:39-44  var column = new Column(gap: 0) { Width = SizeValue.Fill, Cross = CrossAlign.Center, Padding = EdgeInsets.Symmetric(Space.S4, Space.S12), };
  ```

### B17 EmptyState · missing-feature · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/EmptyState.cs`
- **Handoff**: A11y: reader focus moves to the title when a list becomes empty; action is next in order.
- **Code**: The title is a plain Text node with no focus marker, and the vocabulary has no way to express one: Autofocus exists only on TextEntry/CodeSurface (VisualNode.cs:942, 1912), which ListDetail.cs:30 states outright — "vocabulary has no target-neutral 'focus this subtree' yet". Focus stays wherever it was when the list emptied.
- **Evidence**:

  ```
  EmptyState.cs:47  column.Add(new Text(Title, TypeRole.Title, theme.TextPrimary, maxLines: 2)
  ```

### B18 Banner · metric · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/Banner.cs`
- **Handoff**: Dismiss X only for non-critical banners (glyph 18, hit 48).
- **Code**: The close glyph is IconSize.Dense = 20 (Tokens.cs:48), not 18. The component's own doc comment restates the spec as "20dp close, 48dp hit", so the 18 was lost rather than deliberately overridden — note 18 is not on the §07 icon whitelist (16/20/24/32), so either the handoff figure or the whitelist has to give.
- **Evidence**:

  ```
  Banner.cs:69  content.Add(new Pressable(new Icon(Icons.Close, IconSize.Dense, tint.OnSubtle), OnDismiss)
  Banner.cs:28  /// <summary>Dismiss affordance (spec: 20dp close, 48dp hit) ...
  ```

### B18 Banner · behaviour · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/Banner.cs`
- **Handoff**: Dismiss X ... (glyph 18, hit 48). Actions: ≤ 2 text buttons ... hit 48.
- **Code**: The dismiss is a bare Pressable around a 20dp Icon, relying on Pressable's documented guarantee ("the hit rect is expanded symmetrically to at least 48×48dp", VisualNode.cs:1062-1066). Photon honours it (PhotonRealizer.ExpandHitRect, line 1658-1665), but the WEB realizer never does: LowerPressable emits a <button> with padding 0 and no min-width/min-height, and no .eq-pressable rule in TokenCss sets one. On the web the X is a 20×20 target, not 48.
- **Evidence**:

  ```
  WebRealizer.cs:1786-1800  var element = new RealizedElement(wrapping ? "span" : "button") { Style = new HtmlStyle { Padding = "0", Border = "none", Background = "none", FontFamily = "inherit", Cursor = ..., TextAlign = TextAlign.Start, Width = fills.Width ? "100%" : null, Height = fills.Height ? "100%" : null, } };
  ```

### B18 Banner · behaviour · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/Banner.cs`
- **Handoff**: Actions: ≤ 2 text buttons in OnSubtle (primary 700, secondary 600 @ 75%).
- **Code**: The two action Buttons are added to the row verbatim — the Banner never applies the status tint or the 700/600@75% weights, so an action lands in whatever Variant colour the caller built it with (Primary fill on a Warning banner, for instance). The property doc asserts the opposite of what the code does: "rendered as Link buttons in the status tint".
- **Evidence**:

  ```
  Banner.cs:24  /// <summary>Up to two text actions (spec) — rendered as Link buttons in the status tint.</summary>
  Banner.cs:58-59  if (PrimaryAction != null) actions.Add(PrimaryAction);
                    if (SecondaryAction != null) actions.Add(SecondaryAction);
  ```

### B18 Banner · missing-feature · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/Banner.cs`
- **Handoff**: Enter: fade + height Slow 300ms decelerate (layout animates once — allowed exception); exit ⅔ accelerate.
- **Code**: Build wraps nothing in a Presence node (the vocabulary's enter/exit motion, VisualNode.cs 'presence' NodeKind, realized at WebRealizer.LowerPresence) — the tree is Box > Row > Column with no motion at all. The banner pops in and out instantly, and there is no fade or height animation on either realizer.
- **Evidence**:

  ```
  Banner.cs:64  var content = new Row(gap: 10) { Cross = CrossAlign.Start };
  Banner.cs:75  return new Box(new BoxStyle { ... }, content);
  ```

### C1 BottomSheet · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/BottomSheet.cs`
- **Handoff**: grabber 36×4 BorderStrong Full
- **Code**: The grabber is 32×4, not 36×4 (BottomSheet.cs:48). Colour (theme.BorderStrong) and Radius.Full match. The doc comment restates the wrong figure ("a 32×4 drag handle", BottomSheet.cs:8) without naming it as a deviation, so this is a plain drift, not a documented one.
- **Evidence**:

  ```
  BottomSheet.cs:48            Width = 32, Height = 4,
  ```

### C1 BottomSheet · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/BottomSheet.cs`
- **Handoff**: grabber ... 8dp from top · content gutter S5 20
- **Code**: The sheet's padding is EdgeInsets(Start, Top, End, Bottom) = (Space.S5=20, Space.S3=12, Space.S5=20, Space.S6=24) at BottomSheet.cs:62, and the grabber row is the first child of the padded body — so the grabber sits 12dp from the top, not 8dp. The 20dp side gutter is correct.
- **Evidence**:

  ```
  BottomSheet.cs:62            Padding = new EdgeInsets(Space.S5, Space.S3, Space.S5, Space.S6),
  ```

### C1 BottomSheet · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/BottomSheet.cs`
- **Handoff**: Fling down > 700dp/s dismisses ... release snaps to nearest detent by position + velocity
- **Code**: Dismissal is a pure DISTANCE threshold: DragDismiss.ThresholdDp = 96 (Primitives/Nodes/VisualNode.cs:816) — velocity is never read. The DragDismiss doc names the fence: "v1 fences: detents (partial heights), flick-velocity dismissal, horizontal axis, and nested-scroll interplay" (VisualNode.cs:808-809). Stated reason: gesture-system v1 scope. The paired "Release glide — 200ms" DOES match (glide back runs Motion.Base = 200).
- **Evidence**:

  ```
  VisualNode.cs:816    public const float ThresholdDp = 96;
  BottomSheet.cs:80        VisualNode sheetNode = Dismissible ? new DragDismiss(sheet, OnDismiss) : sheet;
  ```

### C1 BottomSheet · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/BottomSheet.cs`
- **Handoff**: Dialog role; focus trapped; first focus = title
- **Code**: Nothing in the sheet is marked InitialFocus, so focus lands on the Overlay container (tabindex=-1, WebRealizer.cs:991). The doc comment names and justifies it: "with nothing focusable in the sheet, focus lands on the sheet container itself, which is what §10 asks for" (BottomSheet.cs:10-12). Note the sheet also passes no Overlay.Label, so a reader hears "dialog" with no name — the title is inside Content, which the component never sees.
- **Evidence**:

  ```
  BottomSheet.cs:90        var layer = new Overlay(layers);
  ```

### C1 BottomSheet · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/BottomSheet.cs`
- **Handoff**: grabber exposes "expand / collapse / close" custom actions
- **Code**: The grabber is a bare Box with no Pressable, no Label and no custom actions (BottomSheet.cs:46-51) — it is decorative to assistive tech. "expand/collapse" presuppose the detents that do not exist, but "close" does not, and there is no keyboard/AT path to dismiss via the grabber (only Escape and the scrim).
- **Evidence**:

  ```
  BottomSheet.cs:46-51        handleRow.Add(new Box(new BoxStyle
          {
              Width = 32, Height = 4,
              Background = theme.BorderStrong,
              CornerRadius = new CornerRadii(Radius.Full),
          }));
  ```

### C1 BottomSheet · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/BottomSheet.cs`
- **Handoff**: bottom safe-area painted in sheet fill
- **Code**: The bottom inset is a hardcoded Space.S6 = 24dp of padding; the sheet never wraps its content in the SafeArea node that Primitives already ships (VisualNode.cs:1760, SafeEdges.Bottom), so on a device with a home indicator the sheet fill does not extend into the system inset and the content is not kept clear of it.
- **Evidence**:

  ```
  BottomSheet.cs:62            Padding = new EdgeInsets(Space.S5, Space.S3, Space.S5, Space.S6),
  ```

### C2 Modal · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/Dialog.cs`
- **Handoff**: Enter: scale 0.96→1 + fade, Base 200ms decelerate
- **Code**: The dialog enters with fade ONLY — no scale. Dialog.cs:120 wraps the layer in `new Presence(layers)` whose default is PresenceMotion.Fade, and the enum has exactly two members, Fade and SlideUp (VisualNode.cs:699-705): no scale motion exists to ask for. Duration is right (200ms, --eq-motion-base) but the curve lowers to CSS `ease-out` (0,0,0.58,1), not the spec's Decelerate (0,0,0,1) — TokenCss.cs:411.
- **Evidence**:

  ```
  Dialog.cs:120        var layer = new Overlay(new Presence(layers))
  VisualNode.cs:700-705    Fade = 0, ... SlideUp = 1,
  ```

### C2 Modal · semantics · **unverified**

- **Component**: `src/eQuantic.UI.Components/Dialog.cs`
- **Handoff**: A11y: alertdialog role — name = title, description = body
- **Code**: The title becomes the accessible NAME (Overlay.Label → aria-label, WebRealizer.cs:990), but nothing wires the body as the accessible DESCRIPTION: Overlay has no Description property, and LowerOverlay emits no aria-describedby (WebRealizer.cs:983-992). A reader announces the dialog's name and then must reach the body as ordinary content.
- **Evidence**:

  ```
  Dialog.cs:120-124        var layer = new Overlay(new Presence(layers))
          {
              Label = Title,
              Alert = Actions.Any(action => action.Variant == Variant.Destructive),
          };
  ```

### C2 Modal · semantics · **unverified**

- **Component**: `src/eQuantic.UI.Components/Dialog.cs`
- **Handoff**: Destructive announced "…, destructive"
- **Code**: No action label is ever suffixed. Dialog.cs:60 builds a plain Button with action.Label verbatim, and Button.cs:132 passes that same string as the Pressable's accessible name — Button.cs contains no occurrence of "destructive" at all. The only destructive signal is the layer's role escalation to alertdialog; the button itself sounds identical to a safe one.
- **Evidence**:

  ```
  Dialog.cs:60            actions.Add(new Button(action.Label, action.Variant, SizeVariant.Medium, action.OnPressed)
  Button.cs:132            Label = Label,
  ```

### C4 Toast · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/Toast.cs`
- **Handoff**: Position: bottom-centered, 16dp gutters, max-width 560
- **Code**: MaxWidth is 480, not 560 (Toast.cs:60). Bottom-centred (Main=End, Cross=Center) and the 16dp side gutter (EdgeInsets.Symmetric(Space.S4=16, Space.S6=24), Toast.cs:69) are correct.
- **Evidence**:

  ```
  Toast.cs:60            MaxWidth = 480,
  ```

### C4 Toast · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/Toast.cs`
- **Handoff**: text 14/500, ≤ 2 lines
- **Code**: The message uses TypeRole.BodyM = 15dp / FontWeight.Regular(400) (PhotonTheme.cs:118) — one step too large and two weight steps too light. maxLines: 2 matches. No role on the scale is 14/500, so this needs a StyleOverride the component does not set.
- **Evidence**:

  ```
  Toast.cs:42        row.Add(new Text(Message, TypeRole.BodyM, theme.TextInverse, maxLines: 2));
  ```

### C4 Toast · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/Toast.cs`
- **Handoff**: swipe down/sideways dismisses (velocity-driven) ... Esc dismisses the visible toast
- **Code**: There is no dismissal path of any kind: the ctor exposes no OnDismiss, the pill is not wrapped in DragDismiss (contrast BottomSheet.cs:80) and no Shortcut binds Escape (contrast BottomSheet.cs:91 / Dialog.cs:125). Unlike the timer, swipe-dismiss and Esc are NOT among the fences the doc comment names (it fences only "enter/exit motion ... and toast queueing", Toast.cs:12).
- **Evidence**:

  ```
  Toast.cs:72        anchor.Add(new Presence(pill, PresenceMotion.SlideUp));
  ```

### C4 Toast · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/Toast.cs`
- **Handoff**: enter rise+fade Slow 300ms decelerate; exit fade ⅔
- **Code**: PresenceMotion.SlideUp lowers to `.eq-presence-slideup { animation: ... var(--eq-motion-base) ease-out; }` — Motion.BaseMs = 200ms, not Slow 300ms, and CSS ease-out rather than the Decelerate curve (TokenCss.cs:412). The paired exit runs --eq-motion-fast = 100ms instead of ⅔ (TokenCss.cs:419). Motion.Enter = (SlowMs, Curve.Decelerate) already exists in Tokens.cs:261 and is not used by Presence. The rise distance (Presence.SlideDistance = 16dp) and the Reduce-Motion crossfade are correct.
- **Evidence**:

  ```
  TokenCss.cs:412        css.AppendLine(".eq-presence-slideup { animation: eq-presence-slideup var(--eq-motion-base) ease-out; }");
  ```

### C5 Drawer · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/Drawer.cs`
- **Handoff**: "both vertical safe-areas painted in panel fill"
- **Code**: The panel is a plain Box with uniform 16dp padding and no SafeArea node. Its fill does reach the edges (Height = Fill), but the content is never inset by the top/bottom system insets, so on a notched phone the header slot renders under the status bar and the footer under the home indicator.
- **Evidence**:

  ```
  Drawer.cs:55-64  var panel = new Box(new BoxStyle
          {
              Width = Width,
              Height = context.InFlow ? default : SizeValue.Fill,
              Background = theme.Surface,
              Elevation = 3,
  ```

### C5 Drawer · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/Drawer.cs`
- **Handoff**: "Semantics: Modal = role=dialog + aria-modal; pinned = navigation landmark." · "Expanded widths prefer the pinned drawer (or NavigationRail C16) over the modal one."
- **Code**: Only the modal form exists. The one non-overlay branch is InFlow, which the primitive's own doc defines as a documentation/preview mode ("A page that wants to SHOW one — documentation, a design review, a visual-regression suite"), not a pinned drawer: it returns a bare Box with no navigation landmark and drops the scrim and Escape binding.
- **Evidence**:

  ```
  Drawer.cs:66  if (context.InFlow) return panel;
  InFlow.cs:11-13  a design review, a visual-regression suite — wants the surface and none of that machinery
  ```

### C6 SegmentedControl · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/SegmentedControl.cs`
- **Handoff**: 4dp inset padding
- **Code**: SegmentedControl.cs:40 hardcodes the track inset at 3f. Note the block contradicts itself here: its own thumb figure "radius = track − inset (7)" only works with inset 3 (10 − 4 = 6), and the code matches the parenthetical rather than the stated inset.
- **Evidence**:

  ```
  SegmentedControl.cs:40  var inset = 3f;                                   // the track's lip around the thumb
  SegmentedControl.cs:78  CornerRadius = new CornerRadii(trackRadius - inset),   // 10 - 3 = 7
  ```

### C6 SegmentedControl · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/SegmentedControl.cs`
- **Handoff**: Hover on a non-selected segment = SurfaceSubtle wash (§10); cursor pointer; the thumb itself never hovers.
- **Code**: the segment Box (SegmentedControl.cs:72-81) sets no Hover diff, so there is no hover wash on any target. The mechanism exists (BoxStyle.Hover, VisualNode.cs:222) and eight sibling components use it — Pagination.cs:108 is the identical "wash the non-current item" case.
- **Evidence**:

  ```
  SegmentedControl.cs:72-81  var segment = new Box(new BoxStyle { Width = ..., Height = ..., Padding = ..., Background = selected ? theme.Surface : null, CornerRadius = ..., Elevation = ..., Transition = ... }, label);   // no Hover = ...
  Pagination.cs:108  Hover = current ? null : new StyleDiff { Background = theme.SurfaceSubtle },
  ```

### C6 SegmentedControl · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/SegmentedControl.cs`
- **Handoff**: press-down pre-tints the target label instantly
- **Code**: the segment Pressable (SegmentedControl.cs:83-89) sets no PressedBackground, so pressing shows nothing until the caller re-renders with the new SelectedIndex. Tabs.cs:65 — the sibling control built the same way — does set it.
- **Evidence**:

  ```
  SegmentedControl.cs:83  var press = new Pressable(segment, Disabled || selected ? null : () => OnChanged?.Invoke(index))
  SegmentedControl.cs:85-88  { Disabled = Disabled, Label = Segments[index], Role = PressableRole.Radio, Selected = selected, }   // no PressedBackground
  Tabs.cs:65  PressedBackground = theme.SurfaceSubtle,
  ```

### C6 SegmentedControl · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/SegmentedControl.cs`
- **Handoff**: dragging the thumb scrubs across segments with a haptic per boundary
- **Code**: Build composes only Pressables inside an Adjustable (SegmentedControl.cs:83-113) — no Draggable node anywhere in the component, so the only pointer gesture that changes the selection is a discrete tap. No haptic API exists in the framework either: a case-insensitive grep for "haptic" over src/ matches nothing outside node_modules.
- **Evidence**:

  ```
  SegmentedControl.cs:109  return new Adjustable(track,
  SegmentedControl.cs:110      direction => OnChanged.Invoke((SelectedIndex + direction + count) % count))
  SegmentedControl.cs:112      Role = AdjustableRole.Radiogroup,
  ```

### C6 SegmentedControl · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/SegmentedControl.cs`
- **Handoff**: icons-only segments allowed with labels for readers
- **Code**: a segment is a string and nothing else — the constructor and the Segments property take IReadOnlyList<string> (SegmentedControl.cs:18/26) and Build renders each through a single Text node (line 65). There is no icon slot, so an icon-only segment cannot be expressed.
- **Evidence**:

  ```
  SegmentedControl.cs:18  public SegmentedControl(IReadOnlyList<string> segments, int selectedIndex, Action<int>? onChanged = null)
  SegmentedControl.cs:26  public IReadOnlyList<string> Segments { get; init; }
  SegmentedControl.cs:65  label.Add(new Text(Segments[index], TypeRole.Label, ...
  ```

### C6 SegmentedControl · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/SegmentedControl.cs`
- **Handoff**: ONE tab stop, roving: ←/→ move AND select (wraps) · Home/End.
- **Code**: the one Tab stop and the wrapping arrows are correct (SegmentedControl.cs:109-113), but Home/End are handled on no target: the Adjustable keydown recognises only the four arrow keys on the web (lowering.ts:2387-2392), only the same four on native (PhotonHost.cs:1919), and the SSR realizer emits no key handler at all (WebRealizer.cs:1088-1111).
- **Evidence**:

  ```
  lowering.ts:2387  const direction = event.key === 'ArrowRight' ? 1
  lowering.ts:2388    : event.key === 'ArrowLeft' ? -1
  lowering.ts:2391    : 0;
  lowering.ts:2392  if (direction === 0) return;
  PhotonHost.cs:1919  && (key is "ArrowLeft" or "ArrowRight" or "ArrowUp" or "ArrowDown")
  ```

### C7 Slider · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/Slider.cs`
- **Handoff**: Thumb 24dp white + E2 + 1dp Border (all themes — white thumb is the cross-platform constant).
- **Code**: the thumb fill is theme.Surface (Slider.cs:57), which is the dark surface under a dark theme rather than the constant white the block calls cross-platform; and its border is 2dp of the accent colour (lines 60-61) rather than 1dp of the Border token. Elevation 2 (E2) is correct.
- **Evidence**:

  ```
  Slider.cs:57  Background = theme.Surface,
  Slider.cs:60  BorderWidth = 2,
  Slider.cs:61  BorderColor = fill,
  Slider.cs:62  Elevation = 2,
  ```

### C7 Slider · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/Slider.cs`
- **Handoff**: ←/↓ −1 step · →/↑ +1 · PgUp/PgDn ±10% · Home/End min/max.
- **Code**: the four arrows are wired correctly (the web lowering even splits ↑/↓ by role so a slider's up increases), but PgUp/PgDn and Home/End are handled nowhere — the web keydown returns early on any other key (lowering.ts:2392) and PhotonHost.cs:1919 gates on the same four names.
- **Evidence**:

  ```
  lowering.ts:2389  : event.key === 'ArrowUp' ? (downIsNext ? -1 : 1)
  lowering.ts:2392  if (direction === 0) return;
  PhotonHost.cs:1919  && (key is "ArrowLeft" or "ArrowRight" or "ArrowUp" or "ArrowDown")
  ```

### C7 Slider · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/Slider.cs`
- **Handoff**: thumb grows to 28 while held ... Thumb hover grows it 24→26 and shows the value bubble (Motion.Press)
- **Code**: the thumb is a fixed ThumbSize square with a Colors-only transition and no Hover diff (Slider.cs:54-64), so it grows neither on hover nor while held — size is not even in the animated channel set.
- **Evidence**:

  ```
  Slider.cs:55-56  Width = ThumbSize,  Height = ThumbSize,
  Slider.cs:63  Transition = TransitionSpec.Of(StyleChannels.Colors, Motion.Press),
  ```

### C7 Slider · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/Slider.cs`
- **Handoff**: new Slider(value, onChanged, min: 0, max: 100, step: null, haptics: true) ... + haptic per detent; min/max edges get a firmer haptic.
- **Code**: there is no haptics parameter and no haptic feedback anywhere in the framework — the constructor takes only value and onChanged (Slider.cs:24-28), and a case-insensitive grep for "haptic" across src/ matches nothing outside node_modules.
- **Evidence**:

  ```
  Slider.cs:24  public Slider(float value, Action<float>? onChanged = null)
  Slider.cs:26-27  Value = value;  OnChanged = onChanged;
  ```

### C8 Stepper · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/Stepper.cs`
- **Handoff**: glyphs 18
- **Code**: the arm glyph is IconSize.Dense, which Tokens.cs:48 defines as 20 — there is no 18 rung on the icon scale (16 / 20 / 24 / 32).
- **Evidence**:

  ```
  Stepper.cs:90  centered.Add(new Icon(glyph, IconSize.Dense, enabled ? theme.TextPrimary : theme.TextMuted));
  Tokens.cs:48  public const float Dense = 20;
  ```

### C8 Stepper · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/Stepper.cs`
- **Handoff**: Press: cell fills SurfaceSubtle instantly; value crossfades 100ms.
- **Code**: the arm Pressable sets no PressedBackground (Stepper.cs:94), so the cell shows no pressed fill — seven sibling components do set it, and Tabs.cs:65 / ListItem.cs:124 use exactly theme.SurfaceSubtle. The value Text (lines 55-61) carries no Transition either, so it swaps with no crossfade.
- **Evidence**:

  ```
  Stepper.cs:94  return new Pressable(box, enabled ? onPressed : null) { Disabled = !enabled, Label = label };
  Stepper.cs:55-61  reading.Add(new Text($"{Value}{Suffix}", TypeRole.Label, ...) { Align = ..., Tabular = true, StyleOverride = ... });   // no Transition
  ```

### C8 Stepper · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/Stepper.cs`
- **Handoff**: At bounds the button disables (38%) — value never wraps around.
- **Code**: the never-wraps half is right (Stepper.cs:40-41 gate on Min/Max and drop the callback), but a bound-disabled arm is NOT dimmed to 38%: it only swaps the glyph colour to TextMuted and sets no Opacity (lines 90 and 92). theme.DisabledOpacity (0.38f, PhotonTheme.cs:33) is applied only when the WHOLE control is Disabled, on the frame at line 75.
- **Evidence**:

  ```
  Stepper.cs:90  centered.Add(new Icon(glyph, IconSize.Dense, enabled ? theme.TextPrimary : theme.TextMuted));
  Stepper.cs:92  var box = new Box(new BoxStyle { Width = height, Height = SizeValue.Fill }, centered);   // no Opacity
  Stepper.cs:75  Opacity = Disabled ? theme.DisabledOpacity : 1f,
  ```

### C9 PullToRefresh · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/PullToRefresh.cs`
- **Handoff**: Spinner Md 24 (Primary)
- **Code**: The spinner is built at IconSize.Dense = 20 (Tokens.cs:48), one rung below the spec'd Md = 24 (Tokens.cs:50).
- **Evidence**:

  ```
  indicator.Add(new Spinner(IconSize.Dense, theme.Colors(Variant.Primary).Base));
  ```

### C9 PullToRefresh · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/PullToRefresh.cs`
- **Handoff**: Pull maps with 0.5 asymptotic resistance
- **Code**: The Draggable follows the finger 1:1 and is simply clamped between 0 and Threshold (PullToRefresh.cs:56-58); the web controller confirms the mapping is linear — `const at = rest + raw; return Math.min(max, Math.max(min, at));` (src/eQuantic.UI.Runtime/src/dom/draggable.ts:55-56). There is no resistance term and no property to express one.
- **Evidence**:

  ```
  Axis = DragAxis.Vertical,
  Min = 0,
  Max = Threshold,
  ```

### C9 PullToRefresh · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/PullToRefresh.cs`
- **Handoff**: While pulling, spinner bars fill clockwise with pull fraction (deterministic — the user can feel the threshold); past threshold it starts spinning.
- **Code**: The Draggable never sets OnMoved, so no pull fraction ever reaches the component (PullToRefresh.cs:53-62), and Spinner has no progress/fraction input at all — its only inputs are Size and Color (src/eQuantic.UI.Primitives/Nodes/Spinner.cs:23-35). The indicator is a plain indeterminate spinner from the first pixel of pull.
- **Evidence**:

  ```
  indicator.Add(new Spinner(IconSize.Dense, theme.Colors(Variant.Primary).Base));
  ```

### C9 PullToRefresh · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/PullToRefresh.cs`
- **Handoff**: new PullToRefresh(onRefresh: async fn, ...) ... onRefresh runs, min visible 500ms, collapses Slow decelerate.
- **Code**: OnRefresh is a synchronous Action with no completion signal (PullToRefresh.cs:28), invoked fire-and-forget on release (PullToRefresh.cs:69). The component therefore cannot enforce the 500ms minimum visible time — collapse is entirely the caller's Refreshing flag (PullToRefresh.cs:61).
- **Evidence**:

  ```
  private void OnReleased(float offset)
  {
      if (offset >= Threshold) OnRefresh?.Invoke();
  }
  ```

### C10 SwipeableRow · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/SwipeableRow.cs`
- **Handoff**: status Base fill + OnBase icon Dense + Caption 10.5/700
- **Code**: The action label uses TypeRole.Caption, which the theme resolves to 12dp / FontWeight.Medium (500) (src/eQuantic.UI.Primitives/Theme/PhotonTheme.cs:120), not 10.5/700. The Base fill and the OnBase Dense icon do match (lines 56, 63).
- **Evidence**:

  ```
  actionContent.Add(new Text(ActionLabel, TypeRole.Caption, colors.OnBase, maxLines: 1));
  ```

### C10 SwipeableRow · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/SwipeableRow.cs`
- **Handoff**: Destructive commit: row height collapses Base + haptic; Undo offered via Toast (C4).
- **Code**: Pressing the action calls OnAction straight through (SwipeableRow.cs:59-64) — no collapse animation, no Toast, and no haptic anywhere in the framework (grep for `haptic` across src returns nothing). ActionVariant defaults to Destructive (line 39), so this is the default path.
- **Evidence**:

  ```
  var action = new Pressable(new Box(new BoxStyle
  {
      Width = ActionWidth,
      Height = SizeValue.Fill,
      Background = colors.Base,
  }, actionContent), OnAction)
  ```

### C10 SwipeableRow · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/SwipeableRow.cs`
- **Handoff**: Focused row: menu key / Shift+F10 opens the actions; destructive commit stays two-step.
- **Code**: The action is a single Pressable whose one press fires OnAction (SwipeableRow.cs:59-64); there is no confirmation step, no Disabled/armed intermediate state, and no key handling on the row.
- **Evidence**:

  ```
  }, actionContent), OnAction)
  {
      Label = ActionLabel,
  };
  ```

### C10 SwipeableRow · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/SwipeableRow.cs`
- **Handoff**: Mouse drags don’t swipe — on pointer the row’s actions surface via right-click context menu (the C3 presentation) plus the row’s own visible affordances.
- **Code**: Build emits only Box > Stack > (Positioned action + Draggable surface) (SwipeableRow.cs:76-86) — no Anchored/menu node and no context-menu hook. On pointer the web Draggable controller arms on plain pointermove past the slop (src/eQuantic.UI.Runtime/src/dom/draggable.ts:61), so a mouse drag DOES swipe the row, which the handoff forbids.
- **Evidence**:

  ```
  var revealed = new Stack();
  revealed.Add(new Positioned(action, top: 0, bottom: 0, end: 0));
  revealed.Add(new Draggable(surface, OnReleased)
  ```

### C11 Accordion · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/Accordion.cs`
- **Handoff**: chevron Dense 20 TextMuted
- **Code**: The chevron is drawn at IconSize.Sm = 16 (src/eQuantic.UI.Primitives/Theme/Tokens.cs:45) in theme.TextSecondary. Both figures are off: Dense is 20 (Tokens.cs:48), and the theme exposes a distinct TextMuted token (src/eQuantic.UI.Primitives/Theme/IAppTheme.cs:70) that is not the one used.
- **Evidence**:

  ```
  header.Add(new Icon(open ? Icons.ChevronUp : Icons.ChevronDown, IconSize.Sm, theme.TextSecondary));
  ```

### C11 Accordion · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/Accordion.cs`
- **Handoff**: chevron ... rotating 180°, Base 200ms standard
- **Code**: There is no rotation and no transition: the component swaps one glyph for another, so the chevron cuts instantly from ChevronDown to ChevronUp. Rotation is expressible in the vocabulary — Transform2D.Rotate (src/eQuantic.UI.Primitives/Nodes/VisualNode.cs:343) animated over StyleChannels.Transform (VisualNode.cs:291) — it is simply not used here.
- **Evidence**:

  ```
  header.Add(new Icon(open ? Icons.ChevronUp : Icons.ChevronDown, IconSize.Sm, theme.TextSecondary));
  ```

### C11 Accordion · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/Accordion.cs`
- **Handoff**: Pressed header = SurfaceSubtle instant.
- **Code**: SurfaceSubtle is wired to HOVER only (Accordion.cs:76), which satisfies the pointer-tier rule but leaves the pressed state unpainted: Pressable.PressedBackground (VisualNode.cs:1085) is never set, so a touch press shows no feedback at all.
- **Evidence**:

  ```
  Hover = new StyleDiff { Background = theme.SurfaceSubtle },
  ```

### C12 PageIndicator · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/PageIndicator.cs`
- **Handoff**: Over media: dots sit in a Scrim-fill pill (padding 4/10) with white dots — never bare on photos.
- **Code**: There is no over-media variant. Build returns the bare Row of dots with no wrapping pill, no scrim fill and no white override, and the component exposes no property to ask for one (PageIndicator.cs:34-75) — its only style input is Variant (line 32).
- **Evidence**:

  ```
  return row;
  ```

### C12 PageIndicator · semantics · **unverified**

- **Component**: `src/eQuantic.UI.Components/PageIndicator.cs`
- **Handoff**: ≤ 8 pages; beyond, swap to a Caption tnum counter "7 / 12".
- **Code**: The threshold and the Caption tnum treatment are right (MaxDots = 8, line 18; Tabular = true, line 44), but the string is the sentence "Page 8 of 12" rather than the counter "7 / 12" — the same label built for the accessible name is reused as the visible readout (lines 38, 42). The behaviour is pinned by a test: `text.Content.Should().Be($"Page 4 of {PageIndicator.MaxDots + 1}")` (tests/eQuantic.UI.Native.Engine.Tests/GalleryComponentTests.cs:145).
- **Evidence**:

  ```
  var label = $"Page {CurrentIndex + 1} of {Count}";
  ```

### C13 Tooltip · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/Tooltip.cs`
- **Handoff**: "padding 6/10" (6dp vertical, 10dp horizontal)
- **Code**: EdgeInsets.Symmetric takes (horizontal, vertical), so Symmetric(Space.S2, Space.S1) resolves to 8dp horizontal and 4dp vertical — the pill is 2dp tighter on each axis than specified.
- **Evidence**:

  ```
  Tooltip.cs:38  Padding = EdgeInsets.Symmetric(Space.S2, Space.S1),
  LayoutTypes.cs:12  public static EdgeInsets Symmetric(float horizontal, float vertical) =>
  Tokens.cs:11-12  public const float S1 = 4;
      public const float S2 = 8;
  ```

### C13 Tooltip · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/Tooltip.cs`
- **Handoff**: "max-width 200, ≤ 2 lines."
- **Code**: The Text is capped at ONE line and the pill sets no MaxWidth, so it inherits the generic anchor-panel cap of min(92vw, 420px). A two-line hint is impossible and a long hint runs to 420dp instead of wrapping at 200.
- **Evidence**:

  ```
  Tooltip.cs:39  }, new Text(Text, TypeRole.Caption, theme.TextInverse, maxLines: 1));
  TokenCss.cs:353  css.AppendLine(".eq-anchor-panel { position: absolute; z-index: 1050; width: max-content; max-width: min(92vw, 420px); }");
  ```

### C13 Tooltip · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/Tooltip.cs`
- **Handoff**: "bubble 8dp above, no caret"
- **Code**: Tooltip never sets Gap, so it takes Anchored.DefaultGap = 4dp — the bubble sits half the specified distance from the glyph.
- **Evidence**:

  ```
  Tooltip.cs:41-46  return new Anchored(Child, pill)
          {
              Placement = Placement,
              OpenOnHover = true,
              DescribesAnchor = true,
          };
  ```

### C14 Select · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/Select.cs`
- **Handoff**: "Trigger inherits TextInput (B9) anatomy/states"
- **Code**: The trigger has a Hover diff but no focused state at all — Select holds no _focused flag, so it never grows the 2dp Primary border that B9 defines ("2dp Primary when focused, padding compensates -1dp", TextInput.cs:85-86). A keyboard user tabbing onto the closed Select sees no field-level focus treatment.
- **Evidence**:

  ```
  Select.cs:70-72  BorderColor = theme.BorderStrong,
              Opacity = Disabled ? theme.DisabledOpacity : null,
              Hover = Disabled ? null : new StyleDiff { BorderColor = theme.Colors(Variant.Primary).Base },
  ```

### C16 NavigationRail · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/NavigationRail.cs`
- **Handoff**: "A11y · usage: Navigation landmark \"primary\"; items announce label + 'current' when selected." and "Semantics: Navigation landmark + aria-current=\"page\"."
- **Code**: The rail's root is a plain Box (NavigationRail.cs:144-156) that lowers to a bare <div>: no landmark role, no accessible name. Only the per-item half of the contract exists (Role = PressableRole.Destination at line 126 → aria-current="page" in lowering.ts:2107). There is no landmark vocabulary anywhere in the framework — grep for landmark/nav roles across src/eQuantic.UI.Primitives, src/eQuantic.UI.Web/WebRealizer.cs and Runtime lowering.ts returns nothing but a prose FENCE note in ListItem.cs:53. So a screen-reader user gets four buttons in the page body, with no "navigation" region to jump to.
- **Evidence**:

  ```
  return new Box(new BoxStyle
  {
      Width = 80,
      Height = SizeValue.Fill,
      Padding = EdgeInsets.Symmetric(0, Space.S3),
      Background = theme.Surface,
  ```

### C16 NavigationRail · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/NavigationRail.cs`
- **Handoff**: "Selection: pill crossfade + glyph fill swap, Motion.State 200ms."
- **Code**: The pill's BoxStyle declares no Transition, so the Primary-subtle background and the tint snap between destinations instead of crossfading. The vocabulary exists and is used by siblings — `Transition = TransitionSpec.Of(StyleChannels.Colors | StyleChannels.Size, Motion.State)` in PageIndicator.cs:62 — and Motion.State resolves to exactly the handoff's 200ms (Tokens.cs:241 BaseMs = 200, line 258 State = new(BaseMs, Curve.Standard)). The glyph fill swap itself is implemented (line 85 picks SelectedIcon), only its 200ms motion is absent.
- **Evidence**:

  ```
  var pill = new Box(new BoxStyle
  {
      Width = 52,
      Height = 30,
      Background = isActive ? primary.Subtle : null,
      CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
  ```

### C16 NavigationRail · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/NavigationRail.cs`
- **Handoff**: "selected = PrimarySubtle pill + filled glyph + label 700; others TextSecondary."
- **Code**: Unselected glyph and label are painted with theme.TextMuted, not theme.TextSecondary. They are distinct tokens in the theme: TextSecondary = #4B5563/#AEB7C2, TextMuted = #5F6B7A/#8B95A3 (PhotonTheme.cs:24-25), so the inactive destinations render one tier lighter than C16 asks for. Caveat worth reviewing: the same block also says "same selection vocabulary as B4", and B4-BottomNavigation.txt line 20 says "Inactive: TextMuted" — the handoff contradicts itself here, and the code follows B4's wording.
- **Evidence**:

  ```
  var tint = isActive ? primary.OnSubtle : theme.TextMuted;
  ```

## Notes and documented deviations

85 rows.

### A1 Box · semantics · **unverified**

- **Component**: `src/eQuantic.UI.Primitives/Nodes/VisualNode.cs`
- **Handoff**: Shadow — Elevation(0–5) token only — free-form ShadowSpec requires design review.
- **Code**: BoxStyle exposes a free-form ShadowSpec (VisualNode.cs:178), a LIST of them (185) and an InsetHighlight (192); the web realizer joins elevation + shadow + list + inset into one box-shadow (WebRealizer.cs:1259-1267) and Photon issues one ShadowRRect per entry (PhotonRealizer.cs:598-608). This also contradicts the framework's own ShadowSpec doc in Tokens.cs:209-211, which calls stacked shadows a spec violation.
- **Evidence**:

  ```
  public IReadOnlyList<ShadowSpec>? Shadows { get; init; }
  /// One analytic rrect shadow (spec §05) … Exactly one shadow per node — stacked shadows are a spec violation.
  ```

### A1 Box · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Primitives/Nodes/VisualNode.cs`
- **Handoff**: Background — ColorToken, or LinearGradient(from, to, angle) — exactly 2 stops (fence).
- **Code**: LinearGradient carries an optional third stop, Via, at ViaPosition (VisualNode.cs:390-395), and the web emits it as a real middle stop (TokenCss.Gradient). The type's doc names the break and its reason — the design system's from/via/to triples need the hue turn — and states the Photon fence: the shader interpolates two stops, so native paints From→To and the midpoint is web-only (PhotonRealizer.cs:582-583). So a via-gradient is a genuine cross-target appearance difference.
- **Evidence**:

  ```
  /// <summary>Optional middle stop at <see cref="ViaPosition"/>. <c>null</c> = plain 2-stop.</summary>
  public ColorToken? Via { get; init; }
  ```

### A1 Box · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Primitives/Nodes/VisualNode.cs`
- **Handoff**: Border(color, width) — uniform width only; drawn inside. Per-side widths are outside the fence: compose thin Boxes instead.
- **Code**: BoxStyle.BorderSides selects WHICH edges draw (VisualNode.cs:144), and the web realizer emits per-side border-width when it is not All (WebRealizer.cs:1274-1276). The doc explicitly rejects the handoff's remedy — it calls the wrapping one-dp Box "a layout lie about what the design meant" — and states its own fence: at a non-zero radius the corner where a present edge meets an absent one differs (web mitres, Photon squares), so only radius 0 is target-identical.
- **Evidence**:

  ```
  public BorderSides BorderSides { get; init; } = BorderSides.All;
  ```

### A1 Box · semantics · **unverified**

- **Component**: `src/eQuantic.UI.Primitives/Nodes/VisualNode.cs`
- **Handoff**: Pointer — Inert — arrow cursor, no hover, right-click falls through. A Box gains pointer states only by composing Pressable.
- **Code**: A bare Box carries three pointer facilities of its own: Hover and Focus style diffs (VisualNode.cs:222, 226) and Cursor (231). The web realizer lowers them to :hover/:focus-visible rules and a cursor declaration (WebRealizer.cs:1298, 1324-1333) and Photon registers a HoverRegion and a CursorRegion for the box (PhotonRealizer.cs:571-572, 620-621) — no Pressable involved.
- **Evidence**:

  ```
  public StyleDiff? Hover { get; init; }
  public StyleDiff? Focus { get; init; }
  public PointerCursor Cursor { get; init; }
  ```

### A1 Box · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Primitives/Nodes/VisualNode.cs`
- **Handoff**: Transform — 2D translate · scale · rotate about a pivot. Does not affect layout (paint-only).
- **Code**: Transform2D has no pivot component (VisualNode.cs:335-340) and both realizers hard-anchor at the element centre: the doc says "anchored at the element's center (the CSS default origin)" and Photon calls CenterAnchored(…, node.Bounds.Center) (PhotonRealizer.cs:465). A rotation about a corner or an arbitrary pivot is inexpressible. Paint-only is honoured on both targets.
- **Evidence**:

  ```
  public readonly record struct Transform2D(
      float TranslateX = 0, float TranslateY = 0, float RotationDegrees = 0, float ScaleX = 1, float ScaleY = 1)
  ```

### A1 Box · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Primitives/Nodes/VisualNode.cs`
- **Handoff**: LinearGradient(from, to, angle)
- **Code**: The axis is a four-value closed enum, not an angle: ToRight, ToBottom, ToBottomRight, ToBottomLeft (VisualNode.cs:362-374), and TokenCss.Gradient emits only those four CSS keywords. A gradient at any other angle cannot be authored.
- **Evidence**:

  ```
  public readonly record struct LinearGradient(
      ColorToken From,
      ColorToken To,
      GradientDirection Direction = GradientDirection.ToRight)
  ```

### A1 Box · semantics · **unverified**

- **Component**: `src/eQuantic.UI.Primitives/Nodes/VisualNode.cs`
- **Handoff**: A Box is the engine's rrect surfaced as a widget: 1 fill draw + optional border draw + optional shadow draw. … Paint order: shadow → fill (solid or 2-stop linear gradient) → border (inside stroke) → child.
- **Code**: BoxStyle carries two further fill layers beyond the single fill: Pattern, a repeating hairline grid (VisualNode.cs:117), and Glow, a radial gradient (121). Both reach paint — EmitChrome takes gradient, pattern and glow together (PhotonRealizer.cs:634-636) and the web stacks them as background-image layers (WebRealizer.cs:1254-1255). The documented order is grid below gradient, glow above the grid, which is a four-layer fill, not one.
- **Evidence**:

  ```
  public GridPattern? Pattern { get; init; }
  public RadialGradient? Glow { get; init; }
  ```

### A1 Box · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Web/WebRealizer.cs`
- **Handoff**: Shadow — Elevation(0–5) token only … Paint order: shadow → fill … → border → child.
- **Code**: On the web, Elevation additionally rewrites stacking: any Elevation > 0 sets z-index to the level and forces position:relative (WebRealizer.cs:1310-1316). Photon does no such thing — its Box case only draws the analytic shadow (PhotonRealizer.cs:586-594), so paint order there stays tree order. The deviation is argued at length in the comment above it ("a raised surface that anything drawn after it covers is not raised"), but it is web-only and unstated in the block.
- **Evidence**:

  ```
  element.Style!.ZIndex = style.Elevation.ToString(System.Globalization.CultureInfo.InvariantCulture);
  element.Style.Position ??= Core.Position.Relative;
  ```

### A2 Row · Column · semantics · **unverified**

- **Component**: `src/eQuantic.UI.Primitives/Nodes/VisualNode.cs`
- **Handoff**: wrap — Row only: children flow to new lines (chip groups).
- **Code**: Wrap lives on the shared FlexNode base (VisualNode.cs:1395) and the Column constructor takes it as a parameter (1456-1457), so a wrapping Column is authorable; the native engine routes it through the same MeasureFlexWrapped (LayoutEngine.cs:959) and the web emits flex-wrap on a column container (WebRealizer.cs:1465). The doc attributes wrap to spec S3 rather than A2.
- **Evidence**:

  ```
  public Column(float gap = 0, MainAlign main = MainAlign.Start,
      CrossAlign cross = CrossAlign.Stretch, bool wrap = false, float? runGap = null,
  ```

### A2 Row · Column · semantics · **unverified**

- **Component**: `src/eQuantic.UI.Primitives/Nodes/VisualNode.cs`
- **Handoff**: wrap — … Line spacing = gap.
- **Code**: RunGap lets line spacing differ from gap (VisualNode.cs:1397-1398); the web emits the "run main" pair when they differ (WebRealizer.cs:1591-1600) and native uses `flex.RunGap ?? flex.Gap` (LayoutEngine.cs:1331). The default matches the handoff, so this is an added override rather than a wrong default.
- **Evidence**:

  ```
  /// <summary>Spacing BETWEEN WRAPPED LINES (spec S3). <c>null</c> = same as <see cref="Gap"/>.</summary>
  public float? RunGap { get; init; }
  ```

### A3 Stack · semantics · **unverified**

- **Component**: `src/eQuantic.UI.Native.Framework/Layout/LayoutEngine.cs`
- **Handoff**: A11y: order = child order (base first, overlays after).
- **Code**: When any Positioned child carries a non-zero ZIndex, native re-sorts the Stack's layout children by Z (LayoutEngine.cs:603-613), and both the semantics walk and the focus/hit walk read that list in order (SemanticsTree.Walk; PhotonHost.cs:288 "order, depth-first — hit regions register in exactly that order"). Reading order then follows z-order instead of child order. The web keeps DOM order and only writes z-index (WebRealizer.cs:255), so the two targets announce a z-ordered Stack differently.
- **Evidence**:

  ```
  .OrderBy(e => e.Z).ThenBy(e => e.I)
  .Select(e => e.Node)
  ```

### A4 Spacer · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Web/WebRealizer.cs`
- **Handoff**: Pointer — Never hit-testable — clicks pass through to whatever sits beneath.
- **Code**: The web Spacer is a plain div with no pointer-events:none (WebRealizer.cs:2088-2106, and the TS twin lowerSpacer at src/eQuantic.UI.Runtime/src/shared/lowering.ts:2713-2729), so it is the hit target over its own area and a click on it never reaches a layer beneath it in a Stack. The realizer already uses PointerEvents = "none" elsewhere for exactly this (WebRealizer.cs:404, 481, 1003). Native matches the handoff — a Spacer registers no region at all. "Announces nothing" is honoured on both (aria-hidden).
- **Evidence**:

  ```
  return new RealizedElement("div") { Style = style, AriaHidden = true };
  ```

### A5 SafeArea · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/AppBar.cs`
- **Handoff**: AppBar, BottomNavigation and BottomSheet handle their own insets (they paint under the inset and pad inside; §08).
- **Code**: Neither bar pads for the host inset yet; both class docs name the gap and defer it to the host-insets work. AppBar: "v1 fences: … safe-area top painting joins the host insets". BottomNavigation: "v1 fences: … the bottom safe-area inset joins the host insets". Stated reason: the inset plumbing is a host concern the components will adopt, not a per-component number.
- **Evidence**:

  ```
  AppBar.cs:9-10  /// title gets 12dp when there is no leading slot. v1 fences: the scrolled Surface+E2 elevation
  /// swap joins the scroll-linking system; safe-area top painting joins the host insets; titleAlign
  BottomNavigation.cs:17-18  /// outline glyph. Badges anchor to the icon's top-end. v1 fences: the E2 top shadow joins the
  /// engine shadow primitive; the bottom safe-area inset joins the host insets.
  ```

### A5 SafeArea · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Native.Shell.MacOS/PhotonWindow.cs`
- **Handoff**: Desktop windows have no notches or home indicators — every inset resolves to 0 and the node is a pass-through. Never reserve fake inset space on pointer targets.
- **Code**: On macOS a window with WindowChrome.Unified reports a 28dp TOP safe-area inset, so SafeArea is not a pass-through on that desktop target. The line carries its own justification: "Whatever the system kept for itself is a safe area, exactly as a phone's notch is" — under unified chrome the content really does run beneath the title bar. Worth a review because the SafeArea class doc still states the handoff rule ("Photon reads them from the window (zero on a desktop with no cutouts…)").
- **Evidence**:

  ```
  PhotonWindow.cs:177-180
              // Whatever the system kept for itself is a safe area, exactly as a phone's notch is.
              SafeAreaInsets = _chrome == WindowChrome.Unified
                  ? new EdgeInsets(0, TitleBarHeight, 0, 0)
                  : default,
  ```

### A6 ScrollView · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Native.Framework/Layout/LayoutEngine.cs`
- **Handoff**: Same-axis nesting asserts in debug — restructure with List sections.
- **Code**: MeasureScrollView measures the child unbounded on its axis and returns; there is no walk for an ancestor ScrollView, no debug assert and no diagnostic. Grepping the layout engine, the web realizer and the node itself finds no same-axis nesting check anywhere, so a vertical ScrollView inside a vertical ScrollView lays out silently (the inner one takes the whole gesture).
- **Evidence**:

  ```
  LayoutEngine.cs:649-656  private static LayoutNode MeasureScrollView(ScrollView scroll, float maxW, float maxH, LayoutContext ctx, string path)
      {
          var result = ctx.Node(scroll);
          var horizontal = scroll.Axis == ScrollAxis.Horizontal;
  
          var child = Measure(scroll.Child,
  ```

### A6 ScrollView · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Native.Framework/Layout/ScrollStore.cs`
- **Handoff**: Platform-native curves. iOS: exponential decay (normal rate 0.998) + rubber-band overscroll. Android: fling friction + stretch overscroll (scale transform — inside the fence; no glow arc).
- **Code**: One platform-agnostic curve, no overscroll of either kind: the glide is a fixed 0.022-per-ms chase of a target with a flat FlingReach of 90, clamped hard to [0, maxOffset] (ScrollStore.cs:49, 63-66) so there is nothing to rubber-band or stretch. The ScrollView node names the fence and the reason — the platform physics wait for the native interaction system. Worth reviewing because the same fence still claims "today the scroll position is the programmatic Offset", which the ScrollStore compositor has since overtaken.
- **Evidence**:

  ```
  ScrollStore.cs:17  private const float SmoothingPerMs = 0.022f;
  ScrollStore.cs:36  private const float FlingReach = 90;
  VisualNode.cs:1822-1825  /// The child lays out UNBOUNDED on the scroll axis and is clipped to the viewport. v1 fences: the
  /// platform physics (decay/fling/rubber-band), gesture capture and the fading scrollbar pill join
  /// with the native interaction system; today the scroll position is the programmatic
  /// <see cref="Offset"/>
  ```

### A6 ScrollView · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Primitives/Nodes/VisualNode.cs`
- **Handoff**: 3dp pill, Radius.Full, BorderStrong @ 60%, 2dp from edge. Appears on scroll, fades out 800ms after idle (Fast/Base motion). Non-interactive in v1.
- **Code**: No scrollbar is drawn on Photon — "scrollbar" matches nothing in eQuantic.UI.Native.Components or the engine, so none of the five figures (3dp, Radius.Full, BorderStrong 60%, 2dp offset, 800ms fade) exists to check. The web realizer leaves the browser's own scrollbar in place by design (WebRealizer.cs:300 comment: the browser owns physics, momentum and the scrollbar). The ScrollView fence names it: "the fading scrollbar pill join[s] with the native interaction system". Consequence: the desktop contract's overlay, hover-revealed, DRAGGABLE scrollbar is also absent on Photon.
- **Evidence**:

  ```
  VisualNode.cs:1823-1824  /// platform physics (decay/fling/rubber-band), gesture capture and the fading scrollbar pill join
  /// with the native interaction system;
  ```

### A6 ScrollView · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Native.Components/PhotonHost.cs`
- **Handoff**: Scroll = translated rrect clip. The compositor caches the content layer; a scroll frame re-encodes zero widgets — it updates one transform.
- **Code**: A scroll frame re-encodes the WHOLE tree. ScrollTo/ScrollBy only set NeedsRender, and RenderFrame then re-runs PhotonRealizer.Realize over _root, which re-measures the ScrollView's child from scratch and moves it by writing child.Bounds (LayoutEngine.cs:654, 668-672) — the offset is a layout translate, not a retained transform on a cached layer. There is object recycling (nodePool) and raster caching (text/icon), but no content-layer cache and no transform-only path, so the 8.33ms 120Hz claim is not what the code buys.
- **Evidence**:

  ```
  PhotonHost.cs:177  _lastFrame = PhotonRealizer.Realize(_root, Width, Height, _theme, Mode, builder, _measurer, _typeScale, _pressed, …
  PhotonHost.cs:1313  if (_scrolls.ScrollTo(pan.Path, pan.FromOffset - travelled, pan.MaxOffset))
                      NeedsRender = true;
  ```

### A8 Text · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Primitives/Nodes/VisualNode.cs`
- **Handoff**: new Text("…", role: TypeRole.BodyL, color: theme.TextPrimary, maxLines: 2, overflow: Overflow.Ellipsis)
- **Code**: The Text constructor has no `overflow` parameter and no `Overflow` enum exists anywhere in the repo (`git grep "enum Overflow"` finds nothing); the UI factory mirrors the same eight parameters (src/eQuantic.UI.Components/UI.cs:95-98). Ellipsis is hard-wired as the only truncation mode, so the handoff's example call does not compile and a caller cannot ask for clip-without-ellipsis.
- **Evidence**:

  ```
  src/eQuantic.UI.Primitives/Nodes/VisualNode.cs:1281-1283 —
      public Text(string content, TypeRole role = TypeRole.BodyL, ColorToken? color = null,
          int maxLines = 0, TextAlignment align = TextAlignment.Start, bool mono = false,
          bool tabular = false, TypeStyle? styleOverride = null)
  ```

### A10 Icon · behaviour · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Primitives/Nodes/Icon.cs`
- **Handoff**: "Sm 16 · Dense 20 · Md 24 · Lg 32 — the atlas whitelist. Arbitrary dp sizes are a compile-time error."
- **Code**: The whitelist itself is right (IconSize.Sm/Dense/Md/Lg = 16/20/24/32, Tokens.cs:42-53), but size is a plain float parameter, so `new Icon(Icons.Search, 18)` compiles and only throws at construction — a runtime ArgumentOutOfRangeException, not a compile-time error. No analyzer/generator diagnostic checks the literal (grep over eQuantic.UI.Generators and eQuantic.UI.Compiler finds no icon-size rule).
- **Evidence**:

  ```
  public Icon(IconGlyph glyph, float size = 24, ColorToken? color = null, string? label = null)
  {
      if (size is not (16 or 20 or 24 or 32))
          throw new ArgumentOutOfRangeException(nameof(size),   // Icon.cs:123-127
  ```

### A11 Image · documented-deviation · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Primitives/Nodes/Image.cs`
- **Handoff**: "Layout: Needs bounded size from parent or explicit dims; intrinsic aspect ratio used when one axis is known." (the block's own signature omits dims: `new Image(source, fit: ImageFit.Cover, radius: Radius.Md, alt: "…")`)
- **Code**: Width AND height are required positional parameters — a parent-bounded image, or one axis plus intrinsic aspect ratio, cannot be expressed. The layout engine hard-codes the slot from those two numbers. The class doc names and justifies the deviation: "an EXPLICITLY sized slot (layout can never infer extent from an undecoded source)" (Image.cs:15-16), echoed at LayoutEngine.cs:296.
- **Evidence**:

  ```
  public Image(string source, float width, float height, ImageFit fit = ImageFit.Cover, string alt = "")   // Image.cs:25
  // Images are an explicitly sized slot - layout can't infer extent from undecoded sources (A11).
  Image image => ctx.Node(image, new Rect(0, 0, image.Width, image.Height)),                                // LayoutEngine.cs:296-297
  ```

### A11 Image · documented-deviation · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Primitives/Nodes/Image.cs`
- **Handoff**: "NineSlice(insets: 14) — 9-slice serves stretchable art (speech bubbles, decorated panels) without vector paths — corners stay crisp at any size."
- **Code**: ImageFit has exactly three members (Contain/Cover/Stretch) and there is no NineSlice type, fit mode or insets parameter anywhere in src/ (grep "NineSlice" hits only the fence comment). Stated reason: the class doc lists it as a v1 fence — "NineSlice … join[s] with the asset and animation systems".
- **Evidence**:

  ```
  public enum ImageFit : byte { Contain = 0, Cover = 1, Stretch = 2, }   // Image.cs:4-12
  /// v1 fences: NineSlice, the loading/error states and the decode crossfade join with the asset
  /// and animation systems;                                              // Image.cs:17-18
  ```

### A11 Image · documented-deviation · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Primitives/Nodes/Image.cs`
- **Handoff**: "Loading: SurfaceSubtle fill (or Skeleton shimmer when inside one). Error: SurfaceSubtle + Md broken-image glyph in TextMuted. Decoded image crossfades in, 200ms."
- **Code**: Neither realizer has a loading or error state: the web lowering emits a bare <img> with src/alt and no placeholder, no error fallback and no crossfade (WebRealizer.cs:1181-1202); the native path draws a plain SurfaceSubtle rrect on a null/failed decode, with no Md broken-image glyph in TextMuted and no 200ms fade (PhotonRealizer.cs:496-501). Stated reason: the class doc fences "the loading/error states and the decode crossfade" to the asset/animation systems.
- **Evidence**:

  ```
  if (data is null)
  {
      builder.FillRRect(new RRect(node.Bounds, image.CornerRadius),
          Paint.Solid(theme.SurfaceSubtle.Resolve(mode)));
      return;
  }                                                     // PhotonRealizer.cs:496-501
  ```

### A11 Image · missing-feature · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Primitives/Nodes/Image.cs`
- **Handoff**: "Memory: Decoded at target size (never full-res into a thumbnail)"
- **Code**: The native realizer uploads the decode at the SOURCE's dimensions, not the slot's — IImageLoader.Load takes only a path (src/eQuantic.UI.Native.Framework/Text/IImageLoader.cs:14, `RgbaImage? Load(string source)`), so a 4000px photo in a 40dp avatar becomes a 4000px texture. Not named as a fence anywhere: the EmitImage doc comment fences only bilinear sampling.
- **Evidence**:

  ```
  var decoded = loader.Load(image.Source);
  data = decoded is null ? null : TextureData.Rgba(decoded.Width, decoded.Height, decoded.Rgba);   // PhotonRealizer.cs:491-492
  ```

### A11 Image · missing-feature · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Primitives/Nodes/Image.cs`
- **Handoff**: "atlas pages evict LRU under memory pressure (M4 lifecycle)"
- **Code**: The native image cache is an unbounded Dictionary keyed by source string with no eviction policy, no LRU ordering, no size cap and no memory-pressure hook — entries are only ever added (PhotonRealizer.cs:344 declares it, :493 writes it). Not named as a fence in the Image or EmitImage doc comments.
- **Evidence**:

  ```
  public Dictionary<string, TextureData?>? ImageCache { get; init; }   // PhotonRealizer.cs:344
  cache?[image.Source] = data;                                          // PhotonRealizer.cs:493
  ```

### A12 Button · documented-deviation · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/Button.cs`
- **Handoff**: Variant × state grid — Link has Default / Pressed / Focused / Disabled columns (only Loading is "n/a").
- **Code**: Link is the one variant with no pressed treatment at all: pressedFill is null, so Pressable.PressedBackground is null and neither realizer paints anything on touch-down. The comment names it and gives the reason — the pressed TEXT swap needs a text channel in the style diff, which does not exist yet.
- **Evidence**:

  ```
  Button.cs:99-104  ColorToken? pressedFill = Variant switch\n        {\n            Variant.Link => null,   (Button.cs:98 "(Link's pressed TEXT swap joins with rich text.)")
  ```

### A12 Button · documented-deviation · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/Button.cs`
- **Handoff**: "Hover (pointer-only): filled variants = Base→Pressed midpoint fill · Outline/Ghost = SurfaceSubtle fill · Link = underline (§10 rule)."
- **Code**: The two filled/quiet rules are exact (colors.Hover is Base.MidpointWith(Pressed), ColorToken.cs:51; Outline/Ghost take theme.SurfaceSubtle), but Link has no hover treatment: hoverFill is null and no underline channel exists. The comment names it and states the reason; the web test even pins the absence (Wave2ComponentTests.cs:76-77 "link.Css.Should().NotContain(":hover")").
- **Evidence**:

  ```
  Button.cs:109-114  ColorToken? hoverFill = Variant switch\n        {\n            Variant.Link => null,   (Button.cs:107-108 "Link's hover is an underline, which joins when the style diff grows a text channel.")
  ```

### A12 Button · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/Button.cs`
- **Handoff**: "Min width 64dp · label scales with Dynamic Type, height grows with the line box, padding fixed."
- **Code**: MinWidth 64 and the Dynamic-Type-scaling label are right (ButtonStyles.MinWidth = 64; TypeStyle.OfSize defaults MaxScale 1.3), but the container height is a FIXED dp, not a floor: BoxStyle.Height lowers to `height: 40px` on the web (WebRealizer.cs:1238 `Height = Size(style.Height)`, and MinHeight is only emitted from style.MinHeight, :1245). The box therefore does not grow with the line box under an OS text scale — inside the ×1.3 clamp the label still fits, so nothing clips today, but the stated rule is not what the code expresses.
- **Evidence**:

  ```
  Button.cs:116-118  var container = new Box(new BoxStyle\n        {\n            Height = height,
  ```

### A13 IconButton · documented-deviation · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/IconButton.cs`
- **Handoff**: "Optional toggle semantics via selected (glyph swaps outline → filled)."
- **Code**: The swap is not automatic from `selected`: it happens only when the caller also supplies SelectedGlyph, otherwise a selected IconButton keeps its outline glyph. The doc names the mechanism and its reason — "outline → filled pairs are DISTINCT glyphs, spec A10" — so the pairing cannot be derived from Icons alone today.
- **Evidence**:

  ```
  IconButton.cs:81  var glyph = Selected && SelectedGlyph is { } filledGlyph ? filledGlyph : Glyph;   (IconButton.cs:22-23 doc: "the glyph swaps to SelectedGlyph (outline → filled pairs are DISTINCT glyphs, spec A10)")
  ```

### B1 Card · documented-deviation · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/Card.cs`
- **Handoff**: "Header: Title 16/600 + Caption subtitle + one trailing action (32dp IconButton, hit-slopped)." · "header/body gap S3 12" (constructor slot `header: CardHeader?`)
- **Code**: No header slot; the component's own doc comment names the deviation and states the reason: "header/body composition owned by the caller (compose a Column with gap S3)". Reported for review only — the stated reason is that composition is the caller's job.
- **Evidence**:

  ```
  Card.cs:17-18  /// The design system's Card (spec B1): Radius.Lg surface, S4 padding (S5 for hero cards),
  ///  header/body composition owned by the caller (compose a <see cref="Column"/> with gap S3).
  ```

### B1 Card · metric · **REFUTED**

- **Component**: `src/eQuantic.UI.Primitives/Theme/PhotonTheme.cs`
- **Handoff**: "Header: Title 16/600 …"
- **Code**: No TypeRole resolves to 16dp/600: Title is 20/SemiBold (PhotonTheme.cs:116), TitleSmall is 15/Bold (PhotonTheme.cs:121), BodyM is 15/Regular (PhotonTheme.cs:118). Since Card delegates the header to the caller, and TypeStyle StyleOverride is marked "SYSTEM COMPONENTS ONLY … free-form font sizes remain outside the component API (spec A8)" (VisualNode.cs:1358-1362), app code composing a card header cannot reach the handoff's 16/600 at all.
- **Evidence**:

  ```
  PhotonTheme.cs:116  TypeRole.Title => new TypeStyle(20, 26, FontWeight.SemiBold, -0.2f, 1.25f),
  PhotonTheme.cs:121  TypeRole.TitleSmall => new TypeStyle(15, 20, FontWeight.Bold, -0.1f, 1.3f),
  ```

### B2 List · ListItem · documented-deviation · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/ListItem.cs`
- **Handoff**: "new List(items: n, builder: i => new ListItem(...), recycle: true)" · "List virtualizes: builder-driven, per-type recycle pools, keyed-LIS diffing, ~1.5 viewports realized."
- **Code**: List takes a materialized IReadOnlyList<ListItem> with no builder and no recycle flag, and Build materializes every row into a Column. The doc comment names the deviation and its reason: "v1 fences: recycling/virtualization joins the List engine work (this renders bounded item sets)". A separate ListView component does virtualize by builder+index, but it has no divider ownership and is not this component.
- **Evidence**:

  ```
  ListItem.cs:145  public List(IReadOnlyList<ListItem> items, bool dividers = true)
  ListItem.cs:138-139  /// v1 fences: recycling/virtualization joins the List engine work (this renders bounded item sets),
  ```

### B2 List · ListItem · documented-deviation · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/ListItem.cs`
- **Handoff**: "Selectable lists are ONE stop: ↑/↓ move, Space toggles, Home/End jump." · "selectable = listbox / option + aria-selected. Native Selected field: REQUEST (§10)."
- **Code**: The requested Selected field now exists, but it implements the NAVIGATION contract instead of the selectable one: it lowers to PressableRole.Destination → aria-current="page" (WebRealizer.cs:1886-1889), each row keeps its own tab stop, and no roving ↑/↓/Space/Home/End exists. The doc comment names this fence and its reason: the roving stop belongs to the container, and an `option` row without it "would leave the tab order with nothing to put it back".
- **Evidence**:

  ```
  ListItem.cs:127  Role = Selected ? PressableRole.Destination : PressableRole.Button,
  ListItem.cs:53-59  /// FENCE — the handoff's B2 draws a line this does not yet cross. … a SELECTABLE list is a listbox of option rows stating aria-selected, and one tab stop for the whole thing
  ```

### B3 AppBar · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/AppBar.cs`
- **Handoff**: Leading back button 48×48.
- **Code**: AppBar adds Leading unsized and imposes nothing; an IconButton's default SizeVariant.Medium resolves to Sizing.Height(Medium) = 40 (Tokens.cs:84), which the render confirms (`width: 40px; height: 40px`). The 48×48 exists only as prose ("48×48 back IconButton by convention", AppBar.cs:22) — unlike Actions, whose max-3 limit IS enforced.
- **Evidence**:

  ```
  AppBar.cs:52  if (Leading is { } leading) row.Add(leading);
  ```

### B4 BottomNavigation · semantics · **unverified**

- **Component**: `src/eQuantic.UI.Components/BottomNavigation.cs`
- **Handoff**: A11y: tab semantics — "Home, tab, 1 of 4, selected". [vs the same block's Semantics line: selected = aria-current="page" on web]
- **Code**: The block contradicts itself and the code follows the Semantics line, with the choice reasoned in place (BottomNavigation.cs:87-89): Role = Destination → aria-current="page", no role="tab" and no set position ("1 of 4" is not emitted — no aria-setsize/aria-posinset exist in the lowering). Reported so the handoff line can be reconciled, not as a defect against the Semantics contract.
- **Evidence**:

  ```
  BottomNavigation.cs:90  Role = PressableRole.Destination,
  ```

### B5 Tabs · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/Tabs.cs`
- **Handoff**: Fixed mode: 2–4 tabs, equal width. Scrollable: 5+.
- **Code**: Labels is a plain init property with no count check, and with Scrollable fenced off there is no mode that legally takes 5+. Verified: `new Tabs(["a"…"f"], 0, _ => {})` renders six equal 1/6 cells without throwing. Both siblings in this family enforce their own limits (AppBar.cs:35 throws past 3 actions, BottomNavigation.cs:38 throws outside 3–5).
- **Evidence**:

  ```
  Tabs.cs:21  public IReadOnlyList<string> Labels { get; init; }
  ```

### B6 Avatar · documented-deviation · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/Avatar.cs`
- **Handoff**: initials "tinted 2-stop gradient hashed from the name"
- **Code**: A single flat Subtle fill, not a 2-stop gradient (src/eQuantic.UI.Components/Avatar.cs:95). The class doc names the deviation and gives the reason — "the spec's 2-stop gradient hash upgrades this when gradient tokens land" (Avatar.cs:16-17). Worth reviewing because that reason is now stale: BoxStyle already carries a token-based 2-stop gradient (src/eQuantic.UI.Primitives/Nodes/VisualNode.cs:113 and the LinearGradient(ColorToken From, ColorToken To) record at VisualNode.cs:385-388).
- **Evidence**:

  ```
  Avatar.cs:16  /// from the name (the spec's 2-stop gradient hash upgrades this when gradient tokens land); the
  Avatar.cs:95  Background = hasInitials ? tint.Subtle : theme.SurfaceSubtle,
  VisualNode.cs:113  public LinearGradient? Gradient { get; init; }
  ```

### B7 Badge · missing-feature · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/Badge.cs`
- **Handoff**: "Count changes: scale-pop 0.8→1 Fast 100ms; appearing badge fades+scales in Base 200ms."
- **Code**: Build returns a bare Box in both branches with no motion wrapper (Badge.cs:58 for the dot, Badge.cs:77 for the count pill) — a count change or a first appearance pops instantly. The vocabulary has no scale entrance either: PresenceMotion offers only Fade and SlideUp (src/eQuantic.UI.Primitives/Nodes/VisualNode.cs:700-705), so the spec's scale-pop cannot be expressed today.
- **Evidence**:

  ```
  Badge.cs:77  return new Box(new BoxStyle
  VisualNode.cs:702      Fade = 0,
  VisualNode.cs:704      SlideUp = 1,
  ```

### B8 Chip · missing-feature · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/Chip.cs`
- **Handoff**: "selection change animates check-in Fast 100ms"
- **Code**: The check glyph is added to / removed from the Row unconditionally on rebuild with no motion wrapper (Chip.cs:66-69), so it pops in and out. Only the background transitions (the generated .eq-pressable rule animates background-color alone, src/eQuantic.UI.Web/TokenCss.cs:318).
- **Evidence**:

  ```
  Chip.cs:66  if (Kind == ChipKind.Filter && Selected)
  Chip.cs:68      content.Add(new Icon(Icons.Check, IconSize.Sm, textColor));
  ```

### B8 Chip · missing-feature · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/Chip.cs`
- **Handoff**: "Remove mirrors as a custom action on native — REQUEST (§10)."
- **Code**: Still unimplemented, as the block itself anticipates: there is no custom-action concept anywhere in the framework (grep for CustomAction / accessibilityCustomAction across src/ returns nothing), so on native the remove glyph is an ordinary nested pressable node rather than a custom action of the chip.
- **Evidence**:

  ```
  Chip.cs:73  content.Add(new Pressable(new Icon(Icons.Close, IconSize.Dense, textColor), OnRemove)
  ```

### B8 Chip · metric · **REFUTED**

- **Component**: `src/eQuantic.UI.Components/Chip.cs`
- **Handoff**: "Height 32 · … One size (chips don't scale with SizeVariant)."
- **Code**: 32 holds only at Comfortable density. The chip reads the control ladder with the ambient density (Chip.cs:81), and Sizing.Height(SizeVariant.Small, Density.Compact) = 26 (src/eQuantic.UI.Primitives/Theme/Tokens.cs:83) — so on a pointer target the chip is 26dp, not 32. This is the framework's deliberate target-driven density rule (Tokens.cs:63-77), but it is neither what B8 pins nor what the component's own doc comment claims ("height 32", Chip.cs:14) — flagging it so the reviewer can decide which of the two documents is wrong.
- **Evidence**:

  ```
  Chip.cs:14  /// The design system's Chip / Tag (spec B8): height 32, Radius.Full, 12dp X padding, 13/600 label —
  Chip.cs:81  Height = Sizing.Height(SizeVariant.Small, context.Density),
  Tokens.cs:83  SizeVariant.Small => density == Density.Compact ? 26 : 32,
  ```

### B9 TextInput · documented-deviation · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/TextInput.cs`
- **Handoff**: new TextInput(..., keyboard: Keyboard.Email, ...) ... keyboard type + return action from keyboard:
- **Code**: The constructor has no keyboard parameter and no Keyboard vocabulary exists in Primitives (no inputmode/keyboard-type concept outside eQuantic.UI.Core/HtmlElement.cs:370's raw escape hatch). Stated reason, class doc TextInput.cs:12-13: "v1 fences: keyboard hints and the trailing slot (clear/eye/counter) land with IME at M4".
- **Evidence**:

  ```
  public TextInput(string value, Action<string>? onChanged = null, string label = "", string? placeholder = null, string? helper = null, string? error = null, Icons? leading = null, SizeVariant size = SizeVariant.Large)  // TextInput.cs:20-22
  ```

### B9 TextInput · documented-deviation · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/TextInput.cs`
- **Handoff**: Trailing slot: clear button (visible when focused + non-empty, 20dp glyph / 48dp hit) or password eye or counter "12/40" Caption.
- **Code**: No trailing slot of any kind — the row holds the leading Icon and the Flexible entry only (TextInput.cs:95-114). Obscure exists as a flag (TextInput.cs:54) but with no eye toggle to unmask it. Stated reason, class doc TextInput.cs:12-13: the trailing slot "land[s] with IME at M4".
- **Evidence**:

  ```
  var row = new Row(gap: 10) { Height = SizeValue.Fill, Cross = CrossAlign.Center };  // TextInput.cs:95 — only Leading (:98) and the Flexible entry (:100) are ever added
  ```

### B9 TextInput · documented-deviation · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/TextInput.cs`
- **Handoff**: States: ... disabled = 38% group + SurfaceSubtle fill.
- **Code**: Only the SurfaceSubtle fill lands; the 38% opacity over the whole group is not applied (label, caption and leading icon keep full-strength tokens). Stated reason, class doc TextInput.cs:13-14: "disabled shows the SurfaceSubtle fill (the 38% opacity group joins with the engine's opacity primitive)".
- **Evidence**:

  ```
  Background = Disabled ? theme.SurfaceSubtle : theme.Surface,  // TextInput.cs:120 — the only Disabled-driven visual
  ```

### B10 SearchField · documented-deviation · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/SearchField.cs`
- **Handoff**: new SearchField(query, onChanged, placeholder: "Search…", onSubmit: fn, debounce: 300) ... Live results: onChanged debounced 300ms
- **Code**: The constructor has no debounce parameter and OnChanged fires on every keystroke straight through the TextEntry input handler (src/eQuantic.UI.Runtime/src/shared/lowering.ts:839). Stated reason, class doc SearchField.cs:10-11: "the 300ms onChanged debounce is the app's until a shared timer primitive exists".
- **Evidence**:

  ```
  public SearchField(string query, Action<string>? onChanged = null, string? placeholder = null, Action? onSubmit = null)  // SearchField.cs:17-18
  ```

### B10 SearchField · documented-deviation · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/SearchField.cs`
- **Handoff**: Cancel text button (Link style) slides in on focus, collapses the field back on tap — iOS pattern kept on both platforms for consistency.
- **Code**: No Cancel slot exists: the component is stateless, tracks no focus, and builds a single Box whose row is [search glyph, entry, optional clear] (SearchField.cs:36-64). Stated reason, class doc SearchField.cs:9-10: "v1 fences: the focused Cancel slide-in rides the state-transition motion system".
- **Evidence**:

  ```
  public sealed class SearchField : StatelessComponent  // SearchField.cs:13 — no focus state, so no focus-driven Cancel
  ```

### B11 Checkbox · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/Checkbox.cs`
- **Handoff**: "Motion: fill + glyph scale-pop 0.6→1, Fast 100ms"
- **Code**: Not implemented, and the component says so: the doc comment names it as a v1 fence — "the scale-pop motion joins later" — pending the animation system. Checkbox.cs:10-11.
- **Evidence**:

  ```
  Checkbox.cs:10-11 — `/// that is only partly true shows the DASH, never a tick. v1 fence: the scale-pop motion joins` / `/// later; Error tints the border Destructive.`
  ```

### B11 Checkbox · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/Checkbox.cs`
- **Handoff**: the specimen strip lists a "disabled" state alongside unchecked / checked / mixed / error
- **Code**: Disabled affects only the label's colour; the box keeps its full-strength Primary fill and BorderStrong border, so a label-less disabled checkbox is pixel-identical to an enabled one. Compare the sibling Switch, which does dim (Switch.cs:38 `if (Disabled) trackFill = trackFill.WithOpacity(theme.DisabledOpacity);`). Checkbox.cs:50-58 has no Disabled branch.
- **Evidence**:

  ```
  Checkbox.cs:63 — `row.Add(new Text(label, TypeRole.BodyM, Disabled ? theme.TextMuted : theme.TextPrimary, maxLines: 2));` (the only use of Disabled in the visual tree)
  ```

### B11 Checkbox · semantics · **unverified**

- **Component**: `src/eQuantic.UI.Components/Checkbox.cs`
- **Handoff**: `new Checkbox(checked, onChanged, label: "…", tristate: false)`
- **Code**: There is no `tristate` slot. The third state is modelled as an explicit `Indeterminate` init property that states the current mixed value rather than enabling a mode, and the cycle "mixed → checked → unchecked" is left entirely to the caller (OnChanged is a bare `Action`, and the doc says "Pressing it is the caller's call"). Checkbox.cs:15, :36.
- **Evidence**:

  ```
  Checkbox.cs:15 — `public Checkbox(bool @checked, Action? onChanged = null, string? label = null)`; Checkbox.cs:36 — `public bool Indeterminate { get; init; }`
  ```

### B12 Switch · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/Switch.cs`
- **Handoff**: "Toggle: thumb slides Base 200ms standard + track crossfades."
- **Code**: The two ends are rendered as two different trees (Positioned start vs Positioned end) with no tween between them, and no crossfade on the track fill — the component's doc names this as its fence, deferred to the animation system. Switch.cs:12, :74-76. (The drag RELEASE glide the block asks for does exist: draggable.ts:14 `const GLIDE_MS = 200;` applied at draggable.ts:107.)
- **Evidence**:

  ```
  Switch.cs:12 — `/// Fence: the slide/crossfade motion between the two ends joins the animation system.`
  ```

### B13 RadioGroup · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/RadioGroup.cs`
- **Handoff**: "The group is ONE tab stop with roving focus"
- **Code**: It is one tab stop, but the focus does not rove: the Adjustable host is the only focusable element (`tabindex="0"`, WebRealizer.cs:1105 / lowering.ts:2379) and every row is pinned at `tabindex="-1"`, so no individual radio ever holds focus and nothing tells assistive tech which one is current. The component names the gap — `aria-activedescendant` is an explicit v1 fence awaiting the shared id machinery. RadioGroup.cs:12-13.
- **Evidence**:

  ```
  RadioGroup.cs:12-13 — `/// system; <c>aria-activedescendant</c> joins the shared id machinery.`; lowering.ts:2379 — `host.attributes['tabindex'] = '0';`
  ```

### B13 RadioGroup · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/RadioGroup.cs`
- **Handoff**: "Space selects when landing unselected"
- **Code**: The Adjustable's keydown maps the four arrows and returns on anything else, and the host is a `<div role="radiogroup">` (not a button), so Space does nothing at all on the group. In practice the state is hard to reach because the arrows both move and select, but the key is genuinely unhandled. lowering.ts:2386-2394 (C# SSR twin WebRealizer.cs:1088-1110 emits the same markup with no handler).
- **Evidence**:

  ```
  lowering.ts:2391-2393 — `: event.key === 'ArrowDown' ? (downIsNext ? 1 : -1)` / `: 0;` / `if (direction === 0) return;`
  ```

### B13 RadioGroup · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/RadioGroup.cs`
- **Handoff**: "Motion: dot scales in 0.4→1 Fast 100ms; previous dot scales out simultaneously."
- **Code**: The dot is added and removed as a plain Box with no transition; the component names it as a v1 fence pending the animation system. RadioGroup.cs:11-12, :46-54.
- **Evidence**:

  ```
  RadioGroup.cs:11-12 — `/// order, still pressable by pointer. v1 fences: the dot scale motion joins the animation` / `/// system; …`
  ```

### B13 RadioGroup · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/RadioGroup.cs`
- **Handoff**: "A11y: … 'Monthly, radio button, 2 of 3, selected'" (with "Native Checked field: REQUEST")
- **Code**: On the web target the rows announce correctly (role=radio + aria-checked, WebRealizer.cs:1848-1854). On Photon they do not: the semantics builder maps only Checkbox and Switch to a checked role and drops every other PressableRole to a plain Button with no checked bit, so a native radio row reads as "Monthly, button". The fence is stated on the role itself (VisualNode.cs:1131-1132: "Native fence: the semantics tree carries no checked bit yet; the role joins its expansion"), and the block's own "Native Checked field: REQUEST" acknowledges the field.
- **Evidence**:

  ```
  Semantics.cs:99 — `_ => (SemanticRole.Button, null),` (the fallback that PressableRole.Radio falls into)
  ```

### B14 ProgressBar · documented-deviation · **REFUTED**

- **Component**: `src/eQuantic.UI.Components/ProgressBar.cs`
- **Handoff**: Value changes animate Base 200ms standard, forward only — regressions snap (honesty over smoothness).
- **Code**: Web honours it (WebRealizer.cs:2075-2077 emits "flex-grow var(--eq-motion-base) var(--eq-curve-standard)", i.e. Motion.BaseMs=200 + Curve.Standard). On Photon the value change SNAPS — Flexible.AnimateChanges names the fence: "native joins with the transition animator (until then weights snap, the documented fence)". Stated reason: the native transition animator has not landed.
- **Evidence**:

  ```
  VisualNode.cs:1507-1510  /// <summary>Animate WEIGHT changes at Base 200ms standard (spec B14 ...) Web = a flex-grow transition; native joins with the transition animator (until then weights snap, the documented fence).</summary>
  ```

### B15 Spinner · documented-deviation · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Primitives/Nodes/Spinner.cs`
- **Handoff**: Appears only after a 400ms delay (skip flash for fast ops).
- **Code**: Spinner.AppearDelayMs has exactly one consumer, the generated web stylesheet (TokenCss.cs:428). PhotonRealizer.EmitSpinner (PhotonRealizer.cs:1626-1653) paints the bars from frame 0 with no appear gate, so the native spinner flashes on fast operations. The Spinner doc names the fence: the delay "is generated CSS on web and joins the native transition animator".
- **Evidence**:

  ```
  TokenCss.cs:428  css.AppendLine($".eq-spinner {{ opacity: 0; animation: eq-appear 1ms linear {Spinner.AppearDelayMs}ms forwards; }}");
  PhotonRealizer.cs:1629  motion.Active = true;   // EmitSpinner — no AppearDelayMs anywhere
  ```

### B16 Skeleton · semantics · **CONFIRMED**

- **Component**: `src/eQuantic.UI.Components/EmptyState.cs`
- **Handoff**: A11y: shapes hidden; the region announces "loading content" once, then "loaded".
- **Code**: The returned Box carries no hidden marker — the only place the web realizer emits aria-hidden is the Spacer (WebRealizer.cs:2106), the icon/vector SVGs (745, 920) and the spinner (677). Boxes and flex containers never get it, so the shapes are present in the a11y tree (empty, but present), and no region announcement mechanism exists at all.
- **Evidence**:

  ```
  EmptyState.cs:133  return new Box(new BoxStyle { Width = Width, Height = height, Background = theme.SurfaceSubtle, CornerRadius = new CornerRadii(radius), Clip = true }, new LoopMotion(...));
  ```

### C1 BottomSheet · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/BottomSheet.cs`
- **Handoff**: The pointer/expanded tier presents a Dialog (C2) or anchored popover instead — the sheet is the touch presentation.
- **Code**: BottomSheet.Build never reads context.Density (the mechanism exists and is used by Chip, TextInput, Switch, IconButton, Select, …), so a pointer target gets the same bottom sheet as a finger. No presentation swap exists.
- **Evidence**:

  ```
  BottomSheet.cs:28    public override VisualNode Build(ComponentContext context)
  ```

### C1 BottomSheet · semantics · **unverified**

- **Component**: `src/eQuantic.UI.Components/BottomSheet.cs`
- **Handoff**: HANDOFF BLOCK: C1 BottomSheet (and C4 Toast)
- **Code**: Doc-comment cross-reference (not ARIA): BottomSheet's summary cites "spec C4" (BottomSheet.cs:6) and Toast's cites "spec C3" (Toast.cs:5) — in this handoff C4 is Toast and C3 is ActionSheet. The two components are documented against a stale numbering, which is how a future audit against the wrong block starts.
- **Evidence**:

  ```
  BottomSheet.cs:6  /// The design system's BottomSheet (spec C4): a MODAL surface anchored to the bottom edge — top
  Toast.cs:5  /// The design system's Toast/Snackbar (spec C3): a TRANSIENT, NON-MODAL notice anchored to the
  ```

### C2 Modal · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/Dialog.cs`
- **Handoff**: A11y: alertdialog role — name = title, description = body  (the block's own Semantics line instead says: role=dialog + aria-modal=true, named by the title)
- **Code**: The role is CONDITIONAL: alertdialog only when some action is Variant.Destructive, otherwise dialog (Dialog.cs:123 → WebRealizer.cs:988). The doc comment names and justifies the split — "role (alertdialog when an action is destructive)" (Dialog.cs:15) and "the assertive announcement a destructive confirm deserves" (Dialog.cs:118-119). Flagging it because the block's two sections disagree with each other, so somebody should say which one is normative.
- **Evidence**:

  ```
  Dialog.cs:123            Alert = Actions.Any(action => action.Variant == Variant.Destructive),
  ```

### C2 Modal · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/Dialog.cs`
- **Handoff**: exit ⅔ accelerate  (⅔ of the 200ms enter = 133ms)
- **Code**: The exit animation runs var(--eq-motion-fast) = Motion.FastMs = 100ms, not ⅔ of the enter. Primitives already expose the correct value, Motion.ExitFor(200) = 133 (Tokens.cs:248) and Motion.Exit (Tokens.cs:264), and the presence CSS ignores both. Same rule, same miss, for the C1 sheet and the C4 toast exits (.eq-presence-exit-slideup).
- **Evidence**:

  ```
  TokenCss.cs:418        css.AppendLine(".eq-presence-exit-fade { animation: eq-presence-exit-fade var(--eq-motion-fast) ease-in forwards; }");
  ```

### C2 Modal · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/Dialog.cs`
- **Handoff**: body 15 TextSecondary, gap 8  (no line limit stated)
- **Code**: The body is clamped to 6 lines (maxLines: 6, Dialog.cs:52) — a limit the block does not specify. Size (BodyM = 15, PhotonTheme.cs:118), colour and the 8dp gap all match; only the undeclared truncation is extra, and on a long destructive-confirm explanation it silently drops text.
- **Evidence**:

  ```
  Dialog.cs:52        content.Add(new Text(Body, TypeRole.BodyM, theme.TextSecondary, maxLines: 6));
  ```

### C4 Toast · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/Toast.cs`
- **Handoff**: Queue: one visible, FIFO
- **Code**: No queue exists — the component is pure declarative presence, so an app that builds two Toasts renders two stacked pills. The doc comment names the fence: "v1 fences: enter/exit motion (state-transition system) and toast queueing (one at a time)" (Toast.cs:12).
- **Evidence**:

  ```
  Toast.cs:12  /// v1 fences: enter/exit motion (state-transition system) and toast queueing (one at a time).
  ```

### C4 Toast · semantics · **unverified**

- **Component**: `src/eQuantic.UI.Components/Toast.cs`
- **Handoff**: Rules: Inverse surface ... padding 12/16 · text 14/500, ≤ 2 lines · one action max  (the block's anatomy is dot-free: message + optional action)
- **Code**: The component adds an anatomy slot the block does not have: a leading 8×8 status dot tinted by a Variant Status parameter (Toast.cs:16, 36-41), which also introduces a Status axis the block never specifies. Reported as a note, not a defect — the padding (Symmetric(S4=16, S3=12) = 12 vertical / 16 horizontal), the inverse fill (theme.TextPrimary) and E3 all match exactly.
- **Evidence**:

  ```
  Toast.cs:36-41        row.Add(new Box(new BoxStyle
          {
              Width = 8, Height = 8,
              Background = theme.Colors(Status).Base,
              CornerRadius = new CornerRadii(Radius.Full),
          }));
  ```

### C5 Drawer · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/Drawer.cs`
- **Handoff**: "20dp edge-swipe capture opens, follows finger 1:1 … Release glide — 200ms smoothstep (§06)" — i.e. the panel arrives by lateral slide from its edge.
- **Code**: The panel is wrapped in Presence, which fades it in; the class doc names the gap and the reason: "v1 fences: lateral slide motion (PresenceMotion gains SlideStart with the motion pack)" — the slide is deferred until the motion pack ships SlideStart.
- **Evidence**:

  ```
  Drawer.cs:19  /// v1 fences: lateral slide motion (PresenceMotion gains SlideStart with the motion pack),
  Drawer.cs:69  ? new Positioned(new Presence(panel), top: 0, bottom: 0, start: 0)
  ```

### C6 SegmentedControl · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/SegmentedControl.cs`
- **Handoff**: 2–4 segments, short text only — labels must fit untruncated at ×1.3 Dynamic Type or the component asserts
- **Code**: the class doc states a ceiling of FIVE, not four, and gives its reason — "past five segments the labels stop fitting and the control becomes a Select" (SegmentedControl.cs:6-9). Neither ceiling is enforced: Build never inspects Segments.Count for a limit, and the fit rule is handled by silent truncation (maxLines: 1, line 65) rather than an assert.
- **Evidence**:

  ```
  SegmentedControl.cs:6-9  /// A single choice out of two to five, all of them VISIBLE (spec B9) ... past five segments the labels stop fitting and the control becomes a Select.
  SegmentedControl.cs:65  label.Add(new Text(Segments[index], TypeRole.Label, selected ? theme.TextPrimary : theme.TextSecondary, maxLines: 1)
  ```

### C7 Slider · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/Slider.cs`
- **Handoff**: track-tap jumps with Base animation
- **Code**: a track press moves ONE step toward the press instead of jumping to the pressed position (Slider.cs:70-74). The class doc names and justifies it: "Pressing the track moves ONE step toward the press — the scrollbar's page-click, which is what a track press means everywhere else" (lines 12-14).
- **Evidence**:

  ```
  Slider.cs:70-71  row.Add(new Flexible(TrackHalf(fill, filled: true, enabled: !Disabled, onPressed: () => OnChanged?.Invoke(Math.Max(Min, Value - step))), Weight(fraction)));
  ```

### C7 Slider · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/Slider.cs`
- **Handoff**: snaps on release (Release glide — 200ms smoothstep (§06); velocity spring still REQUEST)
- **Code**: quantization happens on EVERY move rather than on release — Slider.cs:88 quantizes each frame and Draggable.OnReleased is never wired, so there is no release glide at all. The Quantize doc names the choice: "A scrub lands on the same values a press does — a stepped slider has no in-between positions, however finely the finger moves" (lines 113-114).
- **Evidence**:

  ```
  Slider.cs:88  OnMoved = f => OnChanged?.Invoke(Quantize(Min + f * span, step)),
  ```

### C8 Stepper · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/Stepper.cs`
- **Handoff**: Hit: 48dp per half, split at cell boundary.
- **Code**: the 48dp hit expansion the Pressable contract promises (VisualNode.cs:1063-1066) is implemented only in the native realizer, PhotonRealizer.ExpandHitRect (PhotonRealizer.cs:1658-1666). The web realizer emits the visual box as the button's box with no min-width/min-height (WebRealizer.cs:1789-1800), the TS twin does the same (lowering.ts:2053-2063), and the generated .eq-pressable rules add none (TokenCss.cs:317-332) — so on the web an arm's hit rect is its visual 40×40. The same gap defeats C6's "Whole control = one hit strip (≥ 48 with slop)".
- **Evidence**:

  ```
  PhotonRealizer.cs:1658  private static Rect ExpandHitRect(Rect bounds, Density density = Density.Comfortable)   // native only
  WebRealizer.cs:1791-1799  Padding = "0", Border = "none", Background = "none", ... Width = fills.Width ? "100%" : null, Height = fills.Height ? "100%" : null,
  TokenCss.cs:317  css.AppendLine(".eq-pressable { -webkit-tap-highlight-color: transparent; }");
  ```

### C9 PullToRefresh · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/PullToRefresh.cs`
- **Handoff**: Platform flag: iOS translates content with the pull; Android floats the circle over static content.
- **Code**: There is no platform flag on the component. The content is always the Draggable's child, so it always translates with the pull on every target (PullToRefresh.cs:53); the Android "floating circle over static content" mode is unreachable.
- **Evidence**:

  ```
  stack.Add(new Draggable(content, OnReleased)
  ```

### C10 SwipeableRow · semantics · **unverified**

- **Component**: `src/eQuantic.UI.Components/SwipeableRow.cs`
- **Handoff**: every swipe action must mirror as a custom accessibility action on the row ... announce "actions available".
- **Code**: The mirroring half is honoured — the action is a real Pressable with an accessible name (SwipeableRow.cs:59-67). The announcement half is not: nothing on the row carries a Label or a status/live node (the row surface at lines 69-73 is a bare Box).
- **Evidence**:

  ```
  var surface = new Box(new BoxStyle
  {
      Width = SizeValue.Fill,
      Background = theme.Surface,
  }, Child);
  ```

### C10 SwipeableRow · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/SwipeableRow.cs`
- **Handoff**: One row open at a time; outside tap / scroll / another swipe closes it.
- **Code**: The row does not enforce it. Open is the caller's state and OnOpenChanged only reports the release (SwipeableRow.cs:41-42, 90-91). The doc comment names and justifies this: "A list that allows one open row at a time is a caller that closes the others — the row does not police it."
- **Evidence**:

  ```
  public bool Open { get; init; }
  public Action<bool>? OnOpenChanged { get; init; }
  ```

### C10 SwipeableRow · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/SwipeableRow.cs`
- **Handoff**: Release snaps to n×72 or closed (… by position + velocity).
- **Code**: The snap decision is position-only — Draggable.OnReleased hands over a single offset float (src/eQuantic.UI.Primitives/Nodes/VisualNode.cs:776) and the web controller reports only `travelOf(ev)` with no velocity (src/eQuantic.UI.Runtime/src/dom/draggable.ts:108). A slow drag to 40% and a fast flick to 40% behave identically. (The handoff brackets the velocity SPRING as REQUEST; the velocity term of the snap decision is separate.)
- **Evidence**:

  ```
  private void OnReleased(float offset) =>
      OnOpenChanged?.Invoke(offset <= -ActionWidth / 2);
  ```

### C11 Accordion · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/Accordion.cs`
- **Handoff**: Height animates Base standard (an explicit layout-animation exception, like Banner); content fades in over the last 100ms.
- **Code**: The content Box is simply added to or omitted from the Column on toggle (Accordion.cs:83-90) — it appears and disappears instantly, with no height transition and no fade. The doc comment names the gap: "v1 fences: height animation (expand rides the state-transition system later), controlled mode, disabled sections."
- **Evidence**:

  ```
  if (open && item.Content is { } content)
  {
      column.Add(new Box(new BoxStyle
  ```

### C12 PageIndicator · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/PageIndicator.cs`
- **Handoff**: Display-only in v1 (no tap-to-jump — 8dp dots can't honor 48dp hits honestly)
- **Code**: Tap-to-jump ships: passing OnSelected turns each dot into a Pressable with a 48dp-tall hit wrapper (PageIndicator.cs:65-71, 88). The doc comment names the choice and gives its reason: "It is a readout of the pager's state, never the primary way to move through it; the dots take taps only as a courtesy, so OnSelected is optional."
- **Evidence**:

  ```
  row.Add(OnSelected is null
      ? dot
      : new Pressable(HitPadded(dot), () => OnSelected(index))
  ```

### C12 PageIndicator · missing-feature · **unverified**

- **Component**: `src/eQuantic.UI.Components/PageIndicator.cs`
- **Handoff**: Active pill grows / previous shrinks Base standard, interpolating with carousel drag fraction (worm effect — two rrects, engine-honest).
- **Code**: The Base standard transition is there — Motion.State is 200ms on Curve.Standard (src/eQuantic.UI.Primitives/Theme/Tokens.cs:258) — but it animates only on a discrete CurrentIndex change. The component takes no drag fraction (its inputs are Count, CurrentIndex, OnSelected, Variant — lines 27-32), so the pill cannot track a carousel mid-swipe and the worm effect is unreachable.
- **Evidence**:

  ```
  Transition = TransitionSpec.Of(StyleChannels.Colors | StyleChannels.Size, Motion.State),
  ```

### C13 Tooltip · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/Tooltip.cs`
- **Handoff**: "Enter: fade + 2dp rise, Fast 100ms"
- **Code**: The hover-reveal rule fades opacity over 120ms ease-out with no transform, so the enter runs 20ms longer than the §06 Fast rung (Motion.FastMs = 100) and the 2dp rise never happens.
- **Evidence**:

  ```
  TokenCss.cs:367  css.AppendLine(".eq-hoverreveal > .eq-anchor-panel { opacity: 0; pointer-events: none; transition: opacity 120ms ease-out; }");
  Tokens.cs:239  public const int FastMs = 100;
  ```

### C13 Tooltip · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/Tooltip.cs`
- **Handoff**: "Opens after ~500ms hover and on focus-visible" · "long-press 500ms"
- **Code**: There is no delay — the CSS rule reveals the panel the instant :hover or :has(:focus-visible) matches. The class doc names it as a deliberate v1 fence: "v1 fences: show/hide delay, arrow caret."
- **Evidence**:

  ```
  Tooltip.cs:16  /// v1 fences: show/hide delay, arrow caret.
  TokenCss.cs:368  css.AppendLine(".eq-hoverreveal:hover > .eq-anchor-panel { opacity: 1; }");
  ```

### C13 Tooltip · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/Tooltip.cs`
- **Handoff**: "No caret arrow in v1 — proximity + shadow anchor it (flips below near the top edge)."
- **Code**: Placement is fixed at TopCenter and never flips; the panel is positioned by a static CSS rule (bottom:100%). The AnchorPlacement doc names the reason: "wave 3 v1: the four corner placements; centered variants and viewport flip/clamp are the positioning fence."
- **Evidence**:

  ```
  Tooltip.cs:28  public AnchorPlacement Placement { get; init; } = AnchorPlacement.TopCenter;
  VisualNode.cs:532-533  /// <summary>Where an <see cref="Anchored"/> panel attaches relative to its anchor (wave 3 v1: the
  /// four corner placements; centered variants and viewport flip/clamp are the positioning fence).</summary>
  ```

### C14 Select · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/Select.cs`
- **Handoff**: "Trigger inherits TextInput (B9) anatomy/states, trailing chevron-down; value 15, placeholder TextMuted."
- **Code**: The two components do not measure alike: Select is SizeVariant.Medium (40dp) with 12dp horizontal padding, while TextInput defaults to SizeVariant.Large (48dp) with 14dp. The value type (BodyM = 15) and the TextMuted placeholder do match. Select's doc names 40dp but does not state it as a departure from B9.
- **Evidence**:

  ```
  Select.cs:63-66  Height = Sizing.Height(SizeVariant.Medium, context.Density),
              Width = SizeValue.Fill,
              Padding = EdgeInsets.Symmetric(Space.S3, 0),
  TextInput.cs:22  Icons? leading = null, SizeVariant size = SizeVariant.Large)
  TextInput.cs:86  var paddingX = _focused ? 13f : 14f;
  ```

### C14 Select · documented-deviation · **unverified**

- **Component**: `src/eQuantic.UI.Components/Select.cs`
- **Handoff**: "Multi-select: Checkbox rows + full-width "Apply" XLarge footer." · "type-ahead jumps"
- **Code**: Neither exists — SelectedIndex is a single int and there is no Apply footer or checkbox row. The class doc names both as deliberate v1 fences: "v1 fences: typeahead, option groups, multi-select, disabled options."
- **Evidence**:

  ```
  Select.cs:17  /// v1 fences: typeahead, option groups, multi-select, disabled options.
  Select.cs:37  public int SelectedIndex { get; private set; }
  ```

### C15 DatePicker · missing-component · **unverified**

- **Component**: `MISSING`
- **Handoff**: new DatePicker(selected, onChanged, min?, max?, mode: DateMode.Single | Range) — "NOT YET IN SDK · REQUEST … DatePicker is not in eQuantic.UI.Components yet." The block then specifies the full contract: 36dp/48-hit chevron IconButtons, Title 15/600 header, dow row Caption 11/700, 44x44 day cells with 13 tnum numerals, selected = Primary Full circle + OnPrimary 700, today = 1.5dp Primary inner ring, out-of-range 38%, range bands, year grid 12/screen, x1.15 Dynamic Type clamp, grid/gridcell + aria-selected, arrows/PgUp/PgDn/Home/End/Enter/Esc.
- **Code**: There is no DatePicker.cs in src/eQuantic.UI.Components/ and no DatePicker type anywhere in the repo — the only occurrence in any source or doc is a forward reference in the i18n plan. The handoff's own status marker is therefore accurate, and none of the checkable figures above have an implementation to compare against.
- **Evidence**:

  ```
  docs/I18N-PLAN.md:256  culture-shaped data (the C15 DatePicker when it lands, any calendar/number surface): they are
  docs/I18N-PLAN.md:413  components (DatePicker is born from `DateTimeFormatInfo`). Still design-only;
  ```

### C16 NavigationRail · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/NavigationRail.cs`
- **Handoff**: "Caption 12 label beneath … selected = … label 700" — selection changes the WEIGHT of the Caption row, nothing else.
- **Code**: The selected override is `new TypeStyle(12, 16, FontWeight.Bold, 0, 1.3f)`, which also drops the tracking from the Caption role's 0.2 to 0 (PhotonTheme.cs:120: `TypeRole.Caption => new TypeStyle(12, 16, FontWeight.Medium, 0.2f, 1.3f)`). Unselected labels keep 0.2 tracking, so within one rail the labels are set at two different letter-spacings and the selected label's advance width shifts by more than the weight change alone. Size 12 and line height 16 do match.
- **Evidence**:

  ```
  StyleOverride = isActive ? new TypeStyle(12, 16, FontWeight.Bold, 0, 1.3f) : null,
  ```

### C16 NavigationRail · behaviour · **unverified**

- **Component**: `src/eQuantic.UI.Components/NavigationRail.cs`
- **Handoff**: "exactly one selected, always. 3–7 destinations (B4's rule)"
- **Code**: The count half of that sentence is enforced (Items validates `value.Count is < 3 or > 7` at line 48), but Selected is an unchecked int: any index outside 0..Items.Count-1 renders a rail where `i == Selected` is never true, i.e. no pill, no bold label and no aria-current on any destination — a rail with nothing selected, which the block forbids. The asymmetry is inside one file: the same component validates its other B4 invariant in a property initializer.
- **Evidence**:

  ```
  public int Selected { get; init; }
  ```

### C16 NavigationRail · metric · **unverified**

- **Component**: `src/eQuantic.UI.Components/NavigationRail.cs`
- **Handoff**: "Item cell 80×56, hit = the full cell: 52×30 pill + Caption 12 label beneath, icon Dense 20 centered"
- **Code**: The CODE is correct (Width = 52 / Height = 30 at lines 97-98, IconSize.Dense at line 92, and the inline comment at lines 88-91 states C16's figures accurately). The class doc comment is stale and states the opposite — it claims the rail draws the BAR's pill and that a differing shape would be a bug, which is exactly the divergence a later fix removed from the code. Anyone reading the contract before the body gets the wrong number.
- **Evidence**:

  ```
  /// 80dp wide, destinations from the top, each one a 56×26 pill + label exactly as the bar draws it:
  /// a destination that changed shape between the two would read as a different app after a rotation.
  ```

## Tally

| Severity | Confirmed | Refuted | Unverified |
| --- | --- | --- | --- |
| visible | 13 | 0 | 70 |
| subtle | 37 | 2 | 87 |
| note | 23 | 3 | 59 |
