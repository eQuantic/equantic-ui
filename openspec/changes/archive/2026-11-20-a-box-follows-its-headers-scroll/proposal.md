## Why

#1769, a user story of #1035 (any design system in C#, complete on the web and on native, with no CSS).

`Pinned.ScrolledStyle` styles a pinned header itself while the surface it pins to has scrolled, and
nothing inside the header can follow that state. A site header that swaps what it shows as the page
scrolls (a large badge hanging past the bar at the top of the page, which shrinks and fades out while a
small mark with the brand's name fades in beside it) had one way left: a scroll callback that keeps the
offset in a field and rebuilds the page at every crossing. A page of `StatelessComponent`s keeping its
state in page fields pays for that with a rebuild of the whole tree, and the swap jumps instead of
gliding.

## What Changes

- **`BoxStyle.Scrolled`**, a `StyleDiff`, beside `Hover`, `Focus` and `Pressed`: laid over the box while
  the nearest `Pinned` around it is scrolled, which is the moment that header's own `ScrolledStyle`
  applies (its surface past `Pinned.ScrolledThreshold`). Every member of the diff applies, through the
  builder the other states use. Inside no header it never applies.
- The box's own `Transition` glides it in and out, as it glides a hover.
- **Order**: a control's states are laid over it. The box's resting style, then `Scrolled`, then
  `Hover`, `Focus` and `Pressed`, member by member, so a link in a scrolled header still lights under
  the pointer.
- **Web**: a rule of a new state family, `header-scrolled`, selecting on the nearest header's
  `data-eq-scrolled`, the attribute the runtime already sets for `ScrolledStyle`. Its weight (0,1,1)
  sits between the box's resting class (0,1,0) and a hover (0,2,0). The runtime's scroll controller is
  installed by a box that declares the diff, so a header without a `ScrolledStyle` of its own is
  tracked too. No script runs per box.
- **Photon**: the emit walk carries the nearest header's scrolled state to every box under it, and
  `EffectiveStyle` lays the diff first.

An app writes:

```csharp
Pinned(Stack(children:
[
    Box(new BoxStyle
    {
        Transition = new TransitionSpec(StyleChannels.Opacity | StyleChannels.Transform),
        Scrolled = new StyleDiff { Opacity = 0, Transform = Transform2D.Scale(0.6f) },
    }, badge),
    Box(new BoxStyle
    {
        Opacity = 0,
        Transition = new TransitionSpec(StyleChannels.Opacity),
        Scrolled = new StyleDiff { Opacity = 1 },
    }, markAndName),
]))
```

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `interaction-states`: a box follows the scrolled state of the header it is in.

## Impact

- **Public surface**: `BoxStyle.Scrolled` (Primitives). No member goes; nothing an app compiles today
  changes. The developer surface does not move.
- **Web realizer**: `StyleSink` gains the `header-scrolled` family in `StateSelector`, and `LowerBox`
  appends the diff. `StyleSink`'s public surface does not move.
- **Runtime**: the `BoxStyle` twin, the `header-scrolled` family in `stateSelector`, `lowerBox`, and the
  scroll controller installed from a box. The served runtime grows by a few hundred bytes.
- **Photon**: `PressScope.InScrolledHeader`, set by the pinned header's visit, read by `EffectiveStyle`.
- **eqc**: none. A `BoxStyle` initializer crosses as a config object, member by member.
- **Docs**: the wiki's styling page in its five languages, and a ledger entry.

## Principles

- **5, the vocabulary speaks no target's language**: the member is named for the state, as `Hover` and
  `Pressed` are, and no CSS reaches the app.
- **6, how Flutter solves it**: see the design; Flutter's `WidgetState.scrolledUnder` resolves the app
  bar's own properties only, and a descendant follows the scroll through a listener and a rebuild.
- **15, customized all the way**: a capability of every box, so a ready-made component built from boxes
  can follow its header as the app's own boxes do.
