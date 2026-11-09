## ADDED Requirements

### Requirement: A test measures a component as Photon lays it out, with no window

`eQuantic.UI.Native.Testing` SHALL provide `PhotonTester`, which a test constructs with a theme, a
culture, a viewport width and height in dp, a density, a theme mode and a type scale, each optional,
and which depends on no test framework. `Pump(root)` SHALL draw the first frame of the root through a
`PhotonHost` with no window and no GPU, and `Pump()` the next one, building what a `SetState` marked.
`Root` SHALL be the page's layout and `Layers` each shown overlay layer's, in paint order, as
`MeasuredNode`s carrying the node laid out, its box in dp, the lines its text measured (null for a node
that is not text), and its children. A measurement SHALL stay what its frame measured after the next
frame is drawn.

#### Scenario: A text's lines are the ones its width allows

- **WHEN** a test pumps a column holding a sentence of twelve words, at 1280 dp and again at 160 dp
- **THEN** the sentence's node measures one line at 1280 dp and more than one at 160 dp, and its box is
  inside the viewport in both

#### Scenario: The density a test names is the one the controls are laid out at

- **WHEN** a test measures the same button with a tester at `Comfortable` and another at `Compact`
- **THEN** the button's box is shorter at `Compact`

#### Scenario: A measurement held across a frame keeps what it measured

- **WHEN** a test keeps the measurement of a counter's text from its first frame, then twice marks the
  counter with a `SetState` that changes the text's length and pumps a frame, the second of which
  reuses the first frame's nodes, as a host that recycles frames does
- **THEN** the kept measurement still reads the first frame's box and line, and the newest root reads
  the newest text

### Requirement: The same measurement on every OS, and the one the native goldens are held to

`PhotonTester` SHALL measure text with the engine's deterministic measurer and draw it as the native
goldens draw it, so a measurement is the same on every OS a test runs on. `ReadPixelsSrgb()` SHALL paint
the last frame through the CPU reference backend as sRGB RGBA rows of the viewport's size.

#### Scenario: A golden scene painted through the package matches its golden

- **WHEN** the SDK's native suite pumps the wave-2 gallery through a `PhotonTester` with the SDK's
  theme, light, at 380 by 460 dp, and reads its pixels
- **THEN** they match `wave2-gallery-light.png`, the golden the host-driven test is held to

### Requirement: A test finds a measured node by the name it announces

`PhotonTester.Find(name)` and `Find(role, name)` SHALL return the `MeasuredNode` of the one node
Photon's semantics tree announces by that name (and role), mapped by its position. A name announced by
no node, or by more than one, SHALL fail naming what the frame announces.

#### Scenario: A button is found by its label

- **WHEN** a test pumps a card holding a `Button` labelled `Sign up` and finds `Sign up`
- **THEN** it answers the button's node, whose box lies inside the card's

#### Scenario: A name nothing announces fails with what is announced

- **WHEN** a test finds `Log out` in a frame that announces `Sign up` and `Ana`
- **THEN** it fails, and the message names `Sign up` and `Ana`

### Requirement: Photon testers draw from one UI thread a process

Photon's dispatcher and frame ticker belong to the process, as an app's one UI thread does, so every
`PhotonTester` whose tree is mounted at the same time SHALL draw from one thread. A pump from a second
thread while another tester is mounted SHALL fail, naming the rule and how to run the tests one at a
time; testers mounted on one thread SHALL measure side by side.

#### Scenario: A second thread is refused while a tester is mounted, and served once it is disposed

- **WHEN** a test mounts a tester, and pumps another on a thread of its own
- **THEN** that pump fails saying Photon has one UI thread a process, and once the first tester is
  disposed, a tester on the other thread draws

#### Scenario: Two testers on one thread measure side by side

- **WHEN** a test mounts a button in a `Comfortable` tester and in a `Compact` one, both alive
- **THEN** each answers its own box, the `Compact` one shorter

### Requirement: The native half is opt-in and experimental

`eQuantic.UI.Testing` SHALL reference nothing of the native track, and `eQuantic.UI.Native.Testing`
SHALL carry `[Experimental("EQX0001")]`, as every assembly of the native track does.

#### Scenario: The web half stays off the native track

- **WHEN** the layering pin reads the testing packages' project references
- **THEN** `eQuantic.UI.Testing` references the web realizer alone, and `eQuantic.UI.Native.Testing`
  the Photon host and the reference backend

#### Scenario: The native half is marked as the track is

- **WHEN** a test reads the attributes of the assembly `PhotonTester` lives in
- **THEN** it carries `ExperimentalAttribute` with the id `EQX0001`
