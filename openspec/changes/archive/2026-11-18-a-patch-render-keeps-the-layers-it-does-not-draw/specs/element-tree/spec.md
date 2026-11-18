## MODIFIED Requirements

### Requirement: A layer builds when it is shown

An `Overlay`'s child and an `Anchored` panel SHALL be built when their layer is realized in a frame,
and a layer that is not realized in a frame (an overlay closed with motion, a panel closed or no
longer hovered) SHALL unmount what it holds. A frame whose realizer drew only parts of the tree
(`BuildOwner.DrewOnly`) SHALL judge only the layers inside those parts: a layer outside them SHALL keep
what it holds, and a frame whose realizer says nothing SHALL have drawn the whole tree.
`BuildOwner.WaitsForALayer` SHALL answer whether a marked position waits for its layer to be shown.

#### Scenario: A menu closes

- **WHEN** an anchored panel holding a stateful component is open for a frame and closed in the next
- **THEN** the component mounted in the first frame and unmounted in the second

#### Scenario: A patch render elsewhere on the page

- **WHEN** a page holds a counter and an open non-modal overlay over another counter, and the first
  counter is pressed twice in the browser, then the second
- **THEN** no counter unmounts, and the second reads `b: 1`

#### Scenario: A panel closed in a patch render

- **WHEN** a component holding an open anchored panel over a counter closes it with its own `SetState`,
  and the browser lowers that component alone
- **THEN** the panel's counter unmounts

#### Scenario: A frame that drew one part

- **WHEN** an owner's frame says it drew only a counter's position, and an open overlay beside it was
  not shown
- **THEN** the overlay keeps what it holds, and a later frame that says nothing takes it out

#### Scenario: Photon draws every frame whole

- **WHEN** a Photon host draws two frames after a `SetState` outside an open overlay and an open
  anchored panel, then one after a `SetState` inside the overlay
- **THEN** neither layer's component unmounts, and the one inside the overlay builds in the third frame
