# component-testing Specification

## Purpose
How an app's own tests draw a component with no browser and no window. On the web, through the
vocabulary's live owner and the web realizer the server uses, so a test sees the frames a browser would,
acts on a control by the name a screen reader announces, and reads what is announced in the vocabulary's
words rather than ARIA's. On Photon, through the host the native shells drive, so a test measures each
node's box and its text's lines as a shell lays them out, the same on every OS.

## Requirements

### Requirement: A test renders a component with a theme and a culture, and no browser

`eQuantic.UI.Testing` SHALL provide `ComponentTester`, which a test in an ordinary xUnit or NUnit
project constructs with a theme, a culture, a viewport width in dp, a density and a service provider,
each optional, and which depends on no test framework. Without a theme it SHALL use the SDK's
(`PhotonTheme`), and without a culture the invariant one. Every build, lowering and event handler the
tester runs SHALL see the culture as both `CultureInfo.CurrentCulture` and `CurrentUICulture`, and the
tester SHALL restore the caller's cultures when it returns.

#### Scenario: A component that reads the theme renders without one being registered

- **WHEN** a test pumps a `Select` of `One` and `Two` with `One` chosen, with a `ComponentTester`
  constructed with no argument
- **THEN** the frame draws, and announces one combo box whose value is `One`

#### Scenario: The culture a test names is the one a component formats with

- **WHEN** a test pumps a component that writes `1234.5.ToString("N1")` with a tester for `pt-BR`
- **THEN** the markup reads `1.234,5`, and `CultureInfo.CurrentCulture` after the call is the one the
  test had before it

### Requirement: A test draws frames through a live owner

`ComponentTester.Pump(root)` SHALL mount the root in a live `BuildOwner` and draw the first frame;
`Pump()` SHALL draw the next one, building only the positions a `SetState` marked. A stateful instance
SHALL be kept by its position from frame to frame, `OnMount` SHALL run once when the frame that first
built it ends, and disposing the tester SHALL take the tree down so every `OnUnmount` runs once.
`HasScheduledFrame` SHALL say whether a `SetState` asked for a frame not yet drawn.

#### Scenario: A stateful component a page builds keeps its state

- **WHEN** a page builds a counter, the test taps the counter, and pumps again
- **THEN** the frame reads `Count: 1`, where the server's one-frame lowering of the same page reads
  `Count: 0`

#### Scenario: Mount, and unmount when the test ends

- **WHEN** a test pumps a component with `OnMount` and `OnUnmount`, pumps twice more, and disposes the
  tester
- **THEN** `OnMount` ran once and `OnUnmount` ran once

### Requirement: A test acts on a control by the name a screen reader announces

`Tap(name)` SHALL invoke the click handler of the control announced by that name, and `Tap(role,
name)` the one announced with that role and name. `EnterText(name, text)` SHALL hand the text to the
change callback of the text entry announced by that name. Neither SHALL draw a frame. A name that no
control, or more than one, is announced by SHALL fail with an exception that lists what is announced,
and so SHALL a disabled control.

#### Scenario: A form typed into and submitted

- **WHEN** a test pumps a form with an email field and a button, enters `ana@example.com` into
  `Email`, taps `Sign up` and pumps
- **THEN** the frame announces a static text `Welcome, ana@example.com`

#### Scenario: A name nothing announces

- **WHEN** a test taps `Delete` on a frame that announces `Save` and `Cancel`
- **THEN** the call throws, and its message names `Save` and `Cancel`

### Requirement: A test reads what a screen reader announces, in the vocabulary's words

`ComponentTester.Semantics` SHALL list what a screen reader announces in the current frame, in reading
order, as `Announcement`s carrying a `SemanticRole`, a name, a value and the disabled, checked,
selected, expanded and current states and the heading level. It SHALL be computed from the ARIA the
web realizer lowered: the role from the `role` attribute or the element's own, the name from
`aria-labelledby`, `aria-label`, `alt`, or the text of a role named from its content. A subtree hidden
by `aria-hidden="true"`, `hidden`, an inline `display: none` or `visibility: hidden`, or an adaptive
gate that does not show at the tester's width SHALL NOT be announced. `Find(name)` and
`Find(role, name)` SHALL answer the one announcement that matches, and fail as `Tap` does otherwise.
`Html` SHALL be the markup of the current frame.

#### Scenario: A checkbox

- **WHEN** a test pumps `Checkbox(true, null, "Accept")`
- **THEN** `Semantics` holds one announcement with role `Checkbox`, name `Accept` and checked `True`

#### Scenario: The arm a width hides

- **WHEN** a test pumps an adaptive node whose compact arm says `Phone` and whose expanded arm says
  `Desk`, with a tester 1280 dp wide
- **THEN** `Semantics` announces `Desk` and not `Phone`, and `Html` holds both

### Requirement: A page's server data loads before its first frame

`PumpPageAsync(page)` SHALL call `PrefetchAsync` on every `IServerPrefetch` component the tree
reaches, one at a time and with the tester's services, mark the position that keeps each one so that
only its subtree builds again, with that instance, and do the same for any component that build
reaches for the first time, until a round reaches none; then it SHALL draw the first frame. The
instance that prefetched SHALL be the one the frame draws. A page still reaching new components after
five rounds, the server's bound, SHALL fail with an exception naming them.

#### Scenario: A page that reads a service

- **WHEN** a page's prefetch reads `675617` from a service the test registered, and the test calls
  `PumpPageAsync`
- **THEN** the first frame reads `Downloads: 675617`

#### Scenario: A stateless component that loads, inside a page

- **WHEN** a page builds a new stateless component on every build, and that component's prefetch
  loads `Title stats`
- **THEN** the first frame reads `Title stats`, where rebuilding every component after the prefetch
  hands the position a fresh instance each round and never settles

#### Scenario: A component the data brings

- **WHEN** a page builds one prefetching card per item its own prefetch loaded, and each card's
  prefetch loads its title
- **THEN** the first frame shows every card's title

#### Scenario: A page that never settles

- **WHEN** each prefetch of a page builds a new component that prefetches
- **THEN** `PumpPageAsync` throws, saying the page still asks for data after five rounds

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
