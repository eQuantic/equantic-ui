## ADDED Requirements

### Requirement: The browser builds through the element engine

The browser SHALL build every component of a page through the element engine's owner, and its lowering
SHALL lower what each position built and keep the instance each position keeps, without building again.
A nested component's state change SHALL build that component's element and no other before the page
lowers again. Hydration SHALL build the element tree before it walks the server's DOM, and the server's
data for a component SHALL reach every fresh instance at its position before that instance builds.

#### Scenario: A nested state change builds only its own element

- **WHEN** a page holds two stateful components side by side and one of them changes its state
- **THEN** that component builds again and neither its sibling nor the page builds

#### Scenario: A page's own state change builds what it composes

- **WHEN** a page changes its own state and hands a nested stateful component new configuration
- **THEN** the nested component keeps its instance and its state, and builds with the new configuration

#### Scenario: Hydration adopts the server's state before the first build

- **WHEN** a page the server drew with a component's loaded data hydrates
- **THEN** the component builds first with that data, under the key of the position the server named it by

#### Scenario: A stateless component handed anew at a kept position

- **WHEN** a stateful component builds again and hands a fresh stateless component the server loaded
  data for to the same position
- **THEN** the fresh instance builds with that data

#### Scenario: A component that throws costs its own subtree

- **WHEN** a nested component's build throws
- **THEN** its position draws the failure and the rest of the page draws as before

### Requirement: Every arm of an adaptive node is a position on the web

An owner the web makes SHALL hold every arm an adaptive node declares, compact at slot 0, medium at
slot 1 and expanded at slot 2, rather than the arm a window size class resolves, since the web draws
every arm behind its own media gate. No window size class SHALL decide an arm on the web, and a resize
SHALL need no build for the arm on screen to be one an element holds. An owner that draws one arm
(Photon's) SHALL keep resolving it.

#### Scenario: The arm on screen is held at any width

- **WHEN** a page with an adaptive node holding a stateful component in each arm is drawn, and the window
  crosses the node's threshold
- **THEN** the component in the arm now shown keeps its instance and answers a press without a build

### Requirement: Server data is keyed by a component's position

The server and the browser SHALL name a component's server data by where it stands: the tree of the
render it is in (`r0`, `r1`, … in the order the render draws its trees), its type, and the slot of each
position from the tree's root down to it (`Element.SlotsBelow`), as `r{tree}/{type}@{slots}`. The key
SHALL not depend on the order either side builds in, nor on which arm of an adaptive node a window
shows. A page's own component SHALL keep `Type#0`. The server SHALL measure a page's keys below the page,
which the browser builds by itself (`MarkupHost.ForPage`).

#### Scenario: One type in two arms and after them

- **WHEN** a page holds a component type that loads data in the compact and the expanded arm of an
  adaptive node and once after the node, each with its own data
- **THEN** the browser hands each instance its own data, whichever arm the window shows

## MODIFIED Requirements

### Requirement: The server draws a write-once page through an owner for the request

The server SHALL draw a write-once page through a structural `MarkupHost` it keeps for the request,
each prefetch round a frame of that one tree, and a client navigation's discovery of it SHALL run
the same rounds through the same kind of host, keeping each component's wire form as it loads. After a frame, `ServerDataRounds` SHALL name the
components to ask: each that loads data, that the frame reached and that was not asked, in the order
the frame named them, save a stateless one below another asked in the same round, which SHALL wait for
the next. A component that loaded SHALL have its positions marked, and only those subtrees SHALL build
in the next frame, with the instance that loaded. A bound of five rounds SHALL count a round that
reached a position, or a component holding none, that no round before it had; past it the page SHALL
be drawn with what it has, its markup and payload agreeing. The markup SHALL be byte for byte what
drawing the tree once per round wrote, and the hydration payload SHALL name each component by its
position (`r{tree}/{type}@{slots}`) and carry every component that loads data or holds a value from the
container, read off the instances that drew. An escape-hatch page SHALL draw each write-once bridge
through a structural host of its own, drawn once, putting back before a fresh instance builds what an
earlier round loaded at its position.

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
