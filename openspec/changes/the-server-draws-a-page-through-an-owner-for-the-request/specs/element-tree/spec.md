## MODIFIED Requirements

### Requirement: A frame of the tree is lowered on the web from its positions

A `MarkupHost` SHALL lower the tree its `BuildOwner` has just built, between its `Build` and its
`EndFrame`. A component SHALL be lowered from what its position built in that frame, by the
instance the position keeps, never built again. A layer the web lowers (an overlay's child, an
anchored panel, one kept mounted while closed for its motion included) SHALL be shown in its
position's deferred slot before it lowers; a layer the web does not lower SHALL leave the tree when
the frame ends. Of an adaptive node, the arm its element holds SHALL lower from its positions and the
other arms as a tree drawn once does. A component, or a layer's holder, that the frame has no
position for SHALL be refused with an exception. The host's components SHALL build through the error
boundary with a measurer that has no font, and the host SHALL note which build asked it, so a
component whose build asked is marked for hydration as a tree drawn once marks it. The first frame
SHALL be the markup `WebRealizer.Lower` writes for the same tree, byte for byte, hydration's
unmeasured mark included.

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

## ADDED Requirements

### Requirement: The server draws a write-once page through an owner for the request

The server SHALL draw a write-once page through a structural `MarkupHost` it keeps for the request,
each prefetch round a frame of that one tree, and a client navigation's discovery of it SHALL run
the same rounds through the same kind of host, keeping each component's wire form as it loads. After a frame, `ServerDataRounds` SHALL name the
components to ask: each that loads data, that the frame reached and that was not asked, in the order
the frame named them, save a stateless one below another asked in the same round, which SHALL wait for
the next. A component that loaded SHALL have its positions marked, and only those subtrees SHALL build
in the next frame, with the instance that loaded. A bound of five rounds SHALL count a round that
reached a position, or a component holding none, that no round before it had; past it the page SHALL
be drawn with what it has, its markup and payload agreeing. The markup and the hydration payload SHALL
be byte for byte what drawing the tree once per round wrote: components named `Type#ordinal` in
lowering order, and the payload carrying every component that loads data or holds a value from the
container, read off the instances that drew. An escape-hatch page SHALL keep drawing its write-once
bridges as trees drawn once, putting back by name what each loaded.

#### Scenario: A round draws the instance that loaded

- **WHEN** a page composes a stateless component that loads, and draws whether the instance drawing it
  is the one whose prefetch ran
- **THEN** the markup says it is

#### Scenario: What a round could not put back

- **WHEN** a component's prefetch stores a delegate, and what it builds depends on that delegate and
  loads data of its own
- **THEN** a full load draws what it composed with its data, and a navigation's payload carries it

#### Scenario: A chain deeper than the bound

- **WHEN** each component of a chain exists only once the one above it has loaded, twelve deep
- **THEN** the page is drawn after five rounds, and every link the payload says loaded reads loaded in
  the markup
