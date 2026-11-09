# Proposal

Closes #616, a Task under #504.

## Why

Since #508 a pressed fill can be said two ways, and each has its own machinery on each target:

- `Pressable.PressedBackground`: on the web a per-element custom property (`--eq-pressed-bg`), two
  `!important` rules in the generated stylesheet and an `eq-press-fill` marker on the control; on
  Photon `PressScope.PendingFill`, consumed by the first box drawn under the pressed control.
- `BoxStyle.Pressed = new StyleDiff { Background = … }`: an atomic rule in the pressed family on the
  web, and the effective style on Photon.

The defect #508 fixed (a control with no pressed fill went transparent while pressed on the web)
lived only in the first mechanism, and every state feature after it has to be taught to both: #614,
the next one, is a descendant reacting to its control's state. Two mechanisms for one fact is the
shape this repository removes rather than keeps.

## What Changes

- The components that set `PressedBackground` (`Button`, `IconButton`, `Chip`, `ListItem`, `Tabs`,
  `NavigationRail`, `BottomNavigation`, `SearchField`) set their surface's
  `Pressed = new StyleDiff { Background = … }` instead, on the box the handoff names. Five of them
  (`Button`, `IconButton`, `Chip`, `ListItem`, the search field's clear button) fill the box that
  was already the pressed control's first child, so they draw the same fill in the same place, on
  both targets, as they did.
- Three did not agree between the targets, because each mechanism chose its own box: the web filled
  the control's first child, and Photon the first box it DREW under the control. A tab's first child
  is its whole cell and its first box drawn is the 3dp strip under its label: measured on main, a
  pressed tab painted 133x3 on Photon and its whole cell on the web. A navigation item's first child
  is the whole item and its first box drawn is its pill. The handoff settles both: "Pressed:
  SurfaceSubtle fill on the tab cell", and of the navigation bar, whose twin the rail is, "Press-down
  shows the pill". So a tab wraps its cell in a surface that carries the fill, and the bar and the
  rail put it on the pill: Photon's tab and the web's bar and rail change place, and a pressed pill
  now changes at once on the web, as on Photon, rather than fading in.
- The pill of the SELECTED destination takes no pressed fill. Photon's first box drawn was that
  pill too, so on main a held press traded the selected pill for `SurfaceSubtle` on Photon, which
  read as the selection going away while the finger stayed down; on the web the whole item was
  washed and the pill stayed on top. The selected pill now keeps its colour on both targets, and so
  does a selected `ListItem`, whose hover already left its selection alone.
- A control's press and focus stop at a control NESTED in it, on both targets. Once a fill was a
  member of the `Pressed` diff like the rest, pressing a row filled every button inside it, because
  both families selected every box under the control (#508); the old mechanism had hidden that for
  fills only, by reaching the control's first box alone, while a nested control's scale or elevation
  already pressed with the row. On the web the pressed family now selects the active control that
  holds no active control and leaves out a box under a control that is not active, and the focus
  family leaves out a box under a control inside the focused one, both inside a `:where()` that
  weighs nothing, so the (0,4,0) over (0,3,0) order holds. On Photon a nested control sets its own
  press and focus for its subtree. A `Simulated` picture follows the same rule, ring included. This
  also closes a difference the wiki named: on the web a pressed button pressed the row around it too. The colour stays as it was;
  where it differs from the handoff's (the pill at 40%, an unselected filter chip pressed into its
  rest colour) is #1254.
- `Pressable.PressedBackground` goes, and so does the `pressedBackground` parameter of the
  `UI.Pressable` factory. With them go the web's `--eq-pressed-bg` custom property, the
  `eq-press-fill` marker, the two `!important` rules of the generated stylesheet and the `eq-pressed`
  marker a simulated press set (its one rule was one of the two; a simulated press already lays the
  `Pressed` diff inline), and Photon's `PendingFill`.
- The order the issue asked to settle holds without the `!important`: on the web every regular
  declaration is an atomic class at (0,1,0), only custom properties stay in the `style` attribute, and
  the pressed family's selector, `.eq-pressable.eq-pressable:active .<class>`, is (0,4,0), over focus
  (0,3,0) and hover (0,2,0). On Photon `EffectiveStyle` lays hover, then focus, then press. Nothing
  else writes a background under the pressed family. A gradient was drawn over the old `!important`
  colour too, and is drawn over the diff's.
- Parts reached: the vocabulary (`eQuantic.UI.Primitives`), the components and their transpiled
  runtime twins, the web realizer and its TypeScript twin (`lowering.ts`, `vocabulary.ts`,
  `nodes.ts`), the generated theme stylesheet, and Photon's emit visitor and press scope. No SDK,
  template or shell changes. The public surface moves (`*REMOVED*` lines in two `PublicAPI` files);
  the developer surface outside C# does not.
- **A break, allowed in preview.** The migration line: a fill set with
  `pressedBackground: x` on a `Pressable` (or `PressedBackground = x`) moves to the style of the box
  it fills, as `Pressed = new StyleDiff { Background = x }`. The OS Cleaner meets it eleven times, all
  of the shape `Pressable(box, …, pressedBackground: theme.Surface)`.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `interaction-states`: a pressed fill is drawn by one mechanism, the surface's `Pressed` diff, and a
  control that declares none keeps its own fill while pressed.
