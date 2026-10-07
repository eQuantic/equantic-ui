# Design

## How Flutter answers it

A control resolves its look per state: `ButtonStyle` takes a `WidgetStateProperty` for each property,
resolved against the set of states the control is in (hovered, focused, pressed, …), and the states
belong to the CONTROL, the `InkWell` or the button, never to a box inside it. The `WidgetStateProperty`
row of `docs/FLUTTER-PARITY.md` records that this SDK resolves per state instead, with a diff. This
change makes the per-state answer follow Flutter's ownership: pressed and focused are the control's
states, and the boxes inside the control show them.

## Decisions

### Hover is the box's state, pressed and focus are the control's

A pointer is over a box, so `Hover` stays the box's own state (CSS `:hover` already rises through the
ancestors, and Photon's hover chain does the same). A press and a focus happen to a CONTROL, the
`Pressable`, so `Pressed` and `Focus` apply to every box inside the control while it is in that
state: the surface that scales on press, and anything inside it that says how it looks then.

- **Web**: two rule families in both atomizers, beside `:hover` and the scrolled one. Focus is
  `.eq-pressable:focus-visible .cls`, pressed is `.eq-pressable.eq-pressable:active .cls`. Only an
  enabled control carries `eq-pressable`, so a disabled one shows neither, which is the handoff's
  "disabled mutes everything". The class hash takes the family's name, as the scrolled family's does.
- **Photon**: the press scope gains two flags, set while the pressed or the focused `Pressable`'s
  subtree is emitted and restored after it, the way a `Simulated` node sets its states for its
  subtree. The box's effective style reads them.

The two differ where controls nest: CSS's `:active` and the descendant combinator reach an outer
control's boxes when an inner one is pressed, and Photon presses only the control the pointer hit.
That is today's difference for `PressedBackground` too, and nesting a pressable in a pressable is
rare.

### Pressed beats focus beats hover, by specificity

The handoff orders pressed over hover. On the web the order is the selectors' specificity, never
their order in the sheet, because the server writes its rules sorted by class and the browser adds
them in the order it lowers: hover is (0,2,0), focus (0,3,0) and pressed (0,4,0), the control's
class written twice for it. On Photon the effective style lays the diffs over the base in the same
order. `PressedBackground` keeps its `!important` and stays the strongest fill.

### The focus ring is a slot in the box's shadow list

The ring is the handoff's double ring, 2dp of Surface then 2dp of Focus outside the control, and on
the web it was a `box-shadow` that replaced the box's own. An `outline` would keep the shadows but
cannot draw the Surface gap, so the ring keeps its shape and moves INTO the list: every list the web
writes for a box starts with `var(--eq-ring, 0 0 #0000)`, a transparent shadow, and the focus rule
sets `--eq-ring` to the ring on the control's first child. A box with no shadow of its own gets the
ring from a `:where()` rule of zero specificity, which any list of the box's outranks. The property
is registered with `inherits: false`, so a shadowed box inside a focused control draws no ring of its
own. A hover's or a press's list carries the slot too, so the ring stays while the focused control is
hovered or pressed.

### Photon's custom shadows glide as one list

The elevation's shadow already glides as its components. The custom shadows glide the same way, by
position in the list, and a list that grows or shrinks pads the shorter one with transparent zero
shadows, which is how CSS interpolates `box-shadow`. The store keeps a track per position, so a
shadow that leaves fades from where it was.

### What stays out

- Photon has no held-key press: a key activates its control on the way down and draws no pressed
  state, so a `Pressed` diff shows for a held pointer there and for a held key too on the web.
- A descendant that reacts to its control's HOVER, the inherited foreground of #498 and a translation
  relative to the box's own size are #614's.
