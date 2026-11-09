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

#### Scenario: A discovery round names the kept instance

- **WHEN** the server's expansion scope is armed while a frame is lowered in which a page hands a
  stateful child's position a fresh instance
- **THEN** the scope names the instance the position keeps, not the fresh one

### Requirement: An element hands its children to a visitor

`Element.VisitChildren(visitor)` SHALL hand the visitor every child position the element keeps, in
slot order, passing over a slot that holds nothing (a layer not shown yet). It SHALL be the only method
of that name on each kind of element, written so eqc transpiles it without a diagnostic.

#### Scenario: The method crosses to the runtime

- **WHEN** the text of `VisitChildren` in `Element.cs`, `NodeElement.cs` and `ComponentElement.cs` is
  run through eqc in a class holding the same fields
- **THEN** eqc reports nothing, and each kind's module holds one `visitChildren`

### Requirement: A host marks a position to build again

`ComponentElement.MarkNeedsBuild()` SHALL be public, so a host that changed what a component reads
without a `SetState` (a prefetch that loaded its fields) asks for that position alone to build again,
with the instance it keeps.

#### Scenario: A prefetch that loaded

- **WHEN** a stateless component a page builds anew on every build loads its data, its position is
  marked, and the owner builds
- **THEN** only that position builds, with the instance that loaded, and draws what it loaded
