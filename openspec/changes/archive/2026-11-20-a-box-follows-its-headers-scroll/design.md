## Context

A pinned header's scrolled state exists on both targets already (#506). On the web the runtime sets
`data-eq-scrolled` on every `[data-eq-pinned]` element whose surface (the nearest ancestor that scrolls,
or the page) is past 8dp, and the header's own `ScrolledStyle` rules select on it. On Photon the emit
walk sets `PressScope.SurfaceScrolled` from the nearest vertical `ScrollView`, and the header's visit
reads it. Neither carries the state below the header.

A box already has three states that belong to something around it: `Focus` and `Pressed` are its
control's (#508, #616), and since #614 so is a hover inside an enabled control. Each is a family of
rules on the web that selects on the control's state, and a flag on Photon's `PressScope` set while the
control's subtree is drawn.

## How Flutter solves it

`WidgetState.scrolledUnder` is the state an `AppBar` resolves its own background, elevation and
shadow with. It is the app bar's: nothing under it receives it. A descendant that changes with the
scroll is written with a `ScrollController` or a `NotificationListener<ScrollNotification>` and a
`setState`, which rebuilds; `SliverAppBar` and `FlexibleSpaceBar` collapse their title continuously with
the offset, through `FlexibleSpaceBarSettings`, an inherited widget, and rebuild every frame of the
scroll.

Doing better here: the state is the one `ScrolledStyle` already has, a threshold crossed rather than an
offset followed, so the app declares two looks and a transition and nothing is rebuilt at all. On the
web no script runs per box; the browser applies a rule.

## Decisions

### A member of `BoxStyle`, named for the state

`Scrolled` beside `Hover`, `Focus` and `Pressed`: one more `StyleDiff` a box lays over its base, through
`DiffDeclarations` on the web and `Over` on Photon, so every member of the diff applies and the two
paths cannot drift. A node of its own around the box (a `ScrollLinked(child, diff)`) was the other
shape, and it would be a box's style living outside the box, with its own transition to agree with the
box's. `StyleDiff` is a class, so a box that declares no `Scrolled` pays a null reference.

### The nearest header decides

As a focus and a press are the nearest control's, the scrolled state is the nearest header's: a box in
a header nested in another (a pinned rail whose own scroller pins its group headers) follows the inner
one. On Photon this is exact by construction: the header's visit sets the flag for its subtree and
restores it after. On the web the rule is

```css
html :where([data-eq-scrolled]) .eq-x:not(:where([data-eq-scrolled] [data-eq-pinned]:not([data-eq-scrolled]) .eq-x))
```

under a scrolled header, and not under a header that is not scrolled inside a scrolled one. That is the
nearest header's state for a header nested in another, both ways. A third level that alternates
(scrolled, not, scrolled) would leave the innermost box resting on the web; CSS has no "nearest
ancestor" combinator, and a selector exact at any depth would need `:has()` over a class every box with
the same declaration shares, which can match a sibling's subtree. It is stated as the rule's limit
rather than solved with a depth the lowering would have to know, which a component boundary hides.

### Its weight is between the resting class and a hover

The resting class is (0,1,0) and a hover (0,2,0), and atomic rules of equal weight are decided by the
order they reach the sheet, which nobody chooses (the server sorts by class name, the browser keeps the
order it lowered in). The scrolled family is (0,1,1): its header part rides in a `:where()`, which
weighs nothing, and the `html` before it is the one type every element of the page is under. So a
scrolled diff always beats the resting style and always loses to a control's state, by its selector
alone. Photon lays the diffs in the same order in `EffectiveStyle`.

### The family's name

`header-scrolled`, written into the hash as `header-scrolled|<declaration>`. `scrolled|<declaration>` is
the header's own `ScrolledStyle` rule, whose selector is the class AND the attribute on one element, and
one class may not stand for two rules.

### The controller is installed by whoever needs it

The runtime's scroll listener was installed when a header with a `ScrolledStyle` was lowered. A box
that declares `Scrolled` installs it too, so a header that styles nothing of its own still has its
attribute set. The listener is one per page and coalesced to one pass per frame, as before.

### No simulated state

`Simulated` pictures a control's hover, focus and press for previews. A header's scroll is not a
control's state, and the enum `SimulatedState` is append-only; it is left out until a preview asks for
it.

## Risks / Trade-offs

- A layer faded out with `Opacity = 0` still takes the pointer on both targets, as a hover's does. The
  wiki says so, and the use case wraps both layers in the one link.
- Both layers are read by a screen reader. The wiki's example names the brand once, on the link.
