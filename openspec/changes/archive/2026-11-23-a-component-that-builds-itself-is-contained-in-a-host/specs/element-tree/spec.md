## MODIFIED Requirements

### Requirement: A frame of the tree is lowered on the web from its positions

A `MarkupHost` SHALL lower the tree its `BuildOwner` has just built, between its `Build` and its
`EndFrame`. A component SHALL be lowered from what its position built in that frame, by the
instance the position keeps, never built again. A component SHALL be found inside the component
position whose build is being lowered, so a lowering descends the positions the engine built, even
where one instance holds several of them. A layer the web lowers (an overlay's child, an
anchored panel, one kept mounted while closed for its motion included) SHALL be shown in its
position's deferred slot before it lowers; a layer the web does not lower SHALL leave the tree when
the frame ends. Of an adaptive node, the arm its element holds SHALL lower from its positions and the
other arms as a tree drawn once does. A component, or a layer's holder, that the frame has no
position for SHALL be refused with an exception. The host's components SHALL build through the error
boundary with a measurer that has no font, and the host SHALL note which build asked it, so a
component whose build asked is marked for hydration as a tree drawn once marks it. The first frame
SHALL be the markup `WebRealizer.Lower` writes for the same tree, byte for byte, hydration's
unmeasured mark included. The browser's lowering SHALL find a component the same way.

#### Scenario: The second frame of a nested stateful component

- **WHEN** a frame is lowered from a page that builds a counter, the counter's lowered click handler
  is invoked, and the next frame is built and lowered
- **THEN** the second frame reads `Count: 1`

#### Scenario: The first frame is the server's markup

- **WHEN** each case of the component parity fixture is built by a live host at `Compact`, `Medium`
  and `Expanded`, and its first frame lowered
- **THEN** each markup equals `WebRealizer.Lower`'s for the same case, the unmeasured mark included,
  and at least one case carries the mark

#### Scenario: A closed panel leaves

- **WHEN** an anchored panel holding a counter is open in one frame and closed, with no motion, in the
  next
- **THEN** the counter's `OnUnmount` runs when the second frame ends

#### Scenario: A component the frame did not build

- **WHEN** a frame is asked for what a component built that no position of it holds
- **THEN** it throws `InvalidOperationException`

#### Scenario: A discovery round names the kept instance

- **WHEN** the server's expansion scope is armed while a frame is lowered in which a page hands a
  stateful child's position a fresh instance
- **THEN** the scope names the instance the position keeps, not the fresh one

#### Scenario: A component that builds itself, drawn through a host

- **WHEN** a row holding a component whose `Build` returns itself is drawn through
  `MarkupHost.Structural` and through `MarkupHost.ForPage`, and alone, and inside a stack
- **THEN** the row's other children are drawn, the component's place shows the boundary's surface, and
  the boundary reports it once

#### Scenario: A component that builds a box around itself

- **WHEN** a component whose `Build` returns a box holding the component itself is drawn through a host
- **THEN** its place shows the boundary's surface

#### Scenario: The browser lowers the same chain

- **WHEN** the browser's lowering lowers a row holding a component whose build returns itself, and a
  stack holding it and one that builds a box around itself
- **THEN** it returns, the row's other children are drawn, the surface is drawn, and the chain is
  reported once
