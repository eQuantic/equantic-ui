## ADDED Requirements

### Requirement: A frame of the tree is lowered on the web from its positions

`WebRealizer.LowerFrame` SHALL lower the tree a `BuildOwner` has just built, between its `Build` and
its `EndFrame`. A component SHALL be lowered from what its position built in that frame, by the
instance the position keeps, never built again. A layer the web lowers (an overlay's child, an
anchored panel, one kept mounted while closed for its motion included) SHALL be shown in its
position's deferred slot before it lowers; a layer the web does not lower SHALL leave the tree when
the frame ends. Of an adaptive node, the arm its element holds SHALL lower from its positions and the
other arms as a tree drawn once does. A component, or a layer's holder, that the frame has no
position for SHALL be refused with an exception. The first frame SHALL be the markup `WebRealizer.Lower`
writes for the same tree, byte for byte, save hydration's unmeasured mark, which a frame does not
write.

#### Scenario: The second frame of a nested stateful component

- **WHEN** a frame is lowered from a page that builds a counter, the counter's lowered click handler
  is invoked, and the next frame is built and lowered
- **THEN** the second frame reads `Count: 1`

#### Scenario: The first frame is the server's markup

- **WHEN** each case of the component parity fixture is built by a live owner at `Compact`, `Medium`
  and `Expanded`, and its first frame lowered
- **THEN** each markup equals `WebRealizer.Lower`'s for the same case with `data-eq-unmeasured` taken
  out, and the frame's carries no such mark

#### Scenario: A closed panel leaves

- **WHEN** an anchored panel holding a counter is open in one frame and closed, with no motion, in the
  next
- **THEN** the counter's `OnUnmount` runs when the second frame ends

#### Scenario: A component the frame did not build

- **WHEN** a frame is asked for what a component built that no position of it holds
- **THEN** it throws `InvalidOperationException`
