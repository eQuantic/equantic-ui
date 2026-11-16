## Why

Closes #1562, a slice of #194 (pointer, gestures and focus) in the block catalog 1.0 waits for. Raw pointer
exists only on the drawing surface (`Canvas.OnPointerDown/Move/Up`): no node can hear the pointer over an
arbitrary subtree, so a component that needs it (a custom slider, a selection rectangle over a list, a
signature over a form, the custom recognizers of #1563) cannot be written in C# at all. And where a pointer
lands is the realizer's own business: the web's `pointer-events`, Photon's hit regions. An app cannot say that
a block hears the pointer in its empty space too, or hears it without hiding what is drawn behind it.

## What Changes

- **A block that hears the raw pointer**: `Listener(child)` wraps ANY node and reports the pointer over it to
  `OnPointerDown`, `OnPointerMove`, `OnPointerUp`, `OnPointerCancel`, `OnPointerHover` and `OnPointerScroll`.
  Each callback receives a `PointerEvent`: its `Phase`, its `Position` in the listener's own box, its `Kind`
  (mouse, touch, stylus), its `Buttons`, the `Modifiers` held, which `Pointer` it is, and a scroll's
  `ScrollDelta`. A pointer that went down on a listener is followed to its up or its cancel, wherever it
  moves. A listener only listens: a button inside it still presses, a scroll view inside it still scrolls.
- **Where a pointer lands is the app's to say**: `HitTestBehavior`, Flutter's three, as `Listener.Behavior`.
  `DeferToChild` (the default) hears where the child is hit, `Opaque` hears anywhere in its box and hides the
  listeners behind it, `Translucent` hears anywhere in its box and lets the listeners behind it hear too.
- **The vocabulary's pointer types grow**: `PointerPhase.Scroll`, and `PointerButtons`, flags with Flutter's
  values (primary 1, secondary 2, middle 4, back 8, forward 16).
- **Web**: one document-level controller hit-tests the listeners under every pointer event with the browser's
  own `elementsFromPoint`, and tells each one that hears it, innermost first.
- **Photon**: the paint walk records the listeners and what the frame draws and answers after the first of
  them; the host hears the same events from the presses, moves, cancels and scrolls its shells already
  deliver, and a shell's secondary click as a secondary press.

For someone using the SDK: one wrapper in C# gives any subtree the pointer, on every target, with Flutter's
callbacks and Flutter's hit-test behaviours, and no JavaScript.

## Capabilities

### New Capabilities

- `raw-pointer`: any subtree hears the raw pointer, and an app chooses how it is hit.

### Modified Capabilities

None.

## Impact

- **Vocabulary**: `Listener`, `PointerEvent`, `PointerButtons`, `HitTestBehavior`, `PointerPhase.Scroll`; one
  visitor member, declared in each project's `PublicAPI.Unshipped.feat-any-subtree-hears-the-raw-pointer.txt`,
  additions only. The node count of the coverage suites grows by one.
- **Components**: the `UI.Listener` factory.
- **Web realizer and its twin**: the listener's mark and event; `dom/pointer-listener.ts`, and the hit rule in `dom/listener-hits.ts`.
- **Photon** (experimental): the paint walk's listener and target records, and the host's dispatch from the
  phases the shells already deliver. `PhotonHost.ContextRequest` widens to take the button and the keys the shell
  saw, both optional, and the macOS shell passes the primary button and Control for a Control-click.
- **eqc, SDKs, templates**: nothing. The developer surface does not move.
- **Docs**: the wiki's Components page in every language, two parity rows, a ledger entry.

No break. Rules of `docs/PRINCIPLES.md` followed: 4 (written once, realized per target), 5 (Flutter's names,
none of the web's), 6 (Flutter's algorithm, stated for both targets), 14, and the composite rule: a block over
any child, not a member of `Pressable`.
